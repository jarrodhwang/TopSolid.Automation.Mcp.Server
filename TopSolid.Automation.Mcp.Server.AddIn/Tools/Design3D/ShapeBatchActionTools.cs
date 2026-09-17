using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ShapeBatchActionTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            foreach (var kind in new[] { "extrude", "revolve" })
            {
                var operation = kind;
                var feature = new JObject { ["sketch"] = Schema.Element(), ["section"] = Schema.Item(), ["name"] = Schema.Text("Optional shape name.", 128),
                    ["surface"] = Schema.Boolean("Surface instead of solid; default false."), ["centered"] = Schema.Boolean("Center about start; default false.") };
                if (kind == "extrude") { feature["direction"] = ShapeWorkflowTools.Vector("World extrusion direction"); feature["length"] = Schema.Number("Extrusion length in input units.", 0.001, 100000); }
                else { feature["axisOrigin"] = ShapeWorkflowTools.Vector("World axis origin in input units"); feature["axisDirection"] = ShapeWorkflowTools.Vector("World axis direction"); feature["angleDegrees"] = Schema.Number("Angle in degrees; 360 for full revolution.", 0.001, 360); }
                var props = DocumentActionTools.Target(); props["units"] = Schema.Choice("Length units; default mm.", "mm", "cm", "m");
                props["features"] = Schema.Array(Schema.Object(feature, kind == "extrude" ? new[] { "direction", "length" } : new[] { "axisOrigin", "axisDirection", "angleDegrees" }), 1, 16);
                register(new ToolDefinition("topsolid_" + kind + "_sections", "Create up to 16 native " + kind + " features in ONE confirmed modification, from existing sketch/section handles in one 3D document. Each entry takes exactly one sketch or section. Entire batch attempts rollback if any feature fails. Does not save.",
                    props, p => a.CreateShapeBatch(operation, p), "Design3D", new[] { "documentId", "features" }, false,
                    ApiRefs.Kernel(kind == "extrude" ? "IShapes.CreateExtrudedShape" : "IShapes.CreateRevolvedShape", "IShapes.GetShapeVolume", "ISketches2D.GetPlane", "ISketches2D.GetSections", "ISketches2D.GetProfileCount", "IElements.SetName"),
                    p => Validate(p, operation), p => a.PreviewDocument(p), defaultLengthUnits: "mm",
                    defaults: "Creates separate shapes, with no automatic Boolean union. surface=false; centered=false. No extrusion draft. Directions normalized; angles in degrees. A whole sketch uses all profiles with the API's implicit closure rules."));
            }
        }
        internal static void Validate(JObject p, string operation)
        {
            MutationReferences.Validate(p);
            foreach (JObject feature in p["features"])
            {
                if ((feature["sketch"] == null) == (feature["section"] == null)) throw new ArgumentException("Each feature needs exactly one sketch or section.");
                SpatialInput.Direction(feature[operation == "extrude" ? "direction" : "axisDirection"]);
            }
        }
    }
}
