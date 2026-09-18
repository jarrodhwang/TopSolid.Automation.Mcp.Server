using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class SketchWorkflowTools
    {
        private static JObject Point() => Schema.Object(new JObject { ["x"] = Schema.Number("Sketch X coordinate."), ["y"] = Schema.Number("Sketch Y coordinate.") }, "x", "y");
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            foreach (var append in new[] { false, true })
            {
                var isAppend = append;
                var p = DocumentActionTools.Target();
                p["units"] = Schema.Choice("Length units; default mm.", "mm", "cm", "m");
                p["contour"] = Schema.Object(new JObject { ["start"] = Point(), ["closed"] = Schema.Boolean("True only when the last endpoint equals the start."),
                    ["segments"] = Schema.Array(Schema.Object(new JObject { ["kind"] = Schema.Choice("Segment type.", "line", "arc"), ["end"] = Point(),
                        ["center"] = Point(), ["clockwise"] = Schema.Boolean("Required for arcs; sketch plane orientation determines clockwise.") }, "kind", "end"), 1, 128) }, "start", "closed", "segments");
                var required = new List<string> { "documentId", "contour" };
                if (append) { p["sketch"] = Schema.Element(); required.Add("sketch"); }
                else
                {
                    p["placement"] = Schema.Choice("Native 2D document or principal plane of a 3D document.", "2d", "xy", "xz", "yz"); required.Add("placement");
                    foreach (var key in new[] { "x", "y", "z" }) p[key] = Schema.Number("Sketch origin " + key + ", default 0.");
                    p["name"] = Schema.Text(CreationNames.Description, 128);
                }
                register(new ToolDefinition(append ? "topsolid_append_sketch_contour" : "topsolid_create_contour2d",
                    "Draw a connected native 2D contour from lines and circular arcs. Closed contours get profiles; open contours remain native segments and return profile=null. Creates no sections. Use the sketch directly for extrusion. Coordinates are local to the sketch. Requires confirmation; does not save.",
                    p, input => a.CreateContour(input, isAppend), "Sketch2D", required.ToArray(), false,
                    ApiRefs.Kernel("ISketches2D.CreateSketchIn2D", "ISketches2D.CreateSketchIn3D", "ISketches2D.StartModification", "ISketches2D.EndModification", "ISketches2D.CreateVertex",
                        "ISketches2D.CreateLineSegment", "ISketches2D.CreateArcSegment", "ISketches2D.CreateProfile", "ISketches2D.CreateBuildingOperation",
                        "ISketches2D.GetSegmentCurveRange", "ISketches2D.GetSegmentCircleCurve", "ISketches2D.GetProfileSegments", "ISketches2D.IsProfileClosed", "ISketches2D.GetSectionCount"),
                    input => { ContourGeometry.Validate(input); }, input => a.PreviewDocument(input), defaultLengthUnits: "mm",
                    defaults: "Omitted origin is 0; No sections are created. Arc center and endpoints use the same local sketch coordinates. Closed contours repeat the start as the final endpoint. No dimensional or geometric constraints are inferred."));
            }
            register(new ToolDefinition("topsolid_set_sketch_item_fixed", "Fix or unfix one existing 2D sketch segment or vertex. This is the documented fixed constraint, not a dimensional constraint.",
                new JObject { ["documentId"] = Schema.Text("Target document revision."), ["item"] = Schema.Item(), ["fixed"] = Schema.Boolean("Whether to fix this item.") },
                a.ChangeSketchItem, "Sketch2D", new[] { "documentId", "item", "fixed" }, false, ApiRefs.Kernel("ISketches2D.FixItem", "ISketches2D.UnfixItem", "ISketches2D.IsItemFixed"), MutationReferences.Validate, p => a.PreviewDocument(p)));
            register(new ToolDefinition("topsolid_list_sketch2d_sections", "Inspect existing native sections. Extrusion/revolution also accept a whole sketch; do not create a section or redraw a sketch to extrude it.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Sketches2D.GetSections(a.Element(p)), p)), "Sketch2D", new[] { "element" }, api: ApiRefs.Kernel("ISketches2D.GetSections")));
            register(new ToolDefinition("topsolid_get_sketch2d_plane", "Get the plane of a 2D sketch embedded in a 3D document. This maps local sketch coordinates into world coordinates; not for native 2D documents.", new JObject { ["element"] = Schema.Element() },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Sketches2D.GetPlane(a.Element(p)))), "Sketch2D", new[] { "element" }, api: ApiRefs.Kernel("ISketches2D.GetPlane")));
            register(new ToolDefinition("topsolid_list_section_profiles", "List the bounding profiles of a 2D sketch section.", Schema.Page(new JObject { ["item"] = Schema.Item() }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Sketches2D.GetSectionProfiles(a.Item(p)), p)), "Sketch2D", new[] { "item" }, api: ApiRefs.Kernel("ISketches2D.GetSectionProfiles")));
        }
    }
}
