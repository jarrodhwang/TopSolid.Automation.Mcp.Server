using System.IO;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Preview;

internal sealed record PreviewMesh(Point3D[] Positions, int[] Indices, Color Color, Vector3D[]? Normals = null);
internal readonly record struct PreviewEdge(Point3D A, Point3D B, Vector3D Normal, Vector3D OtherNormal, bool Feature);

/// <summary>Immutable display geometry in millimetres. Surfaces and adjacency are prepared once, off the UI thread.</summary>
internal sealed record PreviewScene(Model3DGroup Surfaces, Model3DGroup Edges, Rect3D Bounds, int Triangles, bool EdgesOmitted)
{
    internal const int MaximumTriangles = GraphicPreviewQuality.MaximumTriangles;
    internal const int MaximumEdges = 10_000;
    internal static int WorkerCount => Math.Clamp(Environment.ProcessorCount - 1, 1, 4);
    internal GpuMesh[] GpuMeshes { get; private init; } = [];
    internal HelixToolkit.SharpDX.LineGeometry3D? GpuEdges { get; private init; }
    private PreviewEdge[] edgeCandidates = [];
    internal PreviewScene Recolor(Color color)
    {
        var brush = new SolidColorBrush(color); brush.Freeze();
        var material = new DiffuseMaterial(brush); material.Freeze();
        var surfaces = new Model3DGroup();
        foreach (var model in Surfaces.Children.OfType<GeometryModel3D>())
        {
            var painted = new GeometryModel3D(model.Geometry, material) { BackMaterial = material }; painted.Freeze(); surfaces.Children.Add(painted);
        }
        surfaces.Freeze();
        return this with { Surfaces = surfaces, GpuMeshes = GpuMeshes.Select(mesh => mesh with { Color = color }).ToArray() };
    }
    internal PreviewScene AsStockOverlay()
    {
        // Keep the native facets exact; duplicate stock edges obscure the target.
        var empty = new Model3DGroup(); empty.Freeze();
        return this with { Edges = empty, edgeCandidates = [], GpuEdges = null };
    }
    internal static PreviewScene Empty(Rect3D bounds)
    {
        var empty = new Model3DGroup(); empty.Freeze();
        return new(empty, empty, bounds, 0, false);
    }
    internal static PreviewScene Combine(params PreviewScene?[] sources)
    {
        var scenes = sources.OfType<PreviewScene>().ToArray();
        var count = scenes.Sum(s => s.Triangles);
        if (count == 0 || count > MaximumTriangles) throw new InvalidDataException("Invalid combined preview size.");
        var surfaces = new Model3DGroup(); var bounds = Rect3D.Empty;
        foreach (var scene in scenes) { surfaces.Children.Add(scene.Surfaces); bounds.Union(scene.Bounds); }
        surfaces.Freeze(); var empty = new Model3DGroup(); empty.Freeze();
        var candidates = scenes.SelectMany(s => s.edgeCandidates).OrderByDescending(e => e.Feature).Take(MaximumEdges).ToArray();
        return new PreviewScene(surfaces, empty, bounds, count, scenes.Any(s => s.EdgesOmitted))
        {
            GpuMeshes = scenes.SelectMany(s => s.GpuMeshes).ToArray(), edgeCandidates = candidates,
            GpuEdges = ToolpathPreviewScene.Lines(candidates.Where(e => e.Feature).Select(e => (e.A, e.B)))
        };
    }
    internal static PreviewScene Build(IReadOnlyList<PreviewMesh> meshes, CancellationToken token)
    {
        var surfaces = new Model3DGroup(); var edges = new Model3DGroup(); var bounds = Rect3D.Empty;
        var count = 0; var vertices = 0; var omitted = false;
        foreach (var mesh in meshes)
        {
            token.ThrowIfCancellationRequested();
            count = checked(count + mesh.Indices.Length / 3);
            vertices = checked(vertices + mesh.Positions.Length);
            if (vertices > MaximumTriangles * 3 || count > MaximumTriangles || mesh.Indices.Length % 3 != 0) throw new InvalidDataException("Preview geometry is too large.");
            foreach (var p in mesh.Positions)
            {
                if (!double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Z) || Math.Max(Math.Abs(p.X), Math.Max(Math.Abs(p.Y), Math.Abs(p.Z))) > 1e9)
                    throw new InvalidDataException("Invalid preview coordinate.");
                bounds.Union(p);
            }
        }
        if (bounds.IsEmpty || count == 0) throw new InvalidDataException("No displayable solid geometry.");
        // Small frozen batches avoid enormous single WPF meshes and per-frame vertex work.
        var edgeSegments = new List<PreviewEdge>();
        var groups = new Model3DGroup[meshes.Count];
        var edgeBatches = new PreviewEdge[meshes.Count][];
        var gpuMeshes = new GpuMesh[meshes.Count];
        Parallel.For(0, meshes.Count, new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = WorkerCount }, meshIndex =>
        {
            var source = meshes[meshIndex]; var group = new Model3DGroup();
            token.ThrowIfCancellationRequested();
            var brush = new SolidColorBrush(source.Color); brush.Freeze();
            var material = new MaterialGroup(); material.Children.Add(new DiffuseMaterial(brush));
            material.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(70, 70, 70)), 36)); material.Freeze();
            for (var offset = 0; offset < source.Indices.Length; offset += 18000)
            {
                var positions = new Point3DCollection(); var indices = new Int32Collection(); var normals = new Vector3DCollection();
                var map = new Dictionary<int, int>();
                for (var i = offset; i < Math.Min(offset + 18000, source.Indices.Length); i++)
                {
                    var index = source.Indices[i];
                    if ((uint)index >= source.Positions.Length) throw new InvalidDataException("Preview index outside mesh.");
                    if (!map.TryGetValue(index, out var mapped))
                    {
                        mapped = positions.Count; map.Add(index, mapped); positions.Add(source.Positions[index]);
                        if (source.Normals != null) normals.Add(source.Normals[index]);
                    }
                    indices.Add(mapped);
                }
                var geometry = new MeshGeometry3D { Positions = positions, TriangleIndices = indices };
                if (normals.Count > 0) geometry.Normals = normals;
                geometry.Freeze();
                var model = new GeometryModel3D(geometry, material) { BackMaterial = material }; model.Freeze(); group.Children.Add(model);
            }
            group.Freeze(); groups[meshIndex] = group;
            // Adjacency can cost much more RAM than the surfaces; large documents use shaded GPU surfaces.
            edgeBatches[meshIndex] = count <= 250000 ? FeatureEdges(source, token).ToArray() : [];
            gpuMeshes[meshIndex] = GpuMesh.Build(source, token);
        });
        foreach (var group in groups) foreach (var child in group.Children) surfaces.Children.Add(child);
        foreach (var batch in edgeBatches) edgeSegments.AddRange(batch);
        omitted = count > 250000 || edgeSegments.Count(e => e.Feature) > MaximumEdges;
        surfaces.Freeze(); edges.Freeze();
        var scene = new PreviewScene(surfaces, edges, bounds, count, omitted)
        { edgeCandidates = edgeSegments.OrderByDescending(e => e.Feature).ToArray(), GpuMeshes = gpuMeshes,
            GpuEdges = ToolpathPreviewScene.Lines(edgeSegments.Where(e => e.Feature).Take(MaximumEdges).Select(e => (e.A, e.B))) };
        var extent = new Vector3D(bounds.SizeX, bounds.SizeY, bounds.SizeZ).Length;
        return scene with { Edges = scene.CreateEdges(new Vector3D(1, -1, 1), Math.Max(1e-8, extent / 400)) };
    }

    private static IEnumerable<PreviewEdge> FeatureEdges(PreviewMesh mesh, CancellationToken token)
    {
        var normals = new Dictionary<(Point3D, Point3D), (Vector3D Normal, Vector3D Other, bool Crease, int Count)>();
        for (var i = 0; i < mesh.Indices.Length; i += 3)
        {
            if (i % 3000 == 0) token.ThrowIfCancellationRequested();
            var a = mesh.Positions[mesh.Indices[i]]; var b = mesh.Positions[mesh.Indices[i + 1]]; var c = mesh.Positions[mesh.Indices[i + 2]];
            var normal = Vector3D.CrossProduct(b - a, c - a); if (normal.LengthSquared < 1e-24) continue; normal.Normalize();
            Add(a, b, normal); Add(b, c, normal); Add(c, a, normal);
        }
        return normals.Where(p => p.Value.Count == 1 || p.Value.Crease || Vector3D.DotProduct(p.Value.Normal, p.Value.Other) < .999999)
            .Select(p => new PreviewEdge(p.Key.Item1, p.Key.Item2, p.Value.Normal, p.Value.Other, p.Value.Count != 2 || p.Value.Crease));
        void Add(Point3D a, Point3D b, Vector3D n)
        {
            var key = Compare(a, b) < 0 ? (a, b) : (b, a);
            if (normals.TryGetValue(key, out var entry)) normals[key] = (entry.Normal, n, entry.Crease || Vector3D.DotProduct(entry.Normal, n) < Math.Cos(Math.PI / 9), entry.Count + 1);
            else normals.Add(key, (n, n, false, 1));
        }
        static int Compare(Point3D a, Point3D b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z.CompareTo(b.Z);
    }

    /// <summary>Depth-tested black ribbons, one screen pixel wide. Only this bounded edge batch changes with the camera.</summary>
    internal Model3DGroup CreateEdges(Vector3D towardCamera, double unitsPerPixel)
    {
        var result = new Model3DGroup();
        if (!double.IsFinite(unitsPerPixel) || unitsPerPixel <= 0 || towardCamera.LengthSquared < 1e-24) { result.Freeze(); return result; }
        towardCamera.Normalize();
        var material = new DiffuseMaterial(Brushes.Black); material.Freeze();
        var mesh = new MeshGeometry3D(); var count = 0;
        var seen = new HashSet<(Point3D, Point3D)>();
        foreach (var edge in edgeCandidates)
        {
            if (!edge.Feature && Vector3D.DotProduct(edge.Normal, towardCamera) * Vector3D.DotProduct(edge.OtherNormal, towardCamera) >= 0) continue;
            if (!seen.Add((edge.A, edge.B))) continue;
            var side = Vector3D.CrossProduct(edge.B - edge.A, towardCamera);
            if (side.LengthSquared < 1e-24) continue;
            side.Normalize(); side *= unitsPerPixel * PreviewQuality.EdgeWidth / 2;
            var bias = towardCamera * unitsPerPixel * .08;
            var a = edge.A + bias; var b = edge.B + bias; var start = mesh.Positions.Count;
            mesh.Positions.Add(a - side); mesh.Positions.Add(a + side); mesh.Positions.Add(b - side); mesh.Positions.Add(b + side);
            foreach (var index in new[] { 0, 1, 2, 2, 1, 3 }) mesh.TriangleIndices.Add(start + index);
            count++;
            if (count % 1500 == 0) Flush();
            if (count >= MaximumEdges) break;
        }
        Flush(); result.Freeze(); return result;
        void Flush()
        {
            if (mesh.Positions.Count == 0) return;
            mesh.Freeze(); var model = new GeometryModel3D(mesh, material) { BackMaterial = material }; model.Freeze(); result.Children.Add(model);
            mesh = new MeshGeometry3D();
        }
    }
}
