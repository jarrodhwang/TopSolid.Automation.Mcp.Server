using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed partial class AutomationGateway
    {
        public JObject PreviewSketchProfiles(JObject p)
        {
            var preview = PreviewDocument(p);
            preview["sketchPlan"] = PreviewSketchPlan(p);
            return preview;
        }
        private JObject PreviewSketchPlan(JObject p)
        {
            var scale = ModelingGeometry.Scale(p);
            if (p["sketch"] != null && !TopSolidHost.Sketches2D.IsSketch(Element(p, "sketch"))) throw new ArgumentException("Target must be a native 2D sketch.");
            var placement = p["sketch"] != null && p["documentSpace"] == null ? null : ResolveSketchPlacement(p, scale);
            var approximations = new JArray(((JArray)p["profiles"]).Cast<JObject>().Select((input, index) => new { Curve = SketchCurveGeometry.Parse(input, scale), Index = index })
                .Where(item => item.Curve.Kind == "ellipse").Select(item => new JObject { ["inputIndex"] = item.Index, ["representation"] = "piecewise cubic ellipse approximation; not an analytic ellipse",
                    ["maximumDeviationBoundMetres"] = item.Curve.ApproximationBound, ["nativeReadbackToleranceMetres"] = 1e-8, ["segmentCount"] = item.Curve.SegmentCount }));
            return new JObject { ["name"] = p["name"], ["existingSketch"] = p["sketch"], ["placement"] = placement?.Json(),
                ["inputUnits"] = (string)p["units"] ?? "mm", ["profiles"] = p["profiles"].DeepClone(), ["sectionMode"] = SketchSectionOptions.Mode(p),
                ["approximations"] = approximations,
                ["openCurveBehavior"] = "Open paths stay native segments; no extra closing edge, profile or section is forced." };
        }
        internal static JObject BatchSketchArguments(JObject batch, JObject sketch)
        {
            SketchCurveGeometry.RequireInheritedUnits(sketch, ModelingGeometry.Scale(batch));
            var p = (JObject)sketch.DeepClone();
            foreach (var key in new[] { "documentId", "documentSpace", "units" }) if (batch[key] != null) p[key] = batch[key].DeepClone();
            return p;
        }
        public JObject PreviewSketches2D(JObject p)
        {
            var preview = PreviewDocument(p);
            preview["sketches"] = new JArray(((JArray)p["sketches"]).Cast<JObject>().Select(s => PreviewSketchPlan(BatchSketchArguments(p, s))));
            return preview;
        }
        public JObject CreateSketches2D(JObject p) => Modify(p, "draw 2D sketches", "kernel", (doc, current) =>
        {
            var results = new JArray();
            foreach (JObject sketch in current["sketches"]) results.Add(CreateSketchProfilesCore(doc, BatchSketchArguments(current, sketch)));
            return new JObject { ["sketches"] = results, ["count"] = results.Count, ["geometryReadBack"] = true };
        });
        public JObject CreateSketchProfiles(JObject p) => Modify(p, "draw sketch geometry", "kernel", CreateSketchProfilesCore);

        private JObject CreateSketchProfilesCore(DocumentId doc, JObject current)
        {
            var scale = ModelingGeometry.Scale(current);
            var placement = current["sketch"] != null && current["documentSpace"] == null ? null : ResolveSketchPlacement(current, scale);
            var sketch = current["sketch"] != null ? Element(current, "sketch") : CreatePlacedSketch(doc, placement);
            RequireValid(sketch, "sketch");
            if (!TopSolidHost.Sketches2D.IsSketch(sketch)) throw new ArgumentException("Target must be a native 2D sketch.");
            var plans = ((JArray)current["profiles"]).Cast<JObject>().Select(p => SketchCurveGeometry.Parse(p, scale)).ToArray();
            var profiles = new List<ElementItemId>(); var sections = new List<ElementItemId>(); var segmentsByInput = new List<List<ElementItemId>>();
            var mode = SketchSectionOptions.Mode(current);
            TopSolidHost.Sketches2D.StartModification(sketch);
            try
            {
                foreach (var curve in plans)
                {
                    var segments = new List<ElementItemId>();
                    if (curve.Kind == "circle") segments.Add(TopSolidHost.Sketches2D.CreateCircleSegment(TopSolidHost.Sketches2D.CreateVertex(curve.Center), curve.Radius));
                    else
                    {
                        var vertices = curve.Points.Select(TopSolidHost.Sketches2D.CreateVertex).ToList();
                        if (vertices.Any(v => v.IsEmpty)) throw new InvalidOperationException("Empty native sketch vertex.");
                        if (curve.Kind == "ellipse")
                        {
                            // Share each cubic endpoint with its neighbor, including
                            // closure, so native profile topology is connected.
                            for (var i = 0; i < curve.SegmentCount; i++)
                                segments.Add(TopSolidHost.Sketches2D.CreateBSplineSegment(Enumerable.Range(0, 4).Select(j => vertices[(3 * i + j) % vertices.Count]).ToList(), false));
                        }
                        else if (curve.Kind == "bspline" || curve.Kind == "parabola") segments.Add(TopSolidHost.Sketches2D.CreateBSplineSegment(vertices, curve.Periodic));
                        else if (curve.Kind == "arc") segments.Add(TopSolidHost.Sketches2D.CreateArcSegment(vertices[0], vertices[1], curve.Center, curve.Clockwise));
                        else for (var i = 0; i < curve.SegmentCount; i++) segments.Add(TopSolidHost.Sketches2D.CreateLineSegment(vertices[i], vertices[(i + 1) % vertices.Count]));
                    }
                    if (segments.Any(v => v.IsEmpty)) throw new InvalidOperationException("Empty native sketch segment.");
                    var profile = SketchTopology.ClosedProfile(segments, curve.Closed, TopSolidHost.Sketches2D.CreateProfile);
                    if (curve.Closed && profile.IsEmpty) throw new InvalidOperationException("Empty native closed profile.");
                    profiles.Add(profile); segmentsByInput.Add(segments);
                    if (mode == "perProfile") sections.Add(TopSolidHost.Sketches2D.CreateSection(new List<ElementItemId> { profile }));
                }
                if (mode == "combined") sections.Add(TopSolidHost.Sketches2D.CreateSection(profiles));
                if (sections.Any(v => v.IsEmpty)) throw new InvalidOperationException("Empty native section.");
            }
            finally { TopSolidHost.Sketches2D.EndModification(); }
            RequireValid(TopSolidHost.Sketches2D.CreateBuildingOperation(sketch), "sketch building operation");
            if (current["name"] != null) TopSolidHost.Elements.SetName(sketch, (string)current["name"]);
            if (placement != null) VerifySketchPlacement(placement, ReadSketchPlacement(sketch, placement.In2D));
            var rows = new JArray();
            for (var i = 0; i < plans.Length; i++)
            {
                var curve = plans[i]; var segments = segmentsByInput[i];
                if (!profiles[i].IsEmpty && (!TopSolidHost.Sketches2D.IsProfileClosed(profiles[i]) || TopSolidHost.Sketches2D.GetProfileSegments(profiles[i]).Count != segments.Count))
                    throw new InvalidOperationException("Native closed profile topology differs from the plan.");
                VerifySketchCurve(curve, segments);
                rows.Add(new JObject { ["inputIndex"] = i, ["kind"] = curve.Kind, ["profile"] = AutomationValues.Json(profiles[i]),
                    ["closed"] = curve.Closed, ["segmentCount"] = segments.Count, ["segments"] = AutomationValues.Json(segments.Take(4)),
                    ["moreSegmentHandles"] = segments.Count > 4, ["geometryReadBack"] = true,
                    ["representation"] = curve.Kind == "parabola" ? "quadratic represented by a cubic B-spline, checked at 17 parameter values" : curve.Kind == "ellipse" ? "piecewise cubic ellipse approximation; not an analytic ellipse" : curve.Kind,
                    ["maximumDeviationBoundMetres"] = curve.Kind == "ellipse" ? (double?)curve.ApproximationBound : null,
                    ["verification"] = curve.Kind == "bspline" ? "finite native samples and topology; control net is not exposed by the read API" : "placement and native curve geometry" });
            }
            return new JObject { ["sketch"] = AutomationValues.Json(sketch), ["name"] = TopSolidHost.Elements.GetName(sketch), ["friendlyName"] = TopSolidHost.Elements.GetFriendlyName(sketch), ["placement"] = placement?.Json(),
                ["profiles"] = rows, ["sections"] = AutomationValues.Json(sections), ["geometryReadBack"] = true, ["units"] = "metres" };
        }
        internal static void VerifySketchPlacement(SketchPlacement expected, SketchPlacement actual)
        {
            var a = expected.Plane; var b = actual.Plane;
            var originError = Math.Abs(a.Origin.X - b.Origin.X) + Math.Abs(a.Origin.Y - b.Origin.Y) + Math.Abs(a.Origin.Z - b.Origin.Z);
            var directionError = Math.Abs(a.XDirection.X - b.XDirection.X) + Math.Abs(a.XDirection.Y - b.XDirection.Y) + Math.Abs(a.XDirection.Z - b.XDirection.Z)
                + Math.Abs(a.YDirection.X - b.YDirection.X) + Math.Abs(a.YDirection.Y - b.YDirection.Y) + Math.Abs(a.YDirection.Z - b.YDirection.Z);
            if (expected.In2D != actual.In2D || double.IsNaN(originError) || double.IsNaN(directionError) || originError > 1e-8 || directionError > 1e-8)
                throw new InvalidOperationException("Native sketch frame differs from requested placement. Rolling back.");
        }
        private static void VerifySketchCurve(SketchCurveGeometry curve, List<ElementItemId> segments)
        {
            void Near(Point2D a, Point2D b) { var d = SketchCurveGeometry.Distance(a, b); if (double.IsNaN(d) || d > 1e-8) throw new InvalidOperationException("Native curve readback differs from requested geometry. Rolling back."); }
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i]; var type = TopSolidHost.Sketches2D.GetSegmentCurveType(segment);
                TopSolidHost.Sketches2D.GetSegmentRange(segment, out var t0, out var t1);
                if (double.IsNaN(t0) || double.IsNaN(t1) || double.IsInfinity(t0) || double.IsInfinity(t1)) throw new InvalidOperationException("A created finite curve returned an unbounded native parameter range.");
                if (curve.Kind == "parabola" || curve.Kind == "ellipse")
                {
                    var controls = curve.Kind == "ellipse" ? curve.CubicSegment(i) : curve.Points;
                    for (var j = 0; j <= 16; j++) Near(TopSolidHost.Sketches2D.GetSegmentPoint(segment, t0 + (t1 - t0) * j / 16.0), SketchCurveGeometry.Cubic(controls, j / 16.0));
                }
                else if (curve.Kind == "circle" || curve.Kind == "arc")
                {
                    if (type != CurveType.Circle) throw new InvalidOperationException("Native curve is not circular.");
                    TopSolidHost.Sketches2D.GetSegmentCircleCurve(segment, out var frame, out var radius); Near(frame.Origin, curve.Center);
                    if (Math.Abs(radius - curve.Radius) > 1e-8) throw new InvalidOperationException("Native radius differs from the plan.");
                    if (curve.Kind == "arc")
                    {
                        var reversed = TopSolidHost.Sketches2D.IsSegmentReversed(segment);
                        Near(TopSolidHost.Sketches2D.GetSegmentPoint(segment, reversed ? t1 : t0), curve.Points[0]);
                        Near(TopSolidHost.Sketches2D.GetSegmentPoint(segment, reversed ? t0 : t1), curve.Points[1]);
                        var start = Math.Atan2(curve.Points[0].Y - curve.Center.Y, curve.Points[0].X - curve.Center.X);
                        var end = Math.Atan2(curve.Points[1].Y - curve.Center.Y, curve.Points[1].X - curve.Center.X);
                        var sweep = end - start;
                        if (curve.Clockwise) { while (sweep >= 0) sweep -= 2 * Math.PI; } else { while (sweep <= 0) sweep += 2 * Math.PI; }
                        Near(TopSolidHost.Sketches2D.GetSegmentPoint(segment, (t0 + t1) / 2), new Point2D(curve.Center.X + curve.Radius * Math.Cos(start + sweep / 2), curve.Center.Y + curve.Radius * Math.Sin(start + sweep / 2)));
                    }
                }
                else if (curve.Kind != "bspline")
                {
                    if (type != CurveType.Line) throw new InvalidOperationException("Native curve is not a line.");
                    TopSolidHost.Sketches2D.GetSegmentVertices(segment, out var start, out var end);
                    Near(TopSolidHost.Sketches2D.GetVertexPoint(start), curve.Points[i]); Near(TopSolidHost.Sketches2D.GetVertexPoint(end), curve.Points[(i + 1) % curve.Points.Length]);
                }
                else
                {
                    // No public control-net reader exists on ISketches2D.
                    for (var j = 0; j <= 8; j++)
                    {
                        var p = TopSolidHost.Sketches2D.GetSegmentPoint(segment, t0 + (t1 - t0) * j / 8.0);
                        if (double.IsNaN(p.X) || double.IsNaN(p.Y) || double.IsInfinity(p.X) || double.IsInfinity(p.Y)) throw new InvalidOperationException("Native B-spline returned non-finite geometry.");
                    }
                }
            }
        }
    }
}
