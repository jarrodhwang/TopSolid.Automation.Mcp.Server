using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using Microsoft.Win32.SafeHandles;

namespace TopSolid.Automation.AI.Studio.Preview.Streaming;

/// <summary>
/// Disk-backed binary STL. All file offsets/counts use 64 bits; no whole-file byte array,
/// base64 string, WPF mesh, or adjacency dictionary is retained. A tile is the allocation unit.
/// </summary>
internal sealed class StlChunkSource : IPreviewChunkSource
{
    internal const int TrianglesPerChunk = 32768;
    private readonly SafeFileHandle handle;
    public long TriangleCount { get; }
    public long ByteLength { get; }
    public PreviewChunk[] Chunks { get; }
    public PreviewBounds Bounds { get; private set; } = PreviewBounds.Empty;
    private StlChunkSource(SafeFileHandle handle, long length, long triangles)
    {
        this.handle = handle; ByteLength = length; TriangleCount = triangles;
        Chunks = new PreviewChunk[checked((int)((triangles + TrianglesPerChunk - 1) / TrianglesPerChunk))];
    }

    internal static async Task<StlChunkSource> OpenAsync(string file, PreviewResources resources, CancellationToken token,
        IProgress<double>? progress = null)
    {
        token.ThrowIfCancellationRequested();
        var handle = File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, FileOptions.Asynchronous | FileOptions.RandomAccess);
        try
        {
            var length = RandomAccess.GetLength(handle);
            var header = new byte[84];
            if (length < header.Length) throw new InvalidDataException("Truncated binary STL.");
            await ReadExactlyAsync(handle, header, 0, token).ConfigureAwait(false);
            var triangles = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(80));
            ValidateLength(length, triangles);
            var result = new StlChunkSource(handle, length, triangles);
            long finished = 0;
            // Each worker owns at most one raw tile. Retain only its bounds after this pass.
            var workers = (int)Math.Max(1, Math.Min(resources.Workers, resources.CpuResidentBytes / (TrianglesPerChunk * 50L)));
            await Parallel.ForEachAsync(Enumerable.Range(0, result.Chunks.Length),
                new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = workers }, async (id, ct) =>
                {
                    var first = (long)id * TrianglesPerChunk;
                    var count = (int)Math.Min(TrianglesPerChunk, triangles - first);
                    var buffer = ArrayPool<byte>.Shared.Rent(count * 50);
                    try
                    {
                        await ReadExactlyAsync(handle, buffer.AsMemory(0, count * 50), checked(84L + first * 50), ct).ConfigureAwait(false);
                        var bounds = Scan(buffer, count, ct);
                        result.Chunks[id] = new(id, first, count, bounds);
                        progress?.Report((double)Interlocked.Add(ref finished, count) / triangles);
                    }
                    finally { ArrayPool<byte>.Shared.Return(buffer); }
                }).ConfigureAwait(false);
            foreach (var chunk in result.Chunks) result.Bounds = result.Bounds.Include(chunk.Bounds);
            return result;
        }
        catch { handle.Dispose(); throw; }
    }

    internal static void ValidateLength(long length, uint triangles)
    {
        if (triangles == 0 || length != checked(84L + triangles * 50L))
            throw new InvalidDataException("Invalid binary STL triangle count or file length.");
    }

    public async Task<PreviewChunkMesh> ReadAsync(int chunkId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if ((uint)chunkId >= Chunks.Length) throw new ArgumentOutOfRangeException(nameof(chunkId));
        var chunk = Chunks[chunkId];
        var buffer = ArrayPool<byte>.Shared.Rent(chunk.TriangleCount * 50);
        try
        {
            await ReadExactlyAsync(handle, buffer.AsMemory(0, chunk.TriangleCount * 50), checked(84L + chunk.FirstTriangle * 50), token).ConfigureAwait(false);
            return Decode(chunk, buffer, token);
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private static PreviewBounds Scan(byte[] buffer, int count, CancellationToken token)
    {
        var bounds = PreviewBounds.Empty;
        for (var triangle = 0; triangle < count; triangle++)
        {
            if ((triangle & 1023) == 0) token.ThrowIfCancellationRequested();
            for (var vertex = 0; vertex < 3; vertex++) bounds = bounds.Include(Point(buffer, triangle * 50 + 12 + vertex * 12));
        }
        return bounds;
    }

    private static PreviewChunkMesh Decode(PreviewChunk chunk, byte[] buffer, CancellationToken token)
    {
        var positions = new Vector3[chunk.TriangleCount * 3];
        var normals = new Vector3[positions.Length]; var indices = new int[positions.Length];
        var origin = chunk.Bounds.Center;
        for (var triangle = 0; triangle < chunk.TriangleCount; triangle++)
        {
            if ((triangle & 1023) == 0) token.ThrowIfCancellationRequested();
            var start = triangle * 3;
            for (var vertex = 0; vertex < 3; vertex++)
            {
                var at = start + vertex;
                positions[at] = Point(buffer, triangle * 50 + 12 + vertex * 12).RelativeTo(origin); indices[at] = at;
            }
            // System.Numerics selects the available SIMD ISA at runtime on both Intel and AMD.
            // Recompute from winding; STL normals and vendor color bytes are untrusted.
            var normal = Vector3.Cross(positions[start + 1] - positions[start], positions[start + 2] - positions[start]);
            var length = normal.Length(); normal = length > 1e-20f ? normal / length : Vector3.Zero;
            normals[start] = normals[start + 1] = normals[start + 2] = normal;
        }
        return new(chunk, origin, positions, normals, indices);
    }

    private static PreviewPoint Point(byte[] bytes, int offset)
        => new(Number(bytes, offset), Number(bytes, offset + 4), Number(bytes, offset + 8));
    private static float Number(byte[] bytes, int offset)
    {
        var value = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset, 4));
        if (!float.IsFinite(value) || Math.Abs(value) > 1e9f) throw new InvalidDataException("Invalid preview coordinate.");
        return value;
    }
    private static async ValueTask ReadExactlyAsync(SafeFileHandle handle, Memory<byte> buffer, long offset, CancellationToken token)
    {
        var completed = 0;
        while (completed < buffer.Length)
        {
            var read = await RandomAccess.ReadAsync(handle, buffer[completed..], checked(offset + completed), token).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("Preview changed or was truncated while reading.");
            completed += read;
        }
    }
    public void Dispose() => handle.Dispose();
}
