using System.Numerics;

namespace TopSolid.Automation.AI.Studio.Preview.Streaming;

internal readonly record struct PreviewPoint(double X, double Y, double Z)
{
    internal Vector3 RelativeTo(PreviewPoint origin) => new((float)(X - origin.X), (float)(Y - origin.Y), (float)(Z - origin.Z));
}

internal readonly record struct PreviewBounds(PreviewPoint Min, PreviewPoint Max)
{
    internal static PreviewBounds Empty => new(new(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity),
        new(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity));
    internal bool IsEmpty => Min.X > Max.X;
    internal PreviewPoint Center => new(Min.X + (Max.X - Min.X) / 2, Min.Y + (Max.Y - Min.Y) / 2, Min.Z + (Max.Z - Min.Z) / 2);
    internal double Radius => Math.Sqrt(Math.Pow((Max.X - Min.X) / 2, 2) + Math.Pow((Max.Y - Min.Y) / 2, 2) + Math.Pow((Max.Z - Min.Z) / 2, 2));
    internal PreviewBounds Include(PreviewPoint p) => new(new(Math.Min(Min.X, p.X), Math.Min(Min.Y, p.Y), Math.Min(Min.Z, p.Z)),
        new(Math.Max(Max.X, p.X), Math.Max(Max.Y, p.Y), Math.Max(Max.Z, p.Z)));
    internal PreviewBounds Include(PreviewBounds b) => b.IsEmpty ? this : Include(b.Min).Include(b.Max);
}

internal sealed record PreviewChunk(int Id, long FirstTriangle, int TriangleCount, PreviewBounds Bounds, System.Windows.Media.Color? Color = null)
{
    internal long ResidentBytes => TriangleCount * 3L * (12 + 12 + 4); // position, normal, index
}

internal sealed record PreviewChunkMesh(PreviewChunk Chunk, PreviewPoint Origin, Vector3[] Positions, Vector3[] Normals, int[] Indices);

internal interface IPreviewChunkSource : IDisposable
{
    long TriangleCount { get; }
    long ByteLength { get; }
    PreviewChunk[] Chunks { get; }
    PreviewBounds Bounds { get; }
    Task<PreviewChunkMesh> ReadAsync(int chunkId, CancellationToken token);
}
