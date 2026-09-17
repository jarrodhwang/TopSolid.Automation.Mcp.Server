using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    // Pure geometry validation/conversion: safe to run before requesting approval.
    internal static class ModelingGeometry
    {
        public static double Scale(JObject arguments) => (string)arguments["units"] == "m" ? 1.0 : (string)arguments["units"] == "cm" ? 0.01 : 0.001;
        public static Plane3D Plane(string placement, Point3D origin) => new Plane3D(origin,
            placement == "yz" ? new Direction3D(0, 1, 0) : new Direction3D(1, 0, 0),
            placement == "xy" ? new Direction3D(0, 1, 0) : new Direction3D(0, 0, 1));
        public static void Validate(string operation, JObject arguments)
        {
            var scale = Scale(arguments);
            if ((string)arguments["placement"] == "2d" && ((double?)arguments["z"] ?? 0) != 0) throw new ArgumentException("A 2D document requires z=0.");
            if (operation != "polyline3d") return;
            var points = ((JArray)arguments["points"]).Select(v => new Point3D((double)v["x"] * scale, (double)v["y"] * scale, (double)v["z"] * scale)).ToArray();
            var closed = (bool?)arguments["closed"] ?? false;
            if (closed && points.Length < 3) throw new ArgumentException("A closed polyline requires at least three points.");
            for (var i = 1; i < points.Length; i++)
                if (DistanceSquared(points[i], points[i - 1]) < 1e-18) throw new ArgumentException("Consecutive polyline points must be distinct.");
            if (closed && DistanceSquared(points[0], points[points.Length - 1]) < 1e-18) throw new ArgumentException("Do not repeat the first point; closed=true adds the closing segment.");
        }
        private static double DistanceSquared(Point3D a, Point3D b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z);
    }
}
