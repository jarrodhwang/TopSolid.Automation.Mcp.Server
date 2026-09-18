using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class SketchPlanTools
    {
        internal static readonly string[] Api = ApiRefs.Kernel("ISketches2D.CreateSketchIn2D", "ISketches2D.CreateSketchIn3D", "ISketches2D.IsSketch", "ISketches2D.GetFrame", "ISketches2D.GetPlane",
            "ISketches2D.StartModification", "ISketches2D.EndModification", "ISketches2D.CreateVertex", "ISketches2D.CreateLineSegment", "ISketches2D.CreateCircleSegment", "ISketches2D.CreateArcSegment", "ISketches2D.CreateBSplineSegment",
            "ISketches2D.CreateProfile", "ISketches2D.CreateBuildingOperation", "ISketches2D.GetProfileSegments", "ISketches2D.IsProfileClosed", "ISketches2D.GetVertices", "ISketches2D.GetSegments", "ISketches2D.GetSectionCount",
            "ISketches2D.GetVertexPoint", "ISketches2D.GetSegmentVertices", "ISketches2D.GetSegmentCurveType", "ISketches2D.GetSegmentCircleCurve", "ISketches2D.GetSegmentLineCurve", "ISketches2D.GetSegmentCenter", "ISketches2D.GetSegmentMiddle", "ISketches2D.GetSegmentRange", "ISketches2D.GetSegmentPoint", "ISketches2D.IsSegmentReversed", "IElements.GetName", "IElements.GetFriendlyName", "IElements.SetName",
            "IGeometries3D.CreateOffsetPoint", "IGeometries3D.GetOffsetPointCreation", "IElements.GetParent", "IElements.Hide",
            "SmartPlane3D.-ctor", "SmartPoint3D.-ctor", "SmartDirection3D.-ctor", "SmartPoint2D.-ctor", "SmartDirection2D.-ctor", "SmartReal.-ctor").Concat(AppearanceTools.Api).ToArray();
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            var sketch = new JObject(); SketchPlanSchema.Placement(sketch); sketch.Remove("documentSpace");
            sketch["profiles"] = SketchPlanSchema.Profiles();
            sketch["units"] = Schema.Choice("Optional redundant units; must match root units. Prefer root units only.", "mm", "cm", "m");
            var p = DocumentActionTools.Target();
            p["documentSpace"] = Schema.Choice("Dimension of the containing document: part=3d, native 2D document=2d.", "2d", "3d");
            p["units"] = Schema.Choice("Input lengths; default mm.", "mm", "cm", "m");
            p["sketches"] = Schema.Array(Schema.Object(sketch, "profiles"), 1, 8);
            p["sketches"]["description"] = "Each entry has name/placement and REQUIRED profiles:[{kind,...geometry}]. Never put primitive geometry directly on a sketch. Example: {name:'Star_XZ',placement:'xz',profiles:[{kind:'star',center:{x:0,y:0},outerRadius:50,innerRadius:20,pointCount:5,rotationDegrees:90}]}";
            register(new ToolDefinition("topsolid_create_sketches2d", "Draw several requested 2D sketches in one document with ONE confirmation and ONE undoable transaction. Up to 8 sketches, 32 primitives total, 512 segments/control vertices. Lines/arcs/circles/rectangles/polylines/B-splines/parabolas. Explicit position/rotation or associative reference plane+anchor+axes. Reference mode defaults to associative and requires an actual anchor vertex and quarter-turn rotation; 3D documents support XY offsets and quarter-turn rotation with zero normal offset. Native 2D supports zero offset/quarter turns. Local dimensions are independent; no inferred dimensional constraints. Ask for missing design data. No auto-save.",
                p, a.CreateSketches2D, "Sketch2D", new[] { "documentId", "documentSpace", "sketches" }, false, Api, Validate,
                a.PreviewSketches2D, defaultLengthUnits: "mm", defaults: "No sections are created; origin/rotation=0 only when intended by the user. Open curves stay open. Reference mode=associative: plane/anchor/axes links, independent local dimensions. Snapshot only when explicitly chosen.",
                effect: "Create or append the listed sketches together. Associative reference placement creates native Smart links and hidden offset point helpers as required. On failure the whole modification attempts rollback. No sections; no automatic save."));

            var context = DocumentActionTools.Target(); context["documentSpace"] = Schema.Choice("Containing document dimension; a part is 3d even for 2D sketches.", "2d", "3d");
            context["names"] = Schema.Array(Schema.Text("Exact sketch friendly name. Duplicate matches are all returned; choose the correct handle."), 1, 16);
            register(new ToolDefinition("topsolid_get_sketch2d_context", "Get one page of 2D sketch friendly names, exact handles, definition frames/planes and topology counts in ONE read. Optionally filter exact names. Coordinates are SI metres. Multiple matches are ambiguous: ask the user or inspect geometry. Use read_sketch2d_geometry for reference vertices/segments; do not infer screen positions from names.",
                Schema.Page(context, 100), q => a.Read("kernel", () =>
                {
                    var doc = a.Document(q); var names = ((JArray)q["names"])?.Values<string>().ToArray();
                    var ids = TopSolidHost.Sketches2D.GetSketches(doc);
                    var knownNames = new global::System.Collections.Generic.Dictionary<ElementId, JObject>();
                    JObject Names(ElementId id)
                    {
                        if (!knownNames.TryGetValue(id, out var row)) knownNames[id] = row = new JObject {
                            ["internalName"] = TopSolidHost.Elements.GetName(id), ["friendlyName"] = TopSolidHost.Elements.GetFriendlyName(id) };
                        return row;
                    }
                    if (names != null) ids = ids.Where(id => MatchesName(Names(id), names)).ToList();
                    var result = BatchRead.Page(ids, q, id => new JObject { ["sketch"] = AutomationValues.Json(id) }, id => {
                        var row = (JObject)Names(id).DeepClone(); row["name"] = row["friendlyName"].DeepClone();
                        row["placement"] = a.ReadSketchPlacement(id, (string)q["documentSpace"] == "2d").Json();
                        row["vertices"] = TopSolidHost.Sketches2D.GetVertexCount(id); row["segments"] = TopSolidHost.Sketches2D.GetSegmentCount(id);
                        row["profiles"] = TopSolidHost.Sketches2D.GetProfileCount(id); row["sections"] = TopSolidHost.Sketches2D.GetSectionCount(id); return row;
                    });
                    result["documentId"] = doc.PdmDocumentId; result["documentName"] = TopSolidHost.Documents.GetName(doc);
                    result["units"] = "metres";
                    return result;
                }), "Sketch2D", new[] { "documentId", "documentSpace" }, api: ApiRefs.Kernel("ISketches2D.GetSketches", "ISketches2D.IsSketch", "ISketches2D.GetFrame", "ISketches2D.GetPlane", "ISketches2D.GetVertexCount", "ISketches2D.GetSegmentCount", "ISketches2D.GetProfileCount", "ISketches2D.GetSectionCount", "IElements.GetName", "IElements.GetFriendlyName", "IDocuments.GetName")));

            var transform = DocumentActionTools.Target(); transform["sourceSketch"] = Schema.Element(); transform["targetSketch"] = Schema.Element();
            transform["documentSpace"] = Schema.Choice("Containing document dimension.", "2d", "3d"); transform["units"] = Schema.Choice("Input point units; default mm. Geometry read tools return m.", "mm", "cm", "m");
            transform["points"] = Schema.Array(SketchPlanSchema.Point2(), 1, 128);
            register(new ToolDefinition("topsolid_transform_sketch2d_points", "Convert points from one verified sketch frame into another in ONE read. Returns world and target-local coordinates in SI metres. Rejects non-coplanar points; does not silently project or create geometry. Use when drawing relative to geometry in another sketch.", transform,
                q => a.Read("kernel", () =>
                {
                    MutationReferences.Validate(q);
                    var source = a.ReadSketchPlacement(a.Element(q, "sourceSketch"), (string)q["documentSpace"] == "2d");
                    var target = a.ReadSketchPlacement(a.Element(q, "targetSketch"), (string)q["documentSpace"] == "2d");
                    return new JObject { ["units"] = "metres", ["points"] = new JArray(((JArray)q["points"]).Select(point => {
                        var world = source.Plane.ToAbsolute(ContourGeometry.Point(point, ModelingGeometry.Scale(q)));
                        return new JObject { ["world"] = AutomationValues.Json(world), ["targetLocal"] = AutomationValues.Json(SketchPlacement.ToLocal(target.Plane, world)) };
                    })) };
                }), "Sketch2D", new[] { "documentId", "documentSpace", "sourceSketch", "targetSketch", "points" }, api: ApiRefs.Kernel("ISketches2D.IsSketch", "ISketches2D.GetFrame", "ISketches2D.GetPlane", "Plane3D.ToAbsolute"), validate: MutationReferences.Validate));
        }
        internal static bool MatchesName(JObject names, string[] requested) => requested.Any(n =>
            string.Equals(n, (string)names["internalName"], StringComparison.OrdinalIgnoreCase) || string.Equals(n, (string)names["friendlyName"], StringComparison.OrdinalIgnoreCase));
        internal static void Validate(JObject p)
        {
            MutationReferences.Validate(p);
            var sketches = ((JArray)p["sketches"]).Cast<JObject>().ToArray(); var total = 0;
            foreach (var s in sketches) total += SketchPlanSchema.ValidateProfiles(AutomationGateway.BatchSketchArguments(p, s), ModelingGeometry.Scale(p));
            if (total > 512 || sketches.Sum(s => ((JArray)s["profiles"]).Count) > 32) throw new ArgumentException("Across the entire request, use at most 32 primitives and 512 segments/control vertices.");
            var targets = sketches.Where(s => s["sketch"] != null).Select(s => s["sketch"].ToString()).ToArray();
            if (targets.Distinct().Count() != targets.Length) throw new ArgumentException("Combine profiles for the same target sketch into one entry.");
            // A reference must remain unchanged throughout this batch; otherwise its
            // preview would no longer describe the placement used by a later entry.
            if (sketches.Any(s => s["referenceSketch"] != null && targets.Contains(s["referenceSketch"].ToString())))
                throw new ArgumentException("A sketch used as a placement reference cannot also be modified in this batch. Separate the requests and refresh the reference.");
        }
    }
}
