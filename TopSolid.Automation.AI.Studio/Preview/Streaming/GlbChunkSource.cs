using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using N = System.Numerics;

namespace TopSolid.Automation.AI.Studio.Preview.Streaming;

/// <summary>File-backed native GLB geometry. Each tile retains its material, instance
/// transform and vertex normals; neither whole-file arrays nor WPF copies are allocated.</summary>
internal sealed class GlbChunkSource : IPreviewChunkSource
{
    private sealed record Accessor(long Offset, int Stride, int Count, int Component);
    private sealed record Primitive(Accessor Positions, Accessor? Normals, Accessor? Indices,
        Matrix3D Transform, Matrix3D Inverse, Color Color, int Triangles);
    private sealed record Tile(Primitive Primitive, int First, int Count);
    private readonly MemoryMappedFile file;
    private readonly object gate = new();
    private bool disposed;
    private Tile[] tiles = [];
    public long ByteLength { get; }
    public long TriangleCount { get; private set; }
    public PreviewChunk[] Chunks { get; private set; } = [];
    public PreviewBounds Bounds { get; private set; } = PreviewBounds.Empty;
    private GlbChunkSource(MemoryMappedFile file, long length) { this.file = file; ByteLength = length; }

    internal static Task<GlbChunkSource> OpenAsync(string path, PreviewResources resources, CancellationToken token, bool nativeColors = false)
        => Task.Run(() => Open(path, resources, token, nativeColors), token);

