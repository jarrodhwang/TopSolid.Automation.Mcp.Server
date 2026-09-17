using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed partial class AutomationGateway
    {
        public JObject CreateContour(JObject arguments, bool append)
        {
            var expectedLength = ContourGeometry.Validate(arguments);
            return Modify(arguments, append ? "append sketch contour" : "create sketch contour", "kernel", (document, current) =>
            {
                var scale = ModelingGeometry.Scale(current);
                double Value(string key) => ((double?)current[key] ?? 0) * scale;
                var origin = new Point3D(Value("x"), Value("y"), Value("z"));
                ElementId sketch;
                if (append)
                {
                    sketch = Element(current, "sketch");
                    if (!TopSolidHost.Sketches2D.IsSketch(sketch)) throw new ArgumentException("Target element must be a native 2D sketch.");
                }
                else if ((string)current["placement"] == "2d")
                    sketch = TopSolidHost.Sketches2D.CreateSketchIn2D(document, new SmartPoint2D(new Point2D(origin.X, origin.Y)), true,
                        new SmartDirection2D(new Direction2D(1, 0), new Point2D(origin.X, origin.Y)));
                else
                {
                    var placement = (string)current["placement"];
                    var plane = ModelingGeometry.Plane(placement, origin);
                    sketch = TopSolidHost.Sketches2D.CreateSketchIn3D(document, new SmartPlane3D(plane, -1, 1, -1, 1), new SmartPoint3D(origin), true,
                        new SmartDirection3D(placement == "yz" ? new Direction3D(0, 1, 0) : new Direction3D(1, 0, 0), origin));
                }
                RequireValid(sketch, "sketch");
                var contour = (JObject)current["contour"];
                var segments = (JArray)contour["segments"];
                ElementItemId profile, section = ElementItemId.Empty;
                var created = new List<ElementItemId>();
                TopSolidHost.Sketches2D.StartModification(sketch);
                try
                {
                    var first = TopSolidHost.Sketches2D.CreateVertex(ContourGeometry.Point(contour["start"], scale));
                    var previous = first;
                    for (var i = 0; i < segments.Count; i++)
                    {
                        var input = segments[i];
                        var next = i == segments.Count - 1 && (bool)contour["closed"] ? first : TopSolidHost.Sketches2D.CreateVertex(ContourGeometry.Point(input["end"], scale));
                        var segment = (string)input["kind"] == "arc"
                            ? TopSolidHost.Sketches2D.CreateArcSegment(previous, next, ContourGeometry.Point(input["center"], scale), (bool)input["clockwise"])
                            : TopSolidHost.Sketches2D.CreateLineSegment(previous, next);
                        if (segment.IsEmpty) throw new InvalidOperationException("TopSolid returned an empty sketch segment.");
                        created.Add(segment); previous = next;
                    }
                    profile = SketchTopology.ClosedProfile(created, (bool)contour["closed"], TopSolidHost.Sketches2D.CreateProfile);
                    if ((bool)contour["closed"] && profile.IsEmpty) throw new InvalidOperationException("TopSolid returned an empty closed profile.");
                    if ((bool)contour["closed"] && SketchSectionOptions.ForContour(current))
                    {
                        section = TopSolidHost.Sketches2D.CreateSection(new List<ElementItemId> { profile });
                        if (section.IsEmpty) throw new InvalidOperationException("TopSolid returned an empty section.");
                    }
                }
                finally { TopSolidHost.Sketches2D.EndModification(); }
                RequireValid(TopSolidHost.Sketches2D.CreateBuildingOperation(sketch), "sketch building operation");
                if (current["name"] != null) TopSolidHost.Elements.SetName(sketch, (string)current["name"]);
                var actualSegments = profile.IsEmpty ? created : TopSolidHost.Sketches2D.GetProfileSegments(profile);
                if (actualSegments.Count == 0 || (!profile.IsEmpty && !TopSolidHost.Sketches2D.IsProfileClosed(profile)))
                    throw new InvalidOperationException("Native profile topology does not match the requested contour.");
                double actualLength = 0;
                foreach (var segment in actualSegments)
                {
                    TopSolidHost.Sketches2D.GetSegmentCurveRange(segment, out var start, out var end);
                    var type = TopSolidHost.Sketches2D.GetSegmentCurveType(segment);
                    if (type == CurveType.Circle) { TopSolidHost.Sketches2D.GetSegmentCircleCurve(segment, out _, out var radius); actualLength += radius * Math.Abs(end - start); }
                    else if (type == CurveType.Line) actualLength += Math.Abs(end - start);
                    else throw new InvalidOperationException("Native contour contains an unexpected curve type.");
                }
                if (Math.Abs(actualLength - expectedLength) > Math.Max(1e-8, expectedLength * 1e-6))
                    throw new InvalidOperationException("Native contour length does not match the input geometry.");
                return new JObject { ["sketch"] = AutomationValues.Json(sketch), ["profile"] = AutomationValues.Json(profile), ["section"] = AutomationValues.Json(section),
                    ["segments"] = AutomationValues.Json(actualSegments.Take(4)), ["moreSegmentHandles"] = actualSegments.Count > 4,
                    ["nativeSegmentCount"] = actualSegments.Count, ["lengthMetres"] = actualLength, ["closed"] = contour["closed"], ["geometryReadBack"] = true };
            });
        }

        public JObject ChangeSketchItem(JObject arguments)
        {
            return Modify(arguments, "fix sketch item", "kernel", (doc, current) =>
            {
                var item = Item(current);
                if (!TopSolidHost.Sketches2D.IsSketch(item.ElementId)) throw new ArgumentException("The item must belong to a native 2D sketch.");
                var known = TopSolidHost.Sketches2D.GetSegments(item.ElementId).Concat(TopSolidHost.Sketches2D.GetVertices(item.ElementId));
                if (!known.Contains(item)) throw new ArgumentException("Only an existing segment or vertex may be fixed or unfixed.");
                TopSolidHost.Sketches2D.StartModification(item.ElementId);
                try { if ((bool)current["fixed"]) TopSolidHost.Sketches2D.FixItem(item); else TopSolidHost.Sketches2D.UnfixItem(item); }
                finally { TopSolidHost.Sketches2D.EndModification(); }
                return new JObject { ["item"] = AutomationValues.Json(item), ["fixed"] = TopSolidHost.Sketches2D.IsItemFixed(item) };
            });
        }
    }
}
