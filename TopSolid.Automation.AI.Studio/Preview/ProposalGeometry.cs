using System.IO;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.AI.Studio.Preview;

/// <summary>Display-only tessellation of supported exact prepared arguments, never a CAD modification/simulation.</summary>
internal static class ProposalGeometry
{
    internal static bool Supports(JObject proposal) => (string?)proposal["toolName"] is "topsolid_create_cylinder" or "topsolid_create_extruded_rectangle";
    internal static PreviewScene Build(JObject proposal, CancellationToken token)
    {
        if (!Supports(proposal)) throw new InvalidDataException("No proposed geometry is available for this action.");
        var args = (JObject)proposal["arguments"]!;
        var scale = (string?)args["units"] switch { null or "mm" => 1d, "cm" => 10d, "m" => 1000d, _ => throw new InvalidDataException("Unknown proposal length units.") };
        var color = Color.FromRgb(255, 128, 0);
        if (args["color"] is JObject rgb && rgb["r"] != null && rgb["g"] != null && rgb["b"] != null)
            color = Color.FromRgb(checked((byte)(int)rgb["r"]!), checked((byte)(int)rgb["g"]!), checked((byte)(int)rgb["b"]!));
        var positions = new List<Point3D>(); var indices = new List<int>();
        if ((string?)proposal["toolName"] == "topsolid_create_cylinder")
        {
            var origin = Point(args["origin"], scale); var axis = Vector(args["axisDirection"], new Vector3D(0, 0, 1)); axis.Normalize();
            var radial = Math.Abs(axis.Z) < .9 ? new Vector3D(-axis.Y, axis.X, 0) : new Vector3D(axis.Z, 0, -axis.X); radial.Normalize();
            var tangent = Vector3D.CrossProduct(axis, radial); var radius = Positive((double)args["diameter"]! * scale / 2);
            var height = Positive((double?)proposal["target"]?["height"]?["currentValueSI"] * 1000 ?? (double?)args["height"] * scale ?? throw new InvalidDataException("Missing resolved cylinder height."));
            const int segments = 96;
            for (var i = 0; i < segments; i++)
            {
                var a = origin + radius * (Math.Cos(2 * Math.PI * i / segments) * radial + Math.Sin(2 * Math.PI * i / segments) * tangent);
                var b = origin + radius * (Math.Cos(2 * Math.PI * (i + 1) / segments) * radial + Math.Sin(2 * Math.PI * (i + 1) / segments) * tangent);
                // Separate cap vertices keep cap normals flat; shared side rings preserve smooth shading.
                positions.Add(a); positions.Add(a + height * axis);
                var n = i * 2; var next = (i + 1) % segments * 2;
                indices.AddRange([n, next, n + 1, n + 1, next, next + 1]);
            }
            var side = new PreviewMesh(positions.ToArray(), indices.ToArray(), color);
            positions.Clear(); indices.Clear();
            for (var i = 0; i < segments; i++)
            {
                var a = side.Positions[i * 2]; var b = side.Positions[(i + 1) % segments * 2];
                Triangle(origin, b, a); Triangle(origin + height * axis, a + height * axis, b + height * axis);
            }
            return PreviewScene.Build([side, new PreviewMesh(positions.ToArray(), indices.ToArray(), color)], token);
        }
        var corner = new Point3D(Number("x"), Number("y"), Number("z")); var x = new Vector3D(Positive(Number("width")), 0, 0);
        var y = new Vector3D(0, Positive(Number("height")), 0); var z = new Vector3D(0, 0, Positive(Number("depth")));
        Quad(corner, corner + y, corner + x + y, corner + x);
        Quad(corner + z, corner + x + z, corner + x + y + z, corner + y + z);
        Quad(corner, corner + x, corner + x + z, corner + z);
        Quad(corner + x, corner + x + y, corner + x + y + z, corner + x + z);
        Quad(corner + y, corner + y + z, corner + x + y + z, corner + x + y);
        Quad(corner, corner + z, corner + y + z, corner + y);
        return PreviewScene.Build([new PreviewMesh(positions.ToArray(), indices.ToArray(), color)], token);
        double Number(string key) => ((double?)args[key] ?? 0) * scale;
        void Triangle(Point3D a, Point3D b, Point3D c)
        { var start = positions.Count; positions.AddRange([a, b, c]); indices.AddRange([start, start + 1, start + 2]); }
        void Quad(Point3D a, Point3D b, Point3D c, Point3D d) { Triangle(a, b, c); Triangle(a, c, d); }
    }
    private static double Positive(double value) => value > 0 && double.IsFinite(value) ? value : throw new InvalidDataException("Invalid geometry dimension.");
    private static Point3D Point(JToken? value, double scale) => value == null ? new Point3D() : new Point3D((double)value["x"]! * scale, (double)value["y"]! * scale, (double)value["z"]! * scale);
    private static Vector3D Vector(JToken? value, Vector3D fallback)
    {
        var vector = value == null ? fallback : new Vector3D((double)value["x"]!, (double)value["y"]!, (double)value["z"]!);
        if (!double.IsFinite(vector.LengthSquared) || vector.LengthSquared < 1e-24) throw new InvalidDataException("Invalid geometry direction."); return vector;
    }
}
