using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class AppearanceTools
    {
        internal static readonly string[] Api = ApiRefs.Kernel("IElements.IsColorModifiable", "IElements.HasColor", "IElements.GetColor", "IElements.SetColor", "Color");
        internal static JObject ColorSchema()
        {
            var p = (JObject)ParameterValueInput.ColorSchema()["properties"].DeepClone();
            p.AddFirst(new JProperty("name", Schema.Choice("Prefer a named color, resolved exactly by the server. Supply name alone, or all r/g/b bytes without name.", ElementAppearance.Names)));
            var schema = Schema.Object(p); schema["description"] = "Prefer {name:'red'} for ordinary colors. Or supply {r:255,g:0,b:0} for exact RGB. Choose ONE representation."; return schema;
        }
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            var p = DocumentActionTools.Target(); p["elements"] = Schema.Array(Schema.Element(), 1, 32); p["color"] = ColorSchema();
            register(new ToolDefinition("topsolid_set_entity_colors", "Apply RGB display color to sketches, shapes, surfaces or other color-modifiable elements in ONE confirmed batch. This changes the actual element, not a Color parameter. Face-specific overrides remain unchanged; use color_shape_faces for faces. Does not save.", p,
                input => a.Modify(input, "color elements", "kernel", (doc, current) => new JObject { ["items"] = new JArray(((JArray)current["elements"]).Select(handle =>
                    ElementAppearance.Set(TopSolidHost.Elements, a.Element(new JObject { ["element"] = handle.DeepClone() }), current["color"]))) }),
                "Entities", new[] { "documentId", "elements", "color" }, false, Api.Concat(EntityBatchReadTools.InfoApi).ToArray(), input => { BatchInput.Elements(input); ElementAppearance.Validate(input); },
                input => EntityBatchActionTools.Preview(a, input, "elements", false, id => {
                    var row = ElementAppearance.Read(TopSolidHost.Elements, id);
                    if (!(bool)row["colorModifiable"]) throw new ArgumentException("The selected element's color is not modifiable in this session.");
                    return row;
                })));

            var faces = DocumentActionTools.Target(); faces["faces"] = Schema.Array(Schema.Item(), 1, 256); faces["color"] = ColorSchema();
            register(new ToolDefinition("topsolid_color_shape_faces", "Create a native coloring operation for up to 256 exact shape/surface face handles. Obtain handles from shape inspection; never invent labels. Applies face-specific RGB color and verifies it. Requires confirmation; does not save.", faces,
                input => a.Modify(input, "color shape faces", "kernel", (doc, current) => {
                    var ids = FaceIds(a, current); VerifyFaces(ids);
                    var requested = ElementAppearance.Parse(current["color"]);
                    var operations = ids.GroupBy(id => id.ElementId).Select(group => {
                        var operation = TopSolidHost.Shapes.CreateColoringOperation(group.ToList(), requested);
                        AutomationGateway.RequireValid(operation, "face coloring operation"); return operation;
                    }).ToList();
                    foreach (var id in ids) if (!TopSolidHost.Shapes.GetFaceColor(id).Equals(requested)) throw new InvalidOperationException("Face color readback differs; rolling back.");
                    return new JObject { ["operation"] = AutomationValues.Json(operations[0]), ["operations"] = AutomationValues.Json(operations), ["faces"] = AutomationValues.Json(ids), ["color"] = ElementAppearance.Json(requested), ["readBackVerified"] = true };
                }), "Design3D", new[] { "documentId", "faces", "color" }, false,
                ApiRefs.Kernel("IShapes.GetFaces", "IShapes.GetFaceColor", "IShapes.CreateColoringOperation", "IElements.Exists", "IElements.IsInvalid", "IElements.IsColorModifiable", "Color"),
                input => { MutationReferences.Validate(input); ElementAppearance.Validate(input); }, input => {
                    var preview = a.PreviewDocument(input); var ids = FaceIds(a, input); VerifyFaces(ids);
                    preview["faces"] = new JArray(ids.Select(id => new JObject { ["face"] = AutomationValues.Json(id), ["color"] = ElementAppearance.Json(TopSolidHost.Shapes.GetFaceColor(id)) })); return preview;
                }));
        }
        private static global::System.Collections.Generic.List<ElementItemId> FaceIds(AutomationGateway a, JObject p)
        {
            var ids = ((JArray)p["faces"]).Select(v => a.Item(new JObject { ["item"] = v.DeepClone() })).ToList();
            if (ids.Distinct().Count() != ids.Count) throw new ArgumentException("Duplicate face handles are not allowed."); return ids;
        }
        private static void VerifyFaces(global::System.Collections.Generic.List<ElementItemId> faces)
        {
            foreach (var group in faces.GroupBy(f => f.ElementId)) {
                if (!TopSolidHost.Elements.IsColorModifiable(group.Key))
                    throw new ArgumentException("The face belongs to an unmodifiable shape. Prepare an editable native workpiece before coloring.");
                var known = TopSolidHost.Shapes.GetFaces(group.Key);
                if (group.Any(f => !known.Contains(f))) throw new ArgumentException("A face handle is not present on its shape. Refresh shape geometry before coloring.");
            }
        }
    }
}
