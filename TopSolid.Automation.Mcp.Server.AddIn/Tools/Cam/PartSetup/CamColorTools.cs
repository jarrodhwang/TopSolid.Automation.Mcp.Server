using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class CamColorTools
    {
        internal const string InspectName = "topsolid_inspect_cam_color_geometry";
        internal const string ApplyName = "topsolid_apply_cam_color_plan";
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            var read = new JObject { ["documentId"] = Schema.Text("Exact target revision; omit for the active document."),
                ["workpiece"] = Schema.Element(), ["offset"] = Schema.Integer("Page offset.", 0, 20000), ["limit"] = Schema.Integer("Page size.", 1, 20) };
            read["targets"] = Schema.Array(Schema.Object(new JObject { ["element"] = Schema.Element(), ["face"] = Schema.Item() }), 1, 20);
            register(new ToolDefinition(InspectName, "Read paged native geometry facts, original RGB, capability and fingerprints for CAM color preparation. No changes. CAM documents require an exact workpiece from the returned workpieces list. Edges are not color targets. Sketch profiles color the whole sketch.", read,
                p => a.Read("kernel", () => CamColorGeometry.Page(a, p)), "Cam/PartSetup", api: Api));
            register(new ToolDefinition(ApplyName, "Apply a reviewed CAM color plan atomically to one exact document/workpiece. Rechecks native geometry, colors and capabilities. At most 256 faces and 32 whole entities. Preserves unrelated colors; rolls back on mismatch. Does not save, execute CAM or generate NC.", PlanProperties(),
                p => { Preview(a,p); return a.Modify(p,"prepare CAM colors","kernel",(doc,current) => Apply(a,current), EditStage.Modeling); },
                "Cam/PartSetup", new[] { "documentId","palette","targets" }, false, Api,
                Validate, p => Preview(a,p)));
        }
        internal static JObject PlanProperties()
        {
            var target = Schema.Object(new JObject { ["element"] = Schema.Element(), ["face"] = Schema.Item() });
            var original = Schema.Object(new JObject { ["empty"] = Schema.Boolean("No explicit color."), ["r"] = Schema.Integer("Red", 0,255), ["g"] = Schema.Integer("Green",0,255), ["b"] = Schema.Integer("Blue",0,255) });
            var role = Schema.Object(new JObject { ["Key"] = Schema.Text("Stable role key.",48), ["Label"] = Schema.Text("Role label.",80), ["Hex"] = Schema.Text("Exact #RRGGBB color.",7) }, "Key","Label","Hex");
            var palette = Schema.Object(new JObject { ["Id"] = Schema.Text("Standard ID.",64), ["Version"] = Schema.Integer("Standard version.",1),
                ["Name"] = Schema.Text("Standard name.",120), ["Roles"] = Schema.Array(role,1,32) }, "Id","Version","Name","Roles");
            var input = DocumentActionTools.Target(); input["workpiece"] = Schema.Element(); input["palette"] = palette;
            input["modelingStage"] = Schema.Element();
            input["targets"] = Schema.Array(Schema.Object(new JObject { ["target"] = target, ["fingerprint"] = Schema.Text("Fingerprint from inspection.",64),
                ["originalColor"] = original, ["roleKey"] = Schema.Text("Reviewed role key.",48), ["group"] = Schema.TextValue("Reviewed group label.",80) },
                "target","fingerprint","originalColor","roleKey","group"),1,288);
            return input;
        }
        internal static readonly string[] Api = ApiRefs.Kernel("IShapes.GetFaces", "IShapes.GetFaceSurfaceType", "IShapes.GetFaceArea", "IShapes.GetFaceEnclosingCoordinates",
            "IShapes.GetFaceConnectedFaces", "IShapes.GetFaceColor", "IShapes.CreateColoringOperation", "IElements.GetElements", "IElements.GetTypeFullName",
            "IElements.IsColorModifiable", "IElements.GetColor", "IElements.SetColor", "ISketches2D.GetProfiles", "ISketches3D.GetProfiles");

        internal static void Validate(JObject input)
        {
            MutationReferences.Validate(input);
            var palette = input["palette"]?.ToObject<CamColorStandard>() ?? throw new ArgumentException("Missing color standard."); palette.Validate();
            var targets = input["targets"] as JArray ?? throw new ArgumentException("Missing color targets."); CamColorPlan.ValidateCounts(targets);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in targets.OfType<JObject>())
            {
                var target = entry["target"] as JObject;
                if (target == null || target.Count != 1 || (target["element"] == null && target["face"] == null) || !keys.Add(CamColorGeometry.Fingerprint(new JObject { ["target"] = target.DeepClone() })))
                    throw new ArgumentException("Use one unique exact element or face target per assignment.");
                if (!palette.Roles.Any(r => r.Key == (string)entry["roleKey"])) throw new ArgumentException("Unknown palette role.");
                if (!global::System.Text.RegularExpressions.Regex.IsMatch((string)entry["fingerprint"] ?? "", "^[A-F0-9]{64}$")) throw new ArgumentException("Invalid geometry fingerprint.");
            }
        }

        internal static JArray Verify(JObject input, Func<JObject, JObject> inspect)
        {
            Validate(input); var verified = new JArray();
            foreach (var entry in (JArray)input["targets"])
            {
                var row = inspect((JObject)entry["target"]);
                if ((bool?)row["colorSupported"] != true) throw new ArgumentException("This exact geometry cannot be colored: " + (string)row["supportReason"]);
                if ((string)row["fingerprint"] != (string)entry["fingerprint"] || !JToken.DeepEquals(row["color"],entry["originalColor"]))
                    throw new InvalidOperationException("Geometry or its original color changed. Analyze and review a new color plan.");
                verified.Add(row);
            }
            return verified;
        }
        private static JObject Preview(AutomationGateway a, JObject p)
        {
            var preview = a.PreviewDocument(p); var geometry = new CamColorGeometry(a,p);
            var stage = CamStages.Resolve(a,p,EditStage.Modeling);
            if (p["modelingStage"] != null && !JToken.DeepEquals(p["modelingStage"], AutomationValues.Json(stage))) throw new InvalidOperationException("The reviewed modeling stage changed. Review the CAM plan again.");
            if (!stage.IsEmpty) preview["requiredStage"] = AutomationValues.Json(stage);
            var rows = Verify(p,geometry.Read);
            preview["palette"] = p["palette"].DeepClone();
            preview["items"] = new JArray(rows.Select((row,i) => new JObject { ["name"] = row["name"], ["kind"] = row["kind"],
                ["target"] = row["target"], ["fingerprint"] = row["fingerprint"], ["originalColor"] = row["color"], ["roleKey"] = p["targets"][i]["roleKey"] }));
            return preview;
        }
        private static JObject Apply(AutomationGateway a, JObject p)
        {
            var geometry = new CamColorGeometry(a,p);
            return ApplyVerified(p,geometry.Read,
                (target,rgb)=>ElementAppearance.Set(TopSolidHost.Elements,a.Element(target),rgb),
                (targets,rgb)=> {
                    var ids=targets.Select(t=>a.Item(new JObject { ["item"]=t["face"].DeepClone() })).ToList();
                    var operation=TopSolidHost.Shapes.CreateColoringOperation(ids,ElementAppearance.Parse(rgb));
                    AutomationGateway.RequireValid(operation,"CAM coloring operation");
                    return (JObject)AutomationValues.Json(operation);
                });
        }
        // Caller owns the single native transaction. Any write/readback error escapes
        // this method so ModificationScope rolls the entire batch back.
        internal static JObject ApplyVerified(JObject p,Func<JObject,JObject> read,
            Action<JObject,JObject> setEntity,Func<List<JObject>,JObject,JObject> colorFaces)
        {
            Verify(p,read);
            var roles = p["palette"].ToObject<CamColorStandard>().Roles.ToDictionary(r => r.Key);
            var entries = ((JArray)p["targets"]).OfType<JObject>().ToArray(); var operations = new JArray();
            foreach (var entry in entries.Where(e => e["target"]["element"] != null))
                setEntity((JObject)entry["target"],roles[(string)entry["roleKey"]].Rgb);
            // The native API requires every face in one coloring operation to
            // belong to the same shape, even when their RGB values are identical.
            foreach (var group in entries.Where(e => e["target"]["face"] != null).GroupBy(e => new {
                Role = (string)e["roleKey"], Owner = CamColorGeometry.Fingerprint(new JObject { ["owner"] = e["target"]["face"]["element"].DeepClone() }) }))
            {
                operations.Add(colorFaces(group.Select(e=>(JObject)e["target"]).ToList(),roles[group.Key.Role].Rgb));
            }
            var result = new JArray();
            foreach (var entry in entries)
            {
                var row = read((JObject)entry["target"]);
                if (!JToken.DeepEquals(row["color"],roles[(string)entry["roleKey"]].Rgb)) throw new InvalidOperationException("Color readback differs; rolling back the complete color batch.");
                result.Add(new JObject { ["target"] = row["target"], ["name"] = row["name"], ["roleKey"] = entry["roleKey"], ["group"] = entry["group"],
                    ["originalColor"] = entry["originalColor"].DeepClone(), ["color"] = row["color"], ["readBackVerified"] = true });
            }
            return new JObject { ["palette"] = p["palette"].DeepClone(), ["items"] = result, ["operations"] = operations, ["readBackVerified"] = true };
        }
    }
}
