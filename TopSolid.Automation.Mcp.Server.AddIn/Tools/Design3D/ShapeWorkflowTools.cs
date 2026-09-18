using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ShapeWorkflowTools
    {
        internal static readonly string[] DimensionApi = ApiRefs.Kernel("SmartReal.-ctor", "IParameters.GetParameterType", "IParameters.HasValue", "IParameters.GetRealUnit", "IParameters.GetRealValue", "IElements.GetName", "IElements.Exists", "IElements.IsInvalid");
        internal static void DimensionSchema(JObject p, string literal, string reference, string description, double max)
        {
            p[literal] = Schema.Number(description + " Absolute value by default; omit when using the parameter alternative.", .001, max);
            p[reference] = Schema.Element(); p[reference]["description"] = "Existing Real " + (literal == "angleDegrees" ? "Angle" : "Length") + " parameter handle instead of " + literal + ". Preserves a native dependency. Use only when parameter-driven modeling is requested.";
        }
        public static JObject Vector(string description) => Schema.Object(new JObject { ["x"] = Schema.Number(description + " X."), ["y"] = Schema.Number(description + " Y."), ["z"] = Schema.Number(description + " Z.") }, "x", "y", "z");
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            foreach (var kind in new[] { "extrude", "revolve", "loft" })
            {
                var operation = kind;
                var p = DocumentActionTools.Target();
                p["units"] = Schema.Choice("Input length units; default mm. Angles use degrees explicitly.", "mm", "cm", "m");
                p["surface"] = Schema.Boolean("Create a surface instead of a solid; default false.");
                p["name"] = Schema.Text(CreationNames.Description, 128);
                p["color"] = AppearanceTools.ColorSchema();
                var required = new List<string> { "documentId" };
                if (kind == "loft") { p["profiles"] = Schema.Array(Schema.Item(), 2, 16); required.Add("profiles"); }
                else
                {
                    p["section"] = Schema.Item();
                    p["sketch"] = Schema.Element();
                    p["centered"] = Schema.Boolean("Center the extrusion or revolution about its start; default false.");
                    if (kind == "extrude")
                    {
                        p["direction"] = Vector("World direction vector; normalized by the server"); DimensionSchema(p, "length", "lengthParameter", "Extrusion length in input units.", 100000);
                        required.Add("direction");
                    }
                    else
                    {
                        p["axisOrigin"] = Vector("World axis origin in input units"); p["axisDirection"] = Vector("World axis direction");
                        DimensionSchema(p, "angleDegrees", "angleParameter", "Revolution angle in degrees; 360 for a full revolution.", 360);
                        required.AddRange(new[] { "axisOrigin", "axisDirection" });
                    }
                }
                register(new ToolDefinition("topsolid_" + kind + "_sketch", "Create a native " + kind + " operation in a 3D document. Extrude/revolve prefer the ORIGINAL sketch element (uses its profiles directly). No section creation or redraw is needed. Existing section handles are also accepted. Loft takes profiles. Requires confirmation; does not save.",
                    p, input => a.CreateShape(operation, input), "Design3D", required.ToArray(), false,
                    ApiRefs.Kernel(kind == "extrude" ? "IShapes.CreateExtrudedShape" : kind == "revolve" ? "IShapes.CreateRevolvedShape" : "IShapes.CreateLoftedShape", "IShapes.GetShapeVolume", "SmartSection3D.-ctor", "ISketches2D.GetSections", "ISketches2D.GetSectionProfiles", "ISketches2D.GetPlane", "ISketches2D.GetProfiles", "ISketches2D.IsProfileClosed").Concat(DimensionApi).Concat(AppearanceTools.Api).ToArray(),
                    input => { MutationReferences.Validate(input); ElementAppearance.Validate(input); if (operation != "loft")
                        { if ((input["section"] == null) == (input["sketch"] == null)) throw new ArgumentException("Supply exactly one explicit section or whole sketch.");
                            FeatureDimension.Validate(input, operation == "extrude" ? "length" : "angleDegrees", operation == "extrude" ? "lengthParameter" : "angleParameter");
                            SpatialInput.Direction(input[operation == "extrude" ? "direction" : "axisDirection"]); } },
                    input => a.PreviewShape(operation, input), defaultLengthUnits: "mm",
                    defaults: kind == "loft" ? "Nonperiodic loft, no guides or end points, arc-length synchronization, 0.001 mm intersection tolerance, minimum faces, geometry simplification. No segment-to-segment matching. Surface=false." :
                        "Surface=false, centered=false. Extrusion has no draft. Supplied directions are normalized. Angles are degrees; lengths use the displayed units."));
            }
            register(new ToolDefinition("topsolid_create_through_drilling", "Create a through-hole drilling feature on an existing Design part shape. Supply an explicit right-handed drilling frame; X and Y axes must be perpendicular. Requires confirmation.",
                new JObject { ["documentId"] = Schema.Text("Target document revision."), ["shape"] = Schema.Element(), ["units"] = Schema.Choice("Length units; default mm.", "mm", "cm", "m"),
                    ["diameter"] = Schema.Number("Through-hole diameter in input units.", 0.001, 100000),
                    ["diameterParameter"] = Schema.Element(),
                    ["frame"] = Schema.Object(new JObject { ["origin"] = Vector("Drilling frame origin in input units"), ["xAxis"] = Vector("Drilling frame X direction"), ["yAxis"] = Vector("Drilling frame Y direction") }, "origin", "xAxis", "yAxis") },
                a.CreateThroughDrilling, "Design3D", new[] { "documentId", "shape", "frame" }, false,
                ApiRefs.For("cad", "TopSolid.Cad.Design.Automating", "IParts.CreateDrillingOperation", "DrillingHolePrimitive", "IParts.IsPart").Concat(ApiRefs.Kernel("IShapes.GetShapes")).Concat(DimensionApi).ToArray(),
                p => { MutationReferences.Validate(p); FeatureDimension.Validate(p, "diameter", "diameterParameter"); SpatialInput.Frame((JObject)p["frame"], ModelingGeometry.Scale(p)); }, a.PreviewThroughDrilling,
                "Add one operative through drilling feature to this part in an undoable modification. Does not generate a CAM operation or NC code; does not save.", defaultLengthUnits: "mm"));
        }
    }
}
