using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using TopSolid.Automation.AI.Studio.Preview.Streaming;

var folder = Path.Combine(Path.GetTempPath(), "TopSolid-preview-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
var file = Path.Combine(folder, "tiles.stl");
try
{
    var triangles = args.Length == 2 && args[0] == "--benchmark" ? uint.Parse(args[1]) : 100_003u;
    var resources = PreviewResources.Detect();
    // The STL file format, not a product-size setting, uses a uint triangle count.
    StlChunkSource.ValidateLength(15_000_000_084L, 300_000_000);
    StlChunkSource.ValidateLength(84L + uint.MaxValue * 50L, uint.MaxValue);
    Throws<InvalidDataException>(() => StlChunkSource.ValidateLength(15_000_000_083L, 300_000_000));
    using (var output = new BinaryWriter(File.Create(file)))
    {
        output.Write(new byte[80]); output.Write(triangles);
        for (uint triangle = 0; triangle < triangles; triangle++)
        {
            output.Write(0f); output.Write(0f); output.Write(0f);
            var x = triangle % 1000 * 2f; var y = triangle / 1000 * 2f;
            foreach (var p in new[] { new Vector3(x, y, 0), new Vector3(x + 1, y, 0), new Vector3(x, y + 1, 0) })
            { output.Write(p.X); output.Write(p.Y); output.Write(p.Z); }
            output.Write((ushort)0);
        }
    }
    var watch = Stopwatch.StartNew();
    using (var source = await StlChunkSource.OpenAsync(file, resources, CancellationToken.None))
    {
        var indexMs = watch.Elapsed.TotalMilliseconds;
        True(source.TriangleCount == triangles, "Total model triangle count was truncated");
        True(source.Chunks.Sum(c => (long)c.TriangleCount) == triangles, "Index lost geometry");
        True(source.ByteLength == 84L + triangles * 50L, "Source size was narrowed");
        var first = await source.ReadAsync(0, CancellationToken.None);
        var last = await source.ReadAsync(source.Chunks.Length - 1, CancellationToken.None);
        True(first.Normals.All(n => n == Vector3.UnitZ) && last.Normals.All(n => n == Vector3.UnitZ), "Winding or SIMD normals changed");
        True(last.Indices.Length == source.Chunks[^1].TriangleCount * 3, "Last partial tile lost triangles");
        True(first.Positions[0] + first.Origin.RelativeTo(new()) == Vector3.Zero, "Tile-local origin changed coordinates");
        var camera = new PreviewCamera(source.Bounds.Center, Vector3.UnitZ, Vector3.UnitY, source.Bounds.Radius * 3, 1);
        var budget = first.Chunk.ResidentBytes;
        var plan = PreviewResidency.Select(source.Chunks, camera, budget);
        True(plan.ResidentBytes <= budget && plan.Proxies.Count > 0, "GPU budget did not retain nonresident bounds proxies");
        True(plan.Detailed.Count + plan.Proxies.Count == source.Chunks.Length, "Visible source geometry disappeared");
        var far = camera with { Target = new(1e8, 1e8, 1e8), Width = 1 };
        var culled = PreviewResidency.Select(source.Chunks, far, budget);
        True(culled.Detailed.Count == 0 && culled.Proxies.Count == 0, "Offscreen geometry consumed residency");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        try { await source.ReadAsync(0, canceled.Token); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { }
        var report = new { triangles, bytes = source.ByteLength, chunks = source.Chunks.Length, indexMilliseconds = indexMs,
            decodeTwoTilesMilliseconds = watch.Elapsed.TotalMilliseconds - indexMs, resources.Workers,
            simd = Vector.IsHardwareAccelerated, peakWorkingSetMiB = Process.GetCurrentProcess().PeakWorkingSet64 / 1048576d,
            note = "Synthetic STL indexing and two tile decodes only; not CAD surfaces, GPU frame rate, or native CAM file validation." };
        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }
    var largeCpu = PreviewResources.FromHardware(64L << 30, 12L << 30, 32);
    True(largeCpu.Workers == 31 && largeCpu.GpuResidentBytes > 6L << 30, "Workstation resources were clamped to entry-level defaults");
    using (var stream = File.OpenWrite(file)) { stream.Position = 96; stream.Write(BitConverter.GetBytes(float.NaN)); }
    try { using var invalid = await StlChunkSource.OpenAsync(file, resources, CancellationToken.None); throw new Exception("Non-finite geometry accepted"); }
    catch (InvalidDataException) { }
    Console.WriteLine("PASS: 64-bit sizes, bounded allocations, all tiles, winding, origin, culling, residency, cancellation, invalid data");
}
finally { File.Delete(file); Directory.Delete(folder); }

static void True(bool value, string message) { if (!value) throw new Exception(message); }
static void Throws<T>(Action action) where T : Exception
{ try { action(); } catch (T) { return; } throw new Exception(typeof(T).Name + " was not thrown"); }