    private static GlbChunkSource Open(string path, PreviewResources resources, CancellationToken token, bool nativeColors)
    {
        token.ThrowIfCancellationRequested();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        var length = stream.Length;
        if (length < 28 || length > uint.MaxValue) throw new InvalidDataException("Invalid GLB length.");
        var map = MemoryMappedFile.CreateFromFile(stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
        var source = new GlbChunkSource(map, length);
        try
        {
            using var view = source.OpenView();
            if (view.ReadUInt32(0) != 0x46546c67 || view.ReadUInt32(4) != 2 || view.ReadUInt32(8) != length || view.ReadUInt32(16) != 0x4e4f534a)
                throw new InvalidDataException("Invalid GLB header.");
            var jsonLength = view.ReadUInt32(12);
            if (jsonLength < 2 || jsonLength > 16 * 1024 * 1024 || jsonLength > length - 28) throw new InvalidDataException("Invalid GLB metadata length.");
            var jsonBytes = new byte[(int)jsonLength]; view.ReadArray(20, jsonBytes, 0, jsonBytes.Length);
            JObject root;
            using (var reader = new JsonTextReader(new StringReader(Encoding.UTF8.GetString(jsonBytes))) { MaxDepth = 48, DateParseHandling = DateParseHandling.None })
            { root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); if (reader.Read()) throw new InvalidDataException("Unexpected GLB JSON."); }
            var binStart = 28L + jsonLength;
            if (view.ReadUInt32(binStart - 4) != 0x004e4942 || view.ReadUInt32(binStart - 8) != length - binStart ||
                (string?)root["asset"]?["version"] != "2.0" || root["extensionsRequired"] is JArray { Count: > 0 } ||
                root["buffers"] is not JArray { Count: 1 } buffers || buffers[0]["uri"] != null ||
                root["skins"] is JArray { Count: > 0 } || root["animations"] is JArray { Count: > 0 }) throw new InvalidDataException("Unsupported GLB data.");
            var binaryLength = (long?)buffers[0]["byteLength"] ?? -1;
            if (binaryLength < 0 || binaryLength > length - binStart || length - binStart - binaryLength > 3) throw new InvalidDataException("Invalid GLB buffer.");
            var list = new List<Tile>(); var visited = new HashSet<int>();
            foreach (var node in Entry("scenes", (int?)root["scene"] ?? 0)["nodes"] as JArray ?? throw new InvalidDataException("Missing scene roots."))
                Walk((int)node, Matrix3D.Identity, 0);
            if (list.Count == 0) throw new InvalidDataException("No displayable geometry.");
            source.tiles = list.ToArray(); source.Chunks = new PreviewChunk[list.Count];
            Parallel.For(0, list.Count, new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = Math.Min(8, resources.Workers) }, id =>
            {
                using var input = source.OpenView(); var tile = list[id]; var bounds = PreviewBounds.Empty;
                for (var i = 0; i < tile.Count * 3; i++)
                {
                    if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                    var p = Position(input, tile.Primitive, Index(input, tile.Primitive, tile.First * 3 + i));
                    bounds = bounds.Include(new PreviewPoint(p.X, p.Y, p.Z));
                }
                source.Chunks[id] = new(id, tile.First, tile.Count, bounds, tile.Primitive.Color);
            });
            foreach (var chunk in source.Chunks) source.Bounds = source.Bounds.Include(chunk.Bounds);
            return source;

            JObject Entry(string key, int index) => root[key] is JArray items && index >= 0 && index < items.Count && items[index] is JObject item
                ? item : throw new InvalidDataException("Invalid GLB reference.");
            Accessor ReadAccessor(int index, string type, int? component)
            {
                var a = Entry("accessors", index); var v = Entry("bufferViews", (int?)a["bufferView"] ?? -1);
                var count = (int?)a["count"] ?? -1; var kind = (int?)a["componentType"] ?? -1;
                var width = kind switch { 5121 => 1, 5123 => 2, 5125 or 5126 => 4, _ => throw new InvalidDataException("Invalid accessor component.") };
                var size = width * (type == "VEC3" ? 3 : 1); var stride = (int?)v["byteStride"] ?? size;
                var offset = (long?)v["byteOffset"] ?? 0; var within = (long?)a["byteOffset"] ?? 0; var sizeBytes = (long?)v["byteLength"] ?? -1;
                if ((string?)a["type"] != type || component.HasValue && component != kind || a["sparse"] != null || (bool?)a["normalized"] == true ||
                    (int?)v["buffer"] != 0 || count <= 0 || stride < size || stride > 252 || offset < 0 || within < 0 || sizeBytes < 0 ||
                    offset > binaryLength - sizeBytes || within > sizeBytes - ((long)(count - 1) * stride + size)) throw new InvalidDataException("Invalid accessor bounds.");
                return new(binStart + offset + within, stride, count, kind);
            }
            void Walk(int index, Matrix3D parent, int depth)
            {
                token.ThrowIfCancellationRequested();
                if (depth > 64 || visited.Count >= 100000 || !visited.Add(index)) throw new InvalidDataException("Invalid scene hierarchy.");
                var node = Entry("nodes", index); var transform = GlbPreviewReader.Transform(node); transform.Append(parent);
                if (node["skin"] != null || node["weights"] != null || !transform.HasInverse) throw new InvalidDataException("Unsupported mesh transform.");
                var inverse = transform; inverse.Invert();
                if (node["mesh"] != null)
                    foreach (var value in Entry("meshes", (int)node["mesh"]!)["primitives"] as JArray ?? throw new InvalidDataException("Missing primitives."))
                    {
                        if (value is not JObject p || ((int?)p["mode"] ?? 4) != 4 || p["targets"] != null || p["extensions"] != null)
                            throw new InvalidDataException("Unsupported primitive.");
                        var positions = ReadAccessor((int?)p["attributes"]?["POSITION"] ?? -1, "VEC3", 5126);
                        var normals = p["attributes"]?["NORMAL"] == null ? null : ReadAccessor((int)p["attributes"]!["NORMAL"]!, "VEC3", 5126);
                        var indices = p["indices"] == null ? null : ReadAccessor((int)p["indices"]!, "SCALAR", null);
                        var count = indices?.Count ?? positions.Count;
                        if (count % 3 != 0 || normals != null && normals.Count != positions.Count || indices != null && indices.Component is not (5121 or 5123 or 5125))
                            throw new InvalidDataException("Invalid primitive buffers.");
                        var color = GlbPreviewReader.ReadColor(p["material"] == null ? null : Entry("materials", (int)p["material"]!), nativeColors);
                        var primitive = new Primitive(positions, normals, indices, transform, inverse, color, count / 3);
                        source.TriangleCount = checked(source.TriangleCount + count / 3);
                        if (source.TriangleCount > Math.Max(1000000, binaryLength * 4)) throw new InvalidDataException("Excessive instance expansion.");
                        for (var first = 0; first < primitive.Triangles; first += StlChunkSource.TrianglesPerChunk)
                        {
                            if (list.Count >= Math.Min(1000000, resources.CpuResidentBytes / 1024)) throw new InvalidDataException("Preview index exceeds its memory budget.");
                            list.Add(new(primitive, first, Math.Min(StlChunkSource.TrianglesPerChunk, primitive.Triangles - first)));
                        }
                    }
                foreach (var child in node["children"] as JArray ?? []) Walk((int)child, transform, depth + 1);
            }
        }
        catch (AggregateException error)
        {
            source.Dispose(); token.ThrowIfCancellationRequested();
            throw new InvalidDataException("Invalid GLB geometry.", error.Flatten());
        }
        catch { source.Dispose(); throw; }
    }

    public Task<PreviewChunkMesh> ReadAsync(int chunkId, CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        if ((uint)chunkId >= tiles.Length) throw new ArgumentOutOfRangeException(nameof(chunkId));
        using var view = OpenView(); var tile = tiles[chunkId]; var p = tile.Primitive; var chunk = Chunks[chunkId]; var origin = chunk.Bounds.Center;
        var positions = new N.Vector3[tile.Count * 3]; var normals = new N.Vector3[positions.Length]; var indices = new int[positions.Length];
        for (var i = 0; i < positions.Length; i++)
        {
            if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
            var index = Index(view, p, tile.First * 3 + i); var point = Position(view, p, index);
            positions[i] = new PreviewPoint(point.X, point.Y, point.Z).RelativeTo(origin); indices[i] = i;
            if (p.Normals is { } n)
            {
                var at = n.Offset + (long)index * n.Stride; var x = Number(view, at); var y = Number(view, at + 4); var z = Number(view, at + 8); var m = p.Inverse;
                var normal = new Vector3D(x*m.M11 + y*m.M12 + z*m.M13, x*m.M21 + y*m.M22 + z*m.M23, x*m.M31 + y*m.M32 + z*m.M33);
                if (normal.LengthSquared < 1e-24 || !double.IsFinite(normal.LengthSquared)) throw new InvalidDataException("Invalid normal.");
                normal.Normalize(); normals[i] = new((float)normal.X, (float)-normal.Z, (float)normal.Y);
            }
        }
        for (var i = 0; i < indices.Length; i += 3)
        {
            if (p.Transform.Determinant < 0) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
            if (p.Normals != null) continue;
            var normal = N.Vector3.Cross(positions[indices[i + 1]] - positions[i], positions[indices[i + 2]] - positions[i]);
            var length = normal.Length(); normals[i] = normals[i+1] = normals[i+2] = length > 1e-20f ? normal / length : N.Vector3.Zero;
        }
        return new PreviewChunkMesh(chunk, origin, positions, normals, indices);
    }, token);

    private static int Index(MemoryMappedViewAccessor view, Primitive p, int i)
    {
        var a = p.Indices; if (a == null) return i;
        var at = a.Offset + (long)i * a.Stride;
        var index = a.Component switch { 5121 => view.ReadByte(at), 5123 => view.ReadUInt16(at), _ => (long)view.ReadUInt32(at) };
        if (index >= p.Positions.Count) throw new InvalidDataException("Mesh index outside vertex buffer.");
        return (int)index;
    }
    private static Point3D Position(MemoryMappedViewAccessor view, Primitive p, int index)
    {
        var at = p.Positions.Offset + (long)index * p.Positions.Stride;
        var world = p.Transform.Transform(new Point3D(Number(view, at), Number(view, at+4), Number(view, at+8)));
        var result = new Point3D(world.X*1000, -world.Z*1000, world.Y*1000);
        if (!double.IsFinite(result.X) || !double.IsFinite(result.Y) || !double.IsFinite(result.Z) ||
            Math.Max(Math.Abs(result.X), Math.Max(Math.Abs(result.Y), Math.Abs(result.Z))) > 1e9) throw new InvalidDataException("Invalid coordinate.");
        return result;
    }
    private static float Number(MemoryMappedViewAccessor view, long at)
    { var value = view.ReadSingle(at); return float.IsFinite(value) ? value : throw new InvalidDataException("Non-finite mesh value."); }
    private MemoryMappedViewAccessor OpenView()
    { lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); return file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read); } }
    public void Dispose() { lock (gate) { if (disposed) return; disposed = true; file.Dispose(); } }
}
