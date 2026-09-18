using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class Sketch3DCurveTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            var curve = new JObject { ["kind"] = Schema.Choice("Only the matching fields: line(start,end); polyline(points,closed); circle(center,normal,radius); arc(start,end,center,normal); bspline(controlPoints,periodic). Coordinates are absolute world coordinates.", "line", "polyline", "circle", "arc", "bspline"),
                ["start"] = ShapeWorkflowTools.Vector("World start"), ["end"] = ShapeWorkflowTools.Vector("World end"), ["center"] = ShapeWorkflowTools.Vector("World circle center"),
                ["normal"] = ShapeWorkflowTools.Vector("Circle/arc plane normal; arcs travel counterclockwise about this normal from start to end. Negate normal for the other sweep"),
                ["radius"] = Schema.Number("Circle radius in input units.", .001, 100000), ["points"] = Schema.Array(ShapeWorkflowTools.Vector("World vertex"), 2, 128),
                ["closed"] = Schema.Boolean("Polyline closure; omit repeated first point."), ["controlPoints"] = Schema.Array(ShapeWorkflowTools.Vector("World B-spline control point, not interpolation point"), 4, 128), ["periodic"] = Schema.Boolean("Periodic B-spline closure.") };
            var p = DocumentActionTools.Target(); p["units"] = Schema.Choice("Input lengths; default mm.", "mm", "cm", "m"); p["name"] = Schema.Text(CreationNames.Description, 128);
            p["color"] = AppearanceTools.ColorSchema(); p["curves"] = Schema.Array(Schema.Object(curve, "kind"), 1, 32);
            register(new ToolDefinition("topsolid_create_sketch3d_curves", "Draw lines, connected polylines, circles, arcs and uniform cubic B-splines in ONE new native 3D sketch. Up to 32 curves/512 points, absolute document coordinates. Closed inputs share vertices and become profiles; open paths remain open. Optional display color. Requires confirmation, no sections, no save.", p,
                a.CreateSketch3DCurves, "Sketch3D", new[] { "documentId", "curves" }, false,
                ApiRefs.Kernel("ISketches3D.CreateSketch", "ISketches3D.CreateVertex", "ISketches3D.CreateLineSegment", "ISketches3D.CreateCircleSegment", "ISketches3D.CreateArcSegment", "ISketches3D.CreateBSplineSegment", "ISketches3D.CreateProfile", "ISketches3D.CreateBuildingOperation", "ISketches3D.StartModification", "ISketches3D.EndModification", "ISketches3D.IsProfileClosed", "ISketches3D.GetProfileSegments", "ISketches3D.GetSegmentCurveType", "ISketches3D.GetSegmentVertices", "ISketches3D.GetVertexPoint", "ISketches3D.GetSegmentCircleCurve", "ISketches3D.GetSegmentRange", "ISketches3D.GetSegmentPoint", "ISketches3D.IsSegmentReversed", "IElements.SetName").Concat(AppearanceTools.Api).ToArray(),
                Validate, a.PreviewModeling, defaultLengthUnits: "mm"));
        }
        internal static void Validate(JObject p)
        {
            MutationReferences.Validate(p);
            ElementAppearance.Validate(p);
            var curves = ((JArray)p["curves"]).Cast<JObject>().Select(v => Sketch3DCurve.Parse(v, ModelingGeometry.Scale(p))).ToArray();
            if (curves.Sum(c => Math.Max(1, c.Points.Length)) > 512) throw new ArgumentException("A 3D sketch batch is limited to 512 points/control vertices.");
        }
    }
}
