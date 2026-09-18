using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed class Sketch3DCurve
    {
        internal string Kind;
        internal Point3D[] Points = new Point3D[0];
        internal Point3D Center;
        internal Direction3D Normal;
        internal double Radius;
        internal bool Closed;
        internal int SegmentCount => Kind == "polyline" ? Points.Length - (Closed ? 0 : 1) : 1;
        internal static double Distance(Point3D a, Point3D b) => Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)+(a.Z-b.Z)*(a.Z-b.Z));
        internal static Sketch3DCurve Parse(JObject p, double scale)
        {
            var kind = (string)p["kind"];
            var fields = kind == "line" ? new[] { "start", "end" } : kind == "polyline" ? new[] { "points", "closed" }
                : kind == "circle" ? new[] { "center", "normal", "radius" } : kind == "arc" ? new[] { "start", "end", "center", "normal" }
                : kind == "bspline" ? new[] { "controlPoints", "periodic" } : throw new ArgumentException("Supported 3D curves: line, polyline, circle, arc, bspline.");
            if (fields.Any(f => p[f] == null) || p.Properties().Any(f => f.Name != "kind" && !fields.Contains(f.Name))) throw new ArgumentException("For 3D " + kind + " supply exactly: " + string.Join(", ", fields));
            var result = new Sketch3DCurve { Kind = kind };
            if (kind == "circle" || kind == "arc") {
                result.Center = SpatialInput.Point(p["center"], scale); result.Normal = SpatialInput.Direction(p["normal"]);
                result.Closed = kind == "circle";
                if (kind == "circle") { result.Radius = (double)p["radius"] * scale; if (!(result.Radius > 0) || double.IsInfinity(result.Radius)) throw new ArgumentException("Circle radius must be positive and finite."); }
            }
            if (kind == "line" || kind == "arc") result.Points = new[] { SpatialInput.Point(p["start"], scale), SpatialInput.Point(p["end"], scale) };
            if (kind == "polyline" || kind == "bspline") {
                result.Points = ((JArray)p[kind == "polyline" ? "points" : "controlPoints"]).Select(v => SpatialInput.Point(v, scale)).ToArray();
                result.Closed = (bool)p[kind == "polyline" ? "closed" : "periodic"];
                if (result.Points.Length < (kind == "bspline" ? 4 : result.Closed ? 3 : 2)) throw new ArgumentException("Insufficient 3D points for this curve.");
            }
            foreach (var point in result.Points.Concat(new[] { result.Center }))
                if (new[] { point.X, point.Y, point.Z }.Any(v => double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1000000)) throw new ArgumentException("3D coordinates must be finite and within the supported range.");
            for (var i = 1; i < result.Points.Length; i++) if (Distance(result.Points[i-1], result.Points[i]) < 1e-9) throw new ArgumentException("Consecutive 3D points must be distinct.");
            if (result.Points.Length > 2 && Distance(result.Points[0], result.Points.Last()) < 1e-9) throw new ArgumentException("Omit the repeated first point and use closed/periodic=true for closure.");
            if (kind == "arc") {
                result.Radius = Distance(result.Points[0], result.Center);
                if (result.Radius < 1e-9 || Math.Abs(Distance(result.Points[1], result.Center) - result.Radius) > Math.Max(1e-9, result.Radius * 1e-8)) throw new ArgumentException("Arc endpoints must have the same nonzero radius.");
                foreach (var point in result.Points)
                    if (Math.Abs((point.X-result.Center.X)*result.Normal.X+(point.Y-result.Center.Y)*result.Normal.Y+(point.Z-result.Center.Z)*result.Normal.Z) > 1e-8)
                        throw new ArgumentException("Arc endpoints must lie on the plane perpendicular to normal through center.");
            }
            return result;
        }
        internal Point3D ArcMidpoint()
        {
            var a = new[] { (Points[0].X-Center.X)/Radius, (Points[0].Y-Center.Y)/Radius, (Points[0].Z-Center.Z)/Radius };
            var b = new[] { (Points[1].X-Center.X)/Radius, (Points[1].Y-Center.Y)/Radius, (Points[1].Z-Center.Z)/Radius };
            var cross = new[] { Normal.Y*a[2]-Normal.Z*a[1], Normal.Z*a[0]-Normal.X*a[2], Normal.X*a[1]-Normal.Y*a[0] };
            var angle = Math.Atan2(cross[0]*b[0]+cross[1]*b[1]+cross[2]*b[2], a[0]*b[0]+a[1]*b[1]+a[2]*b[2]); if (angle <= 0) angle += 2*Math.PI;
            return new Point3D(Center.X+Radius*(a[0]*Math.Cos(angle/2)+cross[0]*Math.Sin(angle/2)), Center.Y+Radius*(a[1]*Math.Cos(angle/2)+cross[1]*Math.Sin(angle/2)), Center.Z+Radius*(a[2]*Math.Cos(angle/2)+cross[2]*Math.Sin(angle/2)));
        }
    }
    internal sealed partial class AutomationGateway
    {
        public JObject CreateSketch3DCurves(JObject p) => Modify(p, "draw 3D sketch curves", "kernel", (doc, current) => {
            var curves = ((JArray)current["curves"]).Cast<JObject>().Select(v => Sketch3DCurve.Parse(v, ModelingGeometry.Scale(current))).ToArray();
            var sketch = TopSolidHost.Sketches3D.CreateSketch(doc, SmartPlane3D.OXY, new SmartPoint3D(new Point3D(0,0,0)), true, new SmartDirection3D(new Direction3D(1,0,0), new Point3D(0,0,0)));
            RequireValid(sketch, "3D sketch"); var groups = new List<List<ElementItemId>>(); var profiles = new List<ElementItemId>();
            TopSolidHost.Sketches3D.StartModification(sketch);
            try {
                foreach (var curve in curves) {
                    var segments = new List<ElementItemId>();
                    if (curve.Kind == "circle") segments.Add(TopSolidHost.Sketches3D.CreateCircleSegment(TopSolidHost.Sketches3D.CreateVertex(curve.Center), curve.Normal, curve.Radius));
                    else {
                        var vertices = curve.Points.Select(TopSolidHost.Sketches3D.CreateVertex).ToList();
                        if (vertices.Any(v => v.IsEmpty)) throw new InvalidOperationException("Empty native 3D vertex.");
                        if (curve.Kind == "arc") segments.Add(TopSolidHost.Sketches3D.CreateArcSegment(vertices[0], vertices[1], curve.Center, curve.Normal));
                        else if (curve.Kind == "bspline") segments.Add(TopSolidHost.Sketches3D.CreateBSplineSegment(vertices, curve.Closed));
                        else for (var i = 0; i < curve.SegmentCount; i++) segments.Add(TopSolidHost.Sketches3D.CreateLineSegment(vertices[i], vertices[(i+1)%vertices.Count]));
                    }
                    if (segments.Any(v => v.IsEmpty)) throw new InvalidOperationException("Empty native 3D segment.");
                    var profile = curve.Closed ? TopSolidHost.Sketches3D.CreateProfile(segments) : ElementItemId.Empty;
                    if (curve.Closed && profile.IsEmpty) throw new InvalidOperationException("Empty native closed 3D profile.");
                    groups.Add(segments); profiles.Add(profile);
                }
            } finally { TopSolidHost.Sketches3D.EndModification(); }
            RequireValid(TopSolidHost.Sketches3D.CreateBuildingOperation(sketch), "3D sketch building operation"); RequireValid(sketch, "3D sketch");
            var rows = new JArray();
            for (var i = 0; i < curves.Length; i++) {
                var profile = profiles[i];
                if (!profile.IsEmpty && (!TopSolidHost.Sketches3D.IsProfileClosed(profile) || TopSolidHost.Sketches3D.GetProfileSegments(profile).Count != groups[i].Count)) throw new InvalidOperationException("3D profile topology differs from the plan; rolling back.");
                Verify3DCurve(TopSolidHost.Sketches3D, curves[i], groups[i]);
                rows.Add(new JObject { ["inputIndex"] = i, ["kind"] = curves[i].Kind, ["profile"] = AutomationValues.Json(profile), ["segments"] = AutomationValues.Json(groups[i].Take(4)),
                    ["segmentCount"] = groups[i].Count, ["moreSegmentHandles"] = groups[i].Count > 4, ["closed"] = curves[i].Closed,
                    ["verification"] = curves[i].Kind == "bspline" ? "native type, finite curve samples and topology; no control-net read API" : "native geometry and topology" });
            }
            CreationNames.SetAndVerify(TopSolidHost.Elements, sketch, (string)current["name"]);
            return new JObject { ["sketch"] = AutomationValues.Json(sketch), ["curves"] = rows, ["geometryReadBack"] = true, ["sectionCreated"] = false,
                ["name"] = TopSolidHost.Elements.GetName(sketch), ["friendlyName"] = TopSolidHost.Elements.GetFriendlyName(sketch),
                ["coordinateSystem"] = "document absolute frame", ["color"] = current["color"] == null ? null : ElementAppearance.Set(TopSolidHost.Elements, sketch, current["color"]) };
        });
        internal static void Verify3DCurve(ISketches3D api, Sketch3DCurve curve, List<ElementItemId> segments)
        {
            void Near(Point3D a, Point3D b) { var d = Sketch3DCurve.Distance(a,b); if (double.IsNaN(d) || d > 1e-8) throw new InvalidOperationException("3D curve readback differs from requested geometry; rolling back."); }
            for (var i = 0; i < segments.Count; i++) {
                var id = segments[i]; var type = api.GetSegmentCurveType(id);
                if (curve.Kind == "line" || curve.Kind == "polyline") {
                    if (type != CurveType.Line) throw new InvalidOperationException("Expected native 3D line.");
                    api.GetSegmentVertices(id, out var start, out var end); Near(api.GetVertexPoint(start), curve.Points[i]); Near(api.GetVertexPoint(end), curve.Points[(i+1)%curve.Points.Length]);
                } else if (curve.Kind == "circle" || curve.Kind == "arc") {
                    if (type != CurveType.Circle) throw new InvalidOperationException("Expected native 3D circular curve.");
                    api.GetSegmentCircleCurve(id, out var plane, out var radius); Near(plane.Origin, curve.Center);
                    var x = plane.XDirection; var y = plane.YDirection;
                    var dot = (x.Y*y.Z-x.Z*y.Y)*curve.Normal.X+(x.Z*y.X-x.X*y.Z)*curve.Normal.Y+(x.X*y.Y-x.Y*y.X)*curve.Normal.Z;
                    if (Math.Abs(radius-curve.Radius) > 1e-8 || Math.Abs(Math.Abs(dot)-1) > 1e-8) throw new InvalidOperationException("3D circle radius or plane differs.");
                    if (curve.Kind == "arc") {
                        api.GetSegmentRange(id, out var t0, out var t1); var reversed = api.IsSegmentReversed(id);
                        Near(api.GetSegmentPoint(id, reversed ? t1 : t0), curve.Points[0]); Near(api.GetSegmentPoint(id, reversed ? t0 : t1), curve.Points[1]); Near(api.GetSegmentPoint(id, (t0+t1)/2), curve.ArcMidpoint());
                    }
                } else {
                    if (type != CurveType.BSpline) throw new InvalidOperationException("Expected a native 3D B-spline.");
                    api.GetSegmentRange(id, out var t0, out var t1);
                    if (double.IsNaN(t0) || double.IsNaN(t1) || double.IsInfinity(t0) || double.IsInfinity(t1)) throw new InvalidOperationException("Native 3D B-spline range is not finite.");
                    for (var j = 0; j <= 8; j++) { var point = api.GetSegmentPoint(id, t0+(t1-t0)*j/8); if (new[] { point.X, point.Y, point.Z }.Any(v => double.IsNaN(v) || double.IsInfinity(v))) throw new InvalidOperationException("Non-finite native 3D B-spline geometry."); }
                }
            }
        }
    }
}
