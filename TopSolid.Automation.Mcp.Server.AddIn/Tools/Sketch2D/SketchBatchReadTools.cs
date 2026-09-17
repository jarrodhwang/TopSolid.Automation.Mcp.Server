using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class SketchBatchReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            foreach (var dimension in new[] { 2, 3 })
            {
                var d = dimension;
                register(new ToolDefinition("topsolid_read_sketch" + d + "d_geometry", "Read a page of sketch vertices with coordinates, segments with curve data/endpoints, or profiles with closure/segments. Geometry uses SI metres in sketch coordinates. Avoid inspecting each item separately. Follow nextOffset.",
                    BatchRead.Paging(new JObject { ["sketch"] = Schema.Element(), ["kind"] = Schema.Choice("Topology to read.", "vertices", "segments", "profiles") }),
                    p => a.Read("kernel", () =>
                    {
                        var sketch = a.Element(p, "sketch"); var kind = (string)p["kind"];
                        if (!(d == 2 ? TopSolidHost.Sketches2D.IsSketch(sketch) : TopSolidHost.Sketches3D.IsSketch(sketch))) throw new ArgumentException("Sketch dimension does not match this tool.");
                        List<ElementItemId> ids = d == 2
                            ? kind == "vertices" ? TopSolidHost.Sketches2D.GetVertices(sketch) : kind == "profiles" ? TopSolidHost.Sketches2D.GetProfiles(sketch) : TopSolidHost.Sketches2D.GetSegments(sketch)
                            : kind == "vertices" ? TopSolidHost.Sketches3D.GetVertices(sketch) : kind == "profiles" ? TopSolidHost.Sketches3D.GetProfiles(sketch) : TopSolidHost.Sketches3D.GetSegments(sketch);
                        return BatchRead.Page(ids, p, id => new JObject { ["item"] = AutomationValues.Json(id) }, id => Detail(id, d, kind));
                    }), "Sketch" + d + "D", new[] { "sketch", "kind" }, api: ApiRefs.Kernel(ApiMembers(d))));
            }
        }
        private static string[] ApiMembers(int d)
        {
            var methods = new[] { "IsSketch", "GetVertices", "GetVertexPoint", "GetProfiles", "GetProfileSegments", "IsProfileClosed", "GetSegments", "GetSegmentVertices", "GetSegmentCurveType", "GetSegmentCurveRange", "GetSegmentCircleCurve", "GetSegmentLineCurve" };
            var references = new List<string>(Array.ConvertAll(methods, name => "ISketches" + d + "D." + name));
            if (d == 2) references.AddRange(new[] { "ISketches2D.GetSegmentRange", "ISketches2D.GetSegmentPoint", "ISketches2D.GetSegmentCenter", "ISketches2D.IsSegmentReversed" });
            return references.ToArray();
        }
        private static JObject Detail(ElementItemId id, int d, string kind)
        {
            if (kind == "vertices") return new JObject { ["pointMetres"] = d == 2 ? AutomationValues.Json(TopSolidHost.Sketches2D.GetVertexPoint(id)) : AutomationValues.Json(TopSolidHost.Sketches3D.GetVertexPoint(id)) };
            if (kind == "profiles") return new JObject { ["closed"] = d == 2 ? TopSolidHost.Sketches2D.IsProfileClosed(id) : TopSolidHost.Sketches3D.IsProfileClosed(id),
                ["segments"] = AutomationValues.Json(d == 2 ? TopSolidHost.Sketches2D.GetProfileSegments(id) : TopSolidHost.Sketches3D.GetProfileSegments(id)) };
            ElementItemId start, end; double t0, t1;
            var type = d == 2 ? TopSolidHost.Sketches2D.GetSegmentCurveType(id) : TopSolidHost.Sketches3D.GetSegmentCurveType(id);
            if (d == 2) { TopSolidHost.Sketches2D.GetSegmentVertices(id, out start, out end); TopSolidHost.Sketches2D.GetSegmentCurveRange(id, out t0, out t1); }
            else { TopSolidHost.Sketches3D.GetSegmentVertices(id, out start, out end); TopSolidHost.Sketches3D.GetSegmentCurveRange(id, out t0, out t1); }
            var row = new JObject { ["curveType"] = type.ToString(), ["startVertex"] = AutomationValues.Json(start), ["endVertex"] = AutomationValues.Json(end), ["curveRange"] = new JArray(t0, t1) };
            if (d == 2)
            {
                TopSolidHost.Sketches2D.GetSegmentRange(id, out var minimum, out var maximum);
                row["segmentRange"] = new JArray(minimum, maximum);
                // 7.20.400.107 requires StartModification for IsSegmentConstruction.
                // Never open an edit session from a read-only tool.
                row["constructionStatus"] = "not queried: native getter requires an edit session";
                row["reversed"] = TopSolidHost.Sketches2D.IsSegmentReversed(id);
                var samples = new JArray();
                if (double.IsNaN(minimum) || double.IsNaN(maximum) || double.IsInfinity(minimum) || double.IsInfinity(maximum))
                    row["samplingStatus"] = "unbounded curve: use its analytic axis, not arbitrary sample limits";
                else for (var i = 0; i <= 4; i++) samples.Add(AutomationValues.Json(TopSolidHost.Sketches2D.GetSegmentPoint(id, minimum + (maximum - minimum) * i / 4)));
                row["samplesMetres"] = samples;
                row["samplesNote"] = "Native evaluations at increasing parameter values; samples are not B-spline control points or a replacement curve.";
            }
            if (!start.IsEmpty) row["startMetres"] = d == 2 ? AutomationValues.Json(TopSolidHost.Sketches2D.GetVertexPoint(start)) : AutomationValues.Json(TopSolidHost.Sketches3D.GetVertexPoint(start));
            if (!end.IsEmpty) row["endMetres"] = d == 2 ? AutomationValues.Json(TopSolidHost.Sketches2D.GetVertexPoint(end)) : AutomationValues.Json(TopSolidHost.Sketches3D.GetVertexPoint(end));
            if (type == CurveType.Circle)
            {
                double radius;
                if (d == 2) { TopSolidHost.Sketches2D.GetSegmentCircleCurve(id, out var frame, out radius); row["circleFrame"] = AutomationValues.Json(frame); row["centerVertex"] = AutomationValues.Json(TopSolidHost.Sketches2D.GetSegmentCenter(id)); }
                else { TopSolidHost.Sketches3D.GetSegmentCircleCurve(id, out var plane, out radius); row["circlePlane"] = AutomationValues.Json(plane); }
                row["radiusMetres"] = radius;
            }
            else if (type == CurveType.Line)
            {
                if (d == 2) { TopSolidHost.Sketches2D.GetSegmentLineCurve(id, out var axis); row["lineAxis"] = AutomationValues.Json(axis); }
                else { TopSolidHost.Sketches3D.GetSegmentLineCurve(id, out var axis); row["lineAxis"] = AutomationValues.Json(axis); }
            }
            else row["geometryNote"] = "Detailed curve adapter supports lines and circles; other types retain their exact handles and curve ranges.";
            return row;
        }
    }
}
