using System.IO;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace TopSolid.Automation.AI.Studio.Preview;

internal sealed record PreviewMesh(Point3D[] Positions, int[] Indices, Color Color, Vector3D[]? Normals = null);

/// <summary>Immutable display geometry in millimetres. All expensive work is done once, off the UI thread.</summary>
internal sealed record PreviewScene(Model3DGroup Surfaces, Model3DGroup Edges, Rect3D Bounds, int Triangles, bool EdgesOmitted)
{
    internal const int MaximumTriangles = 100_000;
    internal static PreviewScene Build(IReadOnlyList<PreviewMesh> meshes, CancellationToken token)
    {
        var surfaces = new Model3DGroup(); var edges = new Model3DGroup(); var bounds = Rect3D.Empty;
        var count = 0; var vertices = 0; var omitted = false;
        foreach (var mesh in meshes)
        {
            token.ThrowIfCancellationRequested();
            count = checked(count + mesh.Indices.Length / 3);
            vertices = checked(vertices + mesh.Positions.Length);
            if (vertices > 300000 || count > MaximumTriangles || mesh.Indices.Length % 3 != 0) throw new InvalidDataException("Preview geometry is too large.");
            foreach (var p in mesh.Positions)
            {
                if (!double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Z) || Math.Max(Math.Abs(p.X), Math.Max(Math.Abs(p.Y), Math.Abs(p.Z))) > 1e9)
                    throw new InvalidDataException("Invalid preview coordinate.");
                bounds.Union(p);
            }
        }
        if (bounds.IsEmpty || count == 0) throw new InvalidDataException("No displayable solid geometry.");
        var radius = Math.Max(1e-7, new Vector3D(bounds.SizeX, bounds.SizeY, bounds.SizeZ).Length / 1000);
        // Small frozen batches avoid enormous single WPF meshes and per-frame vertex work.
        var edgeSegments = new List<(Point3D A, Point3D B)>();
        foreach (var source in meshes)
        {
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
                var model = new GeometryModel3D(geometry, material) { BackMaterial = material }; model.Freeze(); surfaces.Children.Add(model);
            }
            if (!omitted)
            {
                foreach (var segment in FeatureEdges(source, token))
                {
                    edgeSegments.Add(segment);
                    if (edgeSegments.Count > 10000) { omitted = true; edgeSegments.Clear(); break; }
                }
            }
        }
        if (!omitted)
        {
            var material = new DiffuseMaterial(Brushes.Black); material.Freeze();
            for (var start = 0; start < edgeSegments.Count; start += 1500)
            {
                var mesh = new MeshGeometry3D();
                foreach (var segment in edgeSegments.Skip(start).Take(1500)) AddLine(mesh, segment.A, segment.B, radius);
                mesh.Freeze(); var model = new GeometryModel3D(mesh, material) { BackMaterial = material }; model.Freeze(); edges.Children.Add(model);
            }
        }
        surfaces.Freeze(); edges.Freeze();
        return new PreviewScene(surfaces, edges, bounds, count, omitted);
    }

    private static IEnumerable<(Point3D, Point3D)> FeatureEdges(PreviewMesh mesh, CancellationToken token)
    {
        var normals = new Dictionary<(Point3D, Point3D), (Vector3D Normal, bool Crease, int Count)>();
        for (var i = 0; i < mesh.Indices.Length; i += 3)
        {
            if (i % 3000 == 0) token.ThrowIfCancellationRequested();
            var a = mesh.Positions[mesh.Indices[i]]; var b = mesh.Positions[mesh.Indices[i + 1]]; var c = mesh.Positions[mesh.Indices[i + 2]];
            var normal = Vector3D.CrossProduct(b - a, c - a); if (normal.LengthSquared < 1e-24) continue; normal.Normalize();
            Add(a, b, normal); Add(b, c, normal); Add(c, a, normal);
        }
        return normals.Where(p => p.Value.Count == 1 || p.Value.Crease).Select(p => p.Key);
        void Add(Point3D a, Point3D b, Vector3D n)
        {
            var key = Compare(a, b) < 0 ? (a, b) : (b, a);
            if (normals.TryGetValue(key, out var entry)) normals[key] = (entry.Normal, entry.Crease || Vector3D.DotProduct(entry.Normal, n) < .85, entry.Count + 1);
            else normals.Add(key, (n, false, 1));
        }
        static int Compare(Point3D a, Point3D b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z.CompareTo(b.Z);
    }

    internal static void AddLine(MeshGeometry3D mesh, Point3D a, Point3D b, double radius)
    {
        var direction = b - a; if (direction.LengthSquared < 1e-24) return; direction.Normalize();
        var side = Vector3D.CrossProduct(direction, Math.Abs(direction.Z) < .9 ? new Vector3D(0, 0, 1) : new Vector3D(0, 1, 0)); side.Normalize();
        var up = Vector3D.CrossProduct(direction, side); var start = mesh.Positions.Count;
        for (var i = 0; i < 3; i++)
        {
            var offset = radius * (Math.Cos(i * Math.PI * 2 / 3) * side + Math.Sin(i * Math.PI * 2 / 3) * up);
            mesh.Positions.Add(a + offset); mesh.Positions.Add(b + offset);
        }
        for (var i = 0; i < 3; i++)
        {
            var x = start + i * 2; var y = start + (i + 1) % 3 * 2;
            foreach (var index in new[] { x, y, x + 1, x + 1, y, y + 1 }) mesh.TriangleIndices.Add(index);
        }
    }
}
