using System;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal static class ContourGeometry
    {
        public static Point2D Point(JToken point, double scale) => new Point2D((double)point["x"] * scale, (double)point["y"] * scale);
        private static double Distance(Point2D a, Point2D b) => Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
        public static double Validate(JObject arguments)
        {
            MutationReferences.Validate(arguments);
            var scale = ModelingGeometry.Scale(arguments);
            if ((string)arguments["placement"] == "2d" && ((double?)arguments["z"] ?? 0) != 0) throw new ArgumentException("A 2D document cannot have a nonzero Z origin.");
            var contour = (JObject)arguments["contour"];
            var first = Point(contour["start"], scale); var start = first;
            double length = 0;
            foreach (var segment in (JArray)contour["segments"])
            {
                var end = Point(segment["end"], scale);
                if (Distance(start, end) < 1e-9) throw new ArgumentException("A contour segment must have distinct endpoints; use the circle tool for a full circle.");
                if ((string)segment["kind"] == "arc")
                {
                    if (segment["center"] == null || segment["clockwise"] == null) throw new ArgumentException("Every arc requires center and clockwise.");
                    var center = Point(segment["center"], scale);
                    var radius = Distance(start, center);
                    if (radius < 1e-9 || Math.Abs(radius - Distance(end, center)) > Math.Max(1e-9, radius * 1e-7))
                        throw new ArgumentException("Arc endpoints must lie on the same circle about its supplied center.");
                    var angle = Math.Atan2(end.Y-center.Y,end.X-center.X) - Math.Atan2(start.Y-center.Y,start.X-center.X);
                    if ((bool)segment["clockwise"]) angle = -angle;
                    if (angle <= 0) angle += 2*Math.PI;
                    length += radius * angle;
                }
                else
                {
                    if (segment["center"] != null || segment["clockwise"] != null) throw new ArgumentException("Line segments cannot specify arc center or clockwise.");
                    length += Distance(start, end);
                }
                start = end;
            }
            var closed = (bool)contour["closed"];
            if (closed != (Distance(first, start) < 1e-9)) throw new ArgumentException("For closed=true, the last endpoint must equal the start. For an open contour it must differ.");
            return length;
        }
    }
}
