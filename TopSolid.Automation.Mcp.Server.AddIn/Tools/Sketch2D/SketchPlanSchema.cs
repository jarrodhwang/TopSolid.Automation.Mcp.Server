using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class SketchPlanSchema
    {
        internal static JObject Point2() => Schema.Object(new JObject { ["x"] = Schema.Number("Local sketch X."), ["y"] = Schema.Number("Local sketch Y.") }, "x", "y");
        internal static JObject Point3(string description) { var p = Schema.Object(new JObject { ["x"] = Schema.Number("X."), ["y"] = Schema.Number("Y."), ["z"] = Schema.Number("Z.") }, "x", "y", "z"); p["description"] = description; return p; }
        internal static void Placement(JObject p)
        {
            p["sketch"] = Schema.Element();
            p["placement"] = Schema.Choice("New sketch: 2d, a principal 3D plane, explicit frame, or reference sketch. Omit all placement options when appending to sketch.", "2d", "xy", "xz", "yz", "frame", "reference");
            p["documentSpace"] = Schema.Choice("Containing document dimension. Parts contain 2D sketches but documentSpace=3d. Required for frame/reference; recommended when appending.", "2d", "3d");
            p["name"] = Schema.Text(CreationNames.Description + " New sketch only.", 128);
            p["color"] = AppearanceTools.ColorSchema();
            p["origin"] = Point3("Default zero. Principal placement: WORLD origin. Reference placement: offset in reference local X/Y/normal from its origin or anchor. Lengths use input units.");
            p["rotationDegrees"] = Schema.Number("In-plane rotation from placement/reference X toward Y. Default 0.", -360, 360);
            p["frame"] = Schema.Object(new JObject { ["origin"] = Point3("World origin in input units."), ["xDirection"] = Point3("World unit X direction."), ["yDirection"] = Point3("Orthogonal world unit Y direction.") }, "origin", "xDirection", "yDirection");
            p["referenceSketch"] = Schema.Element();
            p["referenceMode"] = Schema.Choice("Default associative: follow the reference plane, anchor and axes through native Smart objects. Requires anchor, zero normal offset and rotation in multiples of 90 degrees. snapshot only if the user explicitly wants fixed placement. Local dimensions do not automatically copy referenced curve dimensions.", "associative", "snapshot");
            p["referenceMode"]["default"] = "associative";
            p["anchor"] = Schema.Object(new JObject { ["item"] = Schema.Item(), ["location"] = Schema.Choice("From referenceSketch: vertex; segment start/end; circular center; midParameter (parameter midpoint, not generally half arc length). Resolved by server, never guessed.", "vertex", "start", "end", "center", "midParameter") }, "item", "location");
        }
        internal static JObject Profiles() => Schema.Array(Schema.Object(new JObject {
            ["kind"] = Schema.Choice("Supply ONLY fields for kind: circle(origin,radius); rectangle(origin,width,height); slot(center,length,width,rotationDegrees); polyline(points,closed); line(start,end); arc(start,end,center,clockwise); bspline(controlPoints,periodic); parabola(vertex,focalLength,startParameter,endParameter,rotationDegrees); star(center,outerRadius,innerRadius,pointCount,rotationDegrees); ellipse(center,majorRadius,minorRadius,rotationDegrees,tolerance); heart(center,width,height,rotationDegrees). For closed boundaries use ONE rectangle/polyline/contour, not independent lines. Slot has two analytic semicircles and two tangent lines. Ellipse is a bounded cubic approximation.", "circle", "rectangle", "slot", "polyline", "line", "arc", "bspline", "parabola", "star", "ellipse", "heart"),
            ["units"] = Schema.Choice("Optional redundant units; must equal the root units. Prefer units once at the request root.", "mm", "cm", "m"),
            ["origin"] = Point2(), ["radius"] = Schema.Number("Circle radius in input units.", 0.001, 100000), ["width"] = Schema.Number("Rectangle/heart/slot width.", 0.001, 100000), ["height"] = Schema.Number("Rectangle/heart height.", 0.001, 100000),
            ["length"] = Schema.Number("Slot overall end-to-end length, strictly greater than width. Center spacing = length - width.", .001, 100000),
            ["points"] = Schema.Array(Point2(), 2, 128), ["closed"] = Schema.Boolean("Polyline closure; omit repeated final start point."),
            ["start"] = Point2(), ["end"] = Point2(), ["center"] = Point2(), ["clockwise"] = Schema.Boolean("Arc direction in local sketch X/Y."),
            ["controlPoints"] = Schema.Array(Point2(), 4, 128), ["periodic"] = Schema.Boolean("Uniform cubic B-spline; points are controls, not interpolation points. Omit repeated first control."),
            ["vertex"] = Point2(), ["focalLength"] = Schema.Number("Signed nonzero focal distance f. Before rotation: x=t, y=t^2/(4f), measured from vertex."),
            ["startParameter"] = Schema.Number("Start x/t in input length units, less than endParameter."), ["endParameter"] = Schema.Number("End x/t in input length units."),
            ["outerRadius"] = Schema.Number("Star tip radius, larger than innerRadius.", 0.001, 100000),
            ["innerRadius"] = Schema.Number("Star valley radius.", 0.001, 100000),
            ["pointCount"] = Schema.Integer("Number of star tips; generates twice as many line segments.", 3, 64),
            ["majorRadius"] = Schema.Number("Ellipse semimajor radius along local X before rotation, at least minorRadius.", 0.001, 100000),
            ["minorRadius"] = Schema.Number("Ellipse semiminor radius along local Y before rotation.", 0.001, 100000),
            ["tolerance"] = Schema.Number("Required maximum ellipse approximation deviation in input length units, disclosed in preview. Server bounds error and work; not an exact conic.", 0.000001, 1000),
            ["rotationDegrees"] = Schema.Number("Required shape rotation: parabola about vertex (0 opens +Y for positive f); star first tip or ellipse major axis angle from local +X. Ask unless user delegates sample values.", -360, 360)
        }, "kind"), 1, 32);

        internal static int ValidateProfiles(JObject p, double scale)
        {
            SketchPlacement.Validate(p);
            ElementAppearance.Validate(p);
            var curves = ((JArray)p["profiles"]).Cast<JObject>().Select(q => SketchCurveGeometry.Parse(q, scale)).ToArray();
            // Bound control vertices too: a spline is one native segment but not one unit of work.
            var work = curves.Sum(c => Math.Max(c.SegmentCount, c.Points.Length));
            if (work > 512) throw new ArgumentException("A sketch batch may contain at most 512 segments/control vertices.");
            return work;
        }
    }
}
