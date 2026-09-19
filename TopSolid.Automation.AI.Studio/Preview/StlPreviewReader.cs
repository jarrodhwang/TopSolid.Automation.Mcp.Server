using System.Buffers.Binary;
using System.IO;
using System.Windows.Media.Media3D;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Preview;

/// <summary>Strict binary STL from the native exporter, explicitly requested in millimetres/Z-up.</summary>
internal static class StlPreviewReader
{
    internal const int MaximumBytes = GraphicPreviewQuality.MaximumStlBytes;
    internal static PreviewScene Read(byte[] bytes, CancellationToken token)
    {
        if (bytes.Length is < 84 or > MaximumBytes) throw new InvalidDataException("Invalid STL size.");
        var triangles = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(80, 4));
        if (triangles == 0 || triangles > PreviewScene.MaximumTriangles || 84L + triangles * 50L != bytes.Length)
            throw new InvalidDataException("Invalid binary STL triangle count.");
        const int batchTriangles = 32000;
        var meshes = new PreviewMesh[(triangles + batchTriangles - 1) / batchTriangles];
        try
        {
            Parallel.For(0, meshes.Length, new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = PreviewScene.WorkerCount }, batch =>
                meshes[batch] = ReadMesh(batch * batchTriangles, Math.Min(batchTriangles, (int)triangles - batch * batchTriangles)));
        }
        catch (AggregateException error) when (error.Flatten().InnerExceptions.All(e => e is InvalidDataException))
        { throw new InvalidDataException("Invalid STL coordinates.", error); }
        return PreviewScene.Build(meshes, token);

        PreviewMesh ReadMesh(int startTriangle, int count)
        {
        var points = new Point3D[checked(count * 3)]; var faceNormals = new Vector3D[count];
        var adjacency = new Dictionary<Point3D, List<Vector3D>>();
        for (var t = 0; t < count; t++)
        {
            if (t % 1000 == 0) token.ThrowIfCancellationRequested();
            for (var v = 0; v < 3; v++)
            {
                var offset = 84 + (startTriangle + t) * 50 + 12 + v * 12;
                points[t * 3 + v] = new Point3D(Number(offset), Number(offset + 4), Number(offset + 8));
            }
            // Trust vertex winding, not unchecked per-facet normal/color extensions.
            var normal = Vector3D.CrossProduct(points[t * 3 + 1] - points[t * 3], points[t * 3 + 2] - points[t * 3]);
            if (normal.LengthSquared > 1e-24) normal.Normalize();
            faceNormals[t] = normal;
            for (var v = 0; v < 3; v++)
            {
                var p = points[t * 3 + v];
                if (!adjacency.TryGetValue(p, out var nearby)) adjacency[p] = nearby = [];
                // Avoid adversarial quadratic smoothing at high-valence vertices.
                if (nearby.Count < 32) nearby.Add(normal);
            }
        }
        var normals = new Vector3D[points.Length]; var threshold = Math.Cos(Math.PI / 9);
        for (var i = 0; i < points.Length; i++)
        {
            if (i % 3000 == 0) token.ThrowIfCancellationRequested();
            var face = faceNormals[i / 3]; var smooth = new Vector3D();
            foreach (var neighbor in adjacency[points[i]]) if (Vector3D.DotProduct(face, neighbor) >= threshold) smooth += neighbor;
            if (smooth.LengthSquared > 1e-24) smooth.Normalize(); else smooth = face;
            normals[i] = smooth;
        }
        return new PreviewMesh(points, Enumerable.Range(0, points.Length).ToArray(), PreviewQuality.DefaultColor, normals);
        }

        double Number(int offset)
        {
            var value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4)));
            if (!float.IsFinite(value) || Math.Abs(value) > 1e9) throw new InvalidDataException("Invalid STL coordinate.");
            return value;
        }
    }
}
