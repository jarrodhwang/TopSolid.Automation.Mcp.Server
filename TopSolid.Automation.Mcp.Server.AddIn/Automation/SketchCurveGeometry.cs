using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    // Geometry is computed in the sketch's local frame, never in screen coordinates.
    // All native lengths are metres. This planner has no connection to TopSolid.
    internal sealed class SketchCurveGeometry
    {
        public string Kind;
        public Point2D[] Points;
        public Point2D Center;
        public double Radius;
        public bool Closed, Clockwise, Periodic;
        public int SegmentCount;
        public double ApproximationBound;
        public Point2D[] SlotCenters;

        public static SketchCurveGeometry Parse(JObject input, double scale)
        {
            var kind = (string)input["kind"];
            var fields = kind == "circle" ? new[] { "origin", "radius" }
                : kind == "rectangle" ? new[] { "origin", "width", "height" }
                : kind == "polyline" ? new[] { "points", "closed" }
                : kind == "line" ? new[] { "start", "end" }
                : kind == "arc" ? new[] { "start", "end", "center", "clockwise" }
                : kind == "bspline" ? new[] { "controlPoints", "periodic" }
                : kind == "parabola" ? new[] { "vertex", "focalLength", "startParameter", "endParameter", "rotationDegrees" }
                : kind == "star" ? new[] { "center", "outerRadius", "innerRadius", "pointCount", "rotationDegrees" }
                : kind == "ellipse" ? new[] { "center", "majorRadius", "minorRadius", "rotationDegrees", "tolerance" }
                : kind == "heart" ? new[] { "center", "width", "height", "rotationDegrees" }
                : kind == "slot" ? new[] { "center", "length", "width", "rotationDegrees" }
                : throw new ArgumentException("Unsupported sketch primitive. Use the exact registered kinds.");
            RequireInheritedUnits(input, scale);
            if (fields.Any(f => input[f] == null) || input.Properties().Any(p => p.Name != "kind" && p.Name != "units" && !fields.Contains(p.Name)))
                throw new ArgumentException("For " + kind + " supply exactly: " + string.Join(", ", fields) + ". Ask for missing dimensions; do not invent them.");
            Point2D Point(string key) => ContourGeometry.Point(input[key], scale);
            var result = new SketchCurveGeometry { Kind = kind, Points = new Point2D[0], SegmentCount = 1 };
            if (kind == "circle") { result.Center = Point("origin"); result.Radius = (double)input["radius"] * scale; result.Closed = true; }
            else if (kind == "rectangle")
            {
                var o = Point("origin"); var w = (double)input["width"] * scale; var h = (double)input["height"] * scale;
                result.Points = new[] { o, new Point2D(o.X + w, o.Y), new Point2D(o.X + w, o.Y + h), new Point2D(o.X, o.Y + h) };
                result.Closed = true; result.SegmentCount = 4;
            }
            else if (kind == "slot")
            {
                var length = (double)input["length"] * scale; var width = (double)input["width"] * scale;
                if (!(length > width && width > 0)) throw new ArgumentException("A straight slot requires overall length > width > 0. For length=width use a circle.");
                result.Center = Point("center"); result.Radius = width / 2; result.Closed = true; result.SegmentCount = 4;
                var a = (double)input["rotationDegrees"] * Math.PI / 180; var halfCenters = (length - width) / 2;
                Point2D Place(double x, double y) => new Point2D(result.Center.X + x * Math.Cos(a) - y * Math.Sin(a), result.Center.Y + x * Math.Sin(a) + y * Math.Cos(a));
                result.Points = new[] { Place(-halfCenters, -width / 2), Place(halfCenters, -width / 2), Place(halfCenters, width / 2), Place(-halfCenters, width / 2) };
                result.SlotCenters = new[] { Place(halfCenters, 0), Place(-halfCenters, 0) };
            }
            else if (kind == "heart")
            {
                // Six cubic Bezier spans with shared native endpoints. The extrema
                // are endpoints, giving exactly the requested unrotated width/height.
                // This is a defined decorative heart, not an inferred engineering curve.
                var w = (double)input["width"] * scale; var h = (double)input["height"] * scale;
                if (!(w > 0 && h > 0)) throw new ArgumentException("Heart width and height must be positive.");
                result.Center = Point("center"); result.Closed = true; result.SegmentCount = 6;
                var a = (double)input["rotationDegrees"] * Math.PI / 180;
                var net = new[] {
                    new Point2D(0, .2), new Point2D(.10, .4), new Point2D(.15, .5),
                    new Point2D(.25, .5), new Point2D(.4, .5), new Point2D(.5, .4),
                    new Point2D(.5, .2), new Point2D(.5, -.1), new Point2D(.15, -.35),
                    new Point2D(0, -.5), new Point2D(-.15, -.35), new Point2D(-.5, -.1),
                    new Point2D(-.5, .2), new Point2D(-.5, .4), new Point2D(-.4, .5),
                    new Point2D(-.25, .5), new Point2D(-.15, .5), new Point2D(-.10, .4) };
                result.Points = net.Select(p => new Point2D(result.Center.X + Math.Cos(a) * p.X * w - Math.Sin(a) * p.Y * h,
                    result.Center.Y + Math.Sin(a) * p.X * w + Math.Cos(a) * p.Y * h)).ToArray();
            }
            else if (kind == "star" || kind == "ellipse")
            {
                result.Center = Point("center"); result.Closed = true;
                var angle = (double)input["rotationDegrees"] * Math.PI / 180;
                Point2D Place(double x, double y) => new Point2D(result.Center.X + Math.Cos(angle) * x - Math.Sin(angle) * y,
                    result.Center.Y + Math.Sin(angle) * x + Math.Cos(angle) * y);
                if (kind == "star")
                {
                    var outer = (double)input["outerRadius"] * scale; var inner = (double)input["innerRadius"] * scale;
                    var tips = (int)input["pointCount"];
                    if (tips < 3 || tips > 64 || inner <= 0 || outer <= inner) throw new ArgumentException("A star requires 3..64 tips and 0 < innerRadius < outerRadius.");
                    result.Points = Enumerable.Range(0, 2 * tips).Select(i => {
                        var r = i % 2 == 0 ? outer : inner; var theta = i * Math.PI / tips;
                        return Place(r * Math.Cos(theta), r * Math.Sin(theta));
                    }).ToArray();
                    result.SegmentCount = result.Points.Length;
                }
                else
                {
                    var a = (double)input["majorRadius"] * scale; var b = (double)input["minorRadius"] * scale;
                    var tolerance = (double)input["tolerance"] * scale;
                    if (b <= 0 || a < b || tolerance <= 0) throw new ArgumentException("An ellipse requires majorRadius >= minorRadius > 0 and a positive tolerance.");
                    // Cubic Hermite interpolation of (a*cos(t), b*sin(t)). Each
                    // coordinate error <= max|f''''|*h^4/384. sqrt(2)*a is a
                    // conservative Euclidean bound, unchanged by rigid placement.
                    // TopSolid exposes no rational weights / analytic ellipse creator.
                    var count = Math.Max(8, 4 * (int)Math.Ceiling(2 * Math.PI * Math.Pow(Math.Sqrt(2) * a / (384 * tolerance), 0.25) / 4));
                    if (count > 128) throw new ArgumentException("Ellipse tolerance requires more than 128 cubic segments. Increase tolerance or reduce the radii.");
                    var h = 2 * Math.PI / count; var points = new List<Point2D>();
                    for (var i = 0; i < count; i++)
                    {
                        var t0 = i * h; var t1 = (i + 1) * h;
                        points.Add(Place(a * Math.Cos(t0), b * Math.Sin(t0)));
                        points.Add(Place(a * (Math.Cos(t0) - h * Math.Sin(t0) / 3), b * (Math.Sin(t0) + h * Math.Cos(t0) / 3)));
                        points.Add(Place(a * (Math.Cos(t1) + h * Math.Sin(t1) / 3), b * (Math.Sin(t1) - h * Math.Cos(t1) / 3)));
                    }
                    result.Points = points.ToArray(); result.SegmentCount = count;
                    result.ApproximationBound = Math.Sqrt(2) * a * Math.Pow(h, 4) / 384;
                }
            }
            else if (kind == "parabola")
            {
                // Local (t, t*t/(4*f)), rotated about the requested vertex.
                // Degree elevation of a quadratic Bezier gives four cubic control points.
                // Native readback must match this polynomial before the transaction can commit.
                var f = (double)input["focalLength"] * scale;
                var t0 = (double)input["startParameter"] * scale; var t1 = (double)input["endParameter"] * scale;
                if (Math.Abs(f) < 1e-9 || t1 - t0 < 1e-9) throw new ArgumentException("A parabola requires nonzero focalLength and startParameter < endParameter.");
                var dt = t1 - t0;
                var q0 = new Point2D(t0, t0 * t0 / (4 * f));
                var q1 = new Point2D(t0 + dt / 2, q0.Y + dt * t0 / (4 * f));
                var q2 = new Point2D(t1, t1 * t1 / (4 * f));
                var vertex = Point("vertex"); var angle = (double)input["rotationDegrees"] * Math.PI / 180;
                result.Points = new[] { q0, Blend(q0, q1, 2.0 / 3), Blend(q2, q1, 2.0 / 3), q2 }
                    .Select(p => new Point2D(vertex.X + Math.Cos(angle) * p.X - Math.Sin(angle) * p.Y,
                        vertex.Y + Math.Sin(angle) * p.X + Math.Cos(angle) * p.Y)).ToArray();
            }
            else if (kind == "bspline")
            {
                result.Points = ((JArray)input["controlPoints"]).Select(p => ContourGeometry.Point(p, scale)).ToArray();
                result.Periodic = (bool)input["periodic"]; result.Closed = result.Periodic;
                if (result.Points.Length < 4) throw new ArgumentException("A uniform cubic B-spline requires at least four control points (not interpolation points).");
            }
            else if (kind == "polyline")
            {
                result.Points = ((JArray)input["points"]).Select(p => ContourGeometry.Point(p, scale)).ToArray();
                result.Closed = (bool)input["closed"];
                if (result.Closed && result.Points.Length < 3) throw new ArgumentException("A closed polyline requires at least three points.");
                if (!result.Closed && result.Points.Length > 2 && Distance(result.Points[0], result.Points.Last()) < 1e-9)
                    throw new ArgumentException("This polyline returns to its start but closed=false would create disconnected endpoints. For a closed outline use closed=true and omit the repeated last point; for an open path use distinct endpoints.");
                result.SegmentCount = result.Points.Length - (result.Closed ? 0 : 1);
            }
            else
            {
                result.Points = new[] { Point("start"), Point("end") };
                if (kind == "arc")
                {
                    result.Center = Point("center"); result.Clockwise = (bool)input["clockwise"];
                    result.Radius = Distance(result.Points[0], result.Center);
                    if (result.Radius < 1e-9 || Math.Abs(Distance(result.Points[1], result.Center) - result.Radius) > Math.Max(1e-9, result.Radius * 1e-8))
                        throw new ArgumentException("An arc's start and end must be the same distance from its center.");
                }
            }
            foreach (var p in result.Points.Concat(new[] { result.Center }))
                if (double.IsNaN(p.X) || double.IsInfinity(p.X) || double.IsNaN(p.Y) || double.IsInfinity(p.Y) || Math.Abs(p.X) > 1000000 || Math.Abs(p.Y) > 1000000)
                    throw new ArgumentException("Computed sketch geometry exceeds the supported finite coordinate range.");
            for (var i = 1; i < result.Points.Length; i++)
                if (Distance(result.Points[i - 1], result.Points[i]) < 1e-9) throw new ArgumentException("Consecutive sketch points must be distinct.");
            if (result.Closed && result.Points.Length > 0 && Distance(result.Points[0], result.Points.Last()) < 1e-9)
                throw new ArgumentException("Do not repeat the first point; closed/periodic geometry adds closure.");
            return result;
        }
        internal static void RequireInheritedUnits(JObject input, double scale)
        {
            if (input["units"] == null) return;
            var units = (string)input["units"];
            if ((units != "mm" && units != "cm" && units != "m") || ModelingGeometry.Scale(input) != scale)
                throw new ArgumentException("Nested units must match the request's root units (default mm). Put units once at the root; mixed units are not inferred.");
        }
        internal Point2D[] CubicSegment(int index) => Enumerable.Range(0, 4).Select(j => Points[(3 * index + j) % Points.Length]).ToArray();
        internal static Point2D Blend(Point2D a, Point2D b, double t) => new Point2D(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        internal static double Distance(Point2D a, Point2D b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        internal static Point2D Cubic(Point2D[] p, double t)
        {
            var q0 = Blend(p[0], p[1], t); var q1 = Blend(p[1], p[2], t); var q2 = Blend(p[2], p[3], t);
            return Blend(Blend(q0, q1, t), Blend(q1, q2, t), t);
        }
    }

    internal static class SketchTopology
    {
        // The reported session rejects CreateProfile for open chains. Open drawing
        // segments are valid native topology; never invent a closing edge to obtain a profile.
        public static ElementItemId ClosedProfile(List<ElementItemId> segments, bool closed, Func<List<ElementItemId>, ElementItemId> create)
            => closed ? create(segments) : ElementItemId.Empty;
    }
}
