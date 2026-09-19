using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Newtonsoft.Json.Linq;
using D = HelixToolkit.SharpDX;

namespace TopSolid.Automation.AI.Studio.Preview;

internal sealed record ToolpathPreviewScene(D.LineGeometry3D Geometry, int Segments, bool Partial)
{
    internal const int MaximumWpfSegments = 10000;
    internal static ToolpathPreviewScene Read(JObject data, CancellationToken token)
    {
        var count = (int?)data["segments"] ?? 0;
        var encoded = (string?)data["data"];
        if ((string?)data["format"] != "segments-f32" || (string?)data["units"] != "mm" || (string?)data["upAxis"] != "Z" ||
            count is < 1 or > 100000 || encoded == null || encoded.Length != count * 32) throw new InvalidDataException("Invalid toolpath payload.");
        var bytes = Convert.FromBase64String(encoded);
        if (bytes.Length != count * 24) throw new InvalidDataException("Invalid toolpath size.");
        var segments = new (Point3D, Point3D)[count];
        for (var i = 0; i < count; i++)
        { if (i % 2048 == 0) token.ThrowIfCancellationRequested(); segments[i] = (Point(i * 24), Point(i * 24 + 12)); }
        var geometry = Lines(segments);
        var roles = data["motionRoles"] as JArray;
        if (data["motionRoles"] != null && (roles == null || roles.Count != count)) throw new InvalidDataException("Invalid toolpath color roles.");
        geometry.Colors = new HelixToolkit.Color4Collection(count * 2);
        for (var i = 0; i < count; i++)
        {
            var role = ToolpathColorRole.Unknown;
            if (roles != null && (roles[i].Type != JTokenType.String || !Enum.TryParse((string?)roles[i], out role) || !Enum.IsDefined(role)))
                throw new InvalidDataException("Unknown toolpath color role.");
            var color = TopSolidPreviewPalette.Toolpath(role);
            var rgba = new HelixToolkit.Maths.Color4(color.R / 255f, color.G / 255f, color.B / 255f, 1);
            geometry.Colors.Add(rgba); geometry.Colors.Add(rgba);
        }
        return new(geometry, count, (bool?)data["partial"] == true || (bool?)data["upToDate"] == false);
        Point3D Point(int offset) => new(Number(offset), Number(offset + 4), Number(offset + 8));
        float Number(int offset)
        { var value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4))); if (!float.IsFinite(value) || Math.Abs(value) > 1e9) throw new InvalidDataException("Invalid toolpath coordinate."); return value; }
    }
    internal static D.LineGeometry3D Lines(IEnumerable<(Point3D A, Point3D B)> lines)
    {
        var positions = new HelixToolkit.Vector3Collection(); var indices = new HelixToolkit.IntCollection();
        foreach (var (a, b) in lines)
        {
            indices.Add(positions.Count); positions.Add(new Vector3((float)a.X, (float)a.Y, (float)a.Z));
            indices.Add(positions.Count); positions.Add(new Vector3((float)b.X, (float)b.Y, (float)b.Z));
        }
        return new D.LineGeometry3D { Positions = positions, Indices = indices };
    }
    internal Model3DGroup Ribbons(Vector3D camera, double scale)
    {
        var result = new Model3DGroup(); var batches = new Dictionary<Color, MeshGeometry3D>();
        // Bounded compatibility rendering; Direct3D 11 handles the complete line buffer in one draw.
        for (var i = 0; Geometry.Positions != null && i + 1 < Geometry.Positions.Count && i < MaximumWpfSegments * 2; i += 2)
        {
            var a = Point(Geometry.Positions[i]); var b = Point(Geometry.Positions[i + 1]);
            var side = Vector3D.CrossProduct(b - a, camera); if (side.LengthSquared < 1e-24) continue;
            side.Normalize(); side *= scale * .8;
            var color = TopSolidPreviewPalette.Toolpath(ToolpathColorRole.Unknown);
            if (Geometry.Colors is { } colors && i < colors.Count)
                color = Color.FromRgb((byte)Math.Round(colors[i].Red * 255), (byte)Math.Round(colors[i].Green * 255), (byte)Math.Round(colors[i].Blue * 255));
            if (!batches.TryGetValue(color, out var mesh)) batches[color] = mesh = new MeshGeometry3D();
            var start = mesh.Positions.Count;
            mesh.Positions.Add(a - side); mesh.Positions.Add(a + side); mesh.Positions.Add(b - side); mesh.Positions.Add(b + side);
            foreach (var index in new[] { 0, 1, 2, 2, 1, 3 }) mesh.TriangleIndices.Add(start + index);
        }
        foreach (var (color, mesh) in batches)
        {
            mesh.Freeze(); var brush = new SolidColorBrush(color); brush.Freeze(); var material = new EmissiveMaterial(brush); material.Freeze();
            var model = new GeometryModel3D(mesh, material) { BackMaterial = material }; model.Freeze(); result.Children.Add(model);
        }
        result.Freeze(); return result;
        static Point3D Point(Vector3 p) => new(p.X, p.Y, p.Z);
    }
}
