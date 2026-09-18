using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class SketchBatchActionTools
    {
        private static JObject Point() => Schema.Object(new JObject { ["x"] = Schema.Number("Local sketch X."), ["y"] = Schema.Number("Local sketch Y.") }, "x", "y");
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            var p = DocumentActionTools.Target(); SketchPlanSchema.Placement(p);
            p["units"] = Schema.Choice("Input units; default mm.", "mm", "cm", "m");
            p["profiles"] = SketchPlanSchema.Profiles();
            register(new ToolDefinition("topsolid_create_sketch_profiles", "Draw up to 32 native primitives in ONE sketch: circles, rectangles, lines, arcs, polylines, cubic B-splines, quadratic parabolas represented as cubic B-splines with checked readback. Open curves remain segments. New explicit placement, associative reference plane/anchor/axes, or append to existing sketch. Reference mode defaults to associative; requires a real anchor vertex and zero normal offset. Shape dimensions remain independent. One confirmation; no save.",
                p, a.CreateSketchProfiles, "Sketch2D", new[] { "documentId", "profiles" }, false,
                SketchPlanTools.Api, ValidateProfiles, a.PreviewSketchProfiles, defaultLengthUnits: "mm",
                defaults: "Local sketch coordinates; origin and rotation default to zero. No sections are created; referenceMode=associative with explicit anchor. Native 2D linked placement requires zero offset and quarter-turn rotation. Missing dimensions must be requested from the user."));
            foreach (var delete in new[] { false, true })
            {
                var remove = delete;
                var properties = DocumentActionTools.Target(); properties["sketch"] = Schema.Element(); properties["items"] = Schema.Array(Schema.Item(), 1, 100);
                if (!delete) properties["fixed"] = Schema.Boolean("Fix or unfix all supplied sketch vertices/segments.");
                register(new ToolDefinition(delete ? "topsolid_delete_sketch2d_items" : "topsolid_set_sketch2d_items_fixed",
                    (delete ? "Delete up to 100 explicit vertices/segments from one 2D sketch using native DeleteItems. Dependent profiles/sections can change." : "Fix/unfix up to 100 existing vertices/segments of one 2D sketch.") + " One confirmed undoable modification. Refresh sketch handles afterwards; does not save.",
                    properties, input => a.Modify(input, remove ? "delete sketch items" : "fix sketch items", "kernel", (doc, current) =>
                    {
                        var sketch = a.Element(current, "sketch"); var items = Items(a, current);
                        TopSolidHost.Sketches2D.StartModification(sketch);
                        try
                        {
                            if (remove) TopSolidHost.Sketches2D.DeleteItems(items);
                            else foreach (var item in items) { if ((bool)current["fixed"]) TopSolidHost.Sketches2D.FixItem(item); else TopSolidHost.Sketches2D.UnfixItem(item); }
                        }
                        finally { TopSolidHost.Sketches2D.EndModification(); }
                        AutomationGateway.RequireValid(TopSolidHost.Sketches2D.CreateBuildingOperation(sketch), "sketch building operation");
                        if (remove)
                        {
                            var remaining = TopSolidHost.Sketches2D.GetVertices(sketch).Concat(TopSolidHost.Sketches2D.GetSegments(sketch)).ToList();
                            if (items.Any(remaining.Contains)) throw new InvalidOperationException("Some requested sketch items remain; rolling back.");
                        }
                        else if (items.Any(item => TopSolidHost.Sketches2D.IsItemFixed(item) != (bool)current["fixed"])) throw new InvalidOperationException("Fixed state readback failed; rolling back.");
                        return new JObject { ["sketch"] = AutomationValues.Json(sketch), ["changed"] = items.Count, ["refreshRequired"] = true };
                    }), "Sketch2D", delete ? new[] { "documentId", "sketch", "items" } : new[] { "documentId", "sketch", "items", "fixed" }, false,
                    ApiRefs.Kernel("ISketches2D.IsSketch", "ISketches2D.GetVertices", "ISketches2D.GetSegments", "ISketches2D.StartModification", "ISketches2D.EndModification", "ISketches2D.DeleteItems", "ISketches2D.FixItem", "ISketches2D.UnfixItem", "ISketches2D.IsItemFixed", "ISketches2D.CreateBuildingOperation"),
                    ValidateItems, input => { var preview = a.PreviewDocument(input); var items = Items(a, input); preview["items"] = new JArray(items.Select(item => new JObject { ["item"] = AutomationValues.Json(item), ["fixed"] = TopSolidHost.Sketches2D.IsItemFixed(item) })); return preview; },
                    effect: delete ? "Delete the listed sketch vertices/segments; connected segments, profiles, sections and dependent geometry may change. One undoable modification; does not save." : null));
            }
        }
        private static List<ElementItemId> Items(AutomationGateway a, JObject p)
        {
            var sketch = a.Element(p, "sketch");
            if (!TopSolidHost.Sketches2D.IsSketch(sketch)) throw new ArgumentException("A native 2D sketch is required.");
            var known = TopSolidHost.Sketches2D.GetVertices(sketch).Concat(TopSolidHost.Sketches2D.GetSegments(sketch)).ToList();
            var items = ((JArray)p["items"]).Select(v => a.Item(new JObject { ["item"] = v.DeepClone() })).ToList();
            if (items.Any(item => !known.Contains(item))) throw new ArgumentException("Use existing vertex/segment handles from this exact sketch.");
            return items;
        }
        internal static void ValidateItems(JObject p)
        {
            MutationReferences.Validate(p); BatchInput.Unique((JArray)p["items"], "sketch item");
            if (((JArray)p["items"]).Any(item => !JToken.DeepEquals(item["element"], p["sketch"]))) throw new ArgumentException("Every item must belong to the selected sketch.");
        }
        internal static void ValidateProfiles(JObject p)
        {
            MutationReferences.Validate(p);
            SketchPlanSchema.ValidateProfiles(p, ModelingGeometry.Scale(p));
        }
    }
}
