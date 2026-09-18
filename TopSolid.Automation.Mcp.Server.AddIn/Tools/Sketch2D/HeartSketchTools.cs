using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class HeartSketchTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_create_heart_sketch",
                "Draw one smooth closed heart sketch using six server-computed cubic curves. No sections. Width/height are exact bounds before rotation; center is the bounding-box center, notch faces local +Y. Supply size, principal plane and world center; all lengths are mm. For a linked/reference sketch use create_sketches2d with kind=heart instead. One confirmation; no save.",
                new JObject { ["documentId"] = Schema.Text("Current document revision from a live tool."),
                    ["name"] = Schema.Text(CreationNames.Description, 128),
                    ["placement"] = Schema.Choice("2d for native 2D; xy/xz/yz for a sketch in a part.", "2d", "xy", "xz", "yz"),
                    ["width"] = Schema.Number("Heart width, mm.", .001, 100000), ["height"] = Schema.Number("Heart height, mm.", .001, 100000),
                    ["center"] = SketchPlanSchema.Point3("World center in mm; z=0 for a native 2D document."),
                    ["rotationDegrees"] = Schema.Number("In-plane rotation; 0 puts the notch toward local +Y.", -360, 360) },
                p => a.CreateSketchProfiles(Expand(p)), "Sketch2D", new[] { "documentId", "placement", "width", "height", "center", "rotationDegrees" }, false,
                SketchPlanTools.Api, p => SketchBatchActionTools.ValidateProfiles(Expand(p)), p => a.PreviewSketchProfiles(Expand(p)),
                defaultLengthUnits: "mm", defaults: "No inferred dimensions; no sections or save.", effect: "Draw one closed decorative heart in the chosen document. Six cubic segments, one closed profile, no section."));
        }
        internal static JObject Expand(JObject p)
        {
            var result = new JObject { ["documentId"] = p["documentId"].DeepClone(), ["units"] = "mm",
                ["documentSpace"] = (string)p["placement"] == "2d" ? "2d" : "3d", ["placement"] = p["placement"].DeepClone(),
                ["origin"] = p["center"].DeepClone(), ["rotationDegrees"] = p["rotationDegrees"].DeepClone(),
                ["profiles"] = new JArray(new JObject { ["kind"] = "heart", ["width"] = p["width"].DeepClone(), ["height"] = p["height"].DeepClone(),
                    ["center"] = new JObject { ["x"] = 0, ["y"] = 0 }, ["rotationDegrees"] = 0 }) };
            if (p["name"] != null) result["name"] = p["name"].DeepClone();
            return result;
        }
    }
}
