using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ShapeWorkflowTools
    {
        public static JObject Vector(string description) => Schema.Object(new JObject { ["x"] = Schema.Number(description + " X."), ["y"] = Schema.Number(description + " Y."), ["z"] = Schema.Number(description + " Z.") }, "x", "y", "z");
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            foreach (var kind in new[] { "extrude", "revolve", "loft" })
            {
                var operation = kind;
                var p = DocumentActionTools.Target();
                p["units"] = Schema.Choice("Input length units; default mm. Angles use degrees explicitly.", "mm", "cm", "m");
                p["surface"] = Schema.Boolean("Create a surface instead of a solid; default false.");
                p["name"] = Schema.Text("Optional new shape name.", 128);
                var required = new List<string> { "documentId" };
                if (kind == "loft") { p["profiles"] = Schema.Array(Schema.Item(), 2, 16); required.Add("profiles"); }
                else
                {
                    p["section"] = Schema.Item();
                    p["sketch"] = Schema.Element();
                    p["centered"] = Schema.Boolean("Center the extrusion or revolution about its start; default false.");
                    if (kind == "extrude")
                    {
                        p["direction"] = Vector("World direction vector; normalized by the server"); p["length"] = Schema.Number("Extrusion length in input units.", 0.001, 100000);
                        required.Add("direction"); required.Add("length");
                    }
                    else
                    {
                        p["axisOrigin"] = Vector("World axis origin in input units"); p["axisDirection"] = Vector("World axis direction");
                        p["angleDegrees"] = Schema.Number("Revolution angle in degrees; 360 for a full revolution.", 0.001, 360);
                        required.AddRange(new[] { "axisOrigin", "axisDirection", "angleDegrees" });
                    }
                }
                register(new ToolDefinition("topsolid_" + kind + "_sketch", "Create a native " + kind + " operation in a 3D document. Extrude/revolve take either one section handle or a sketch element (uses all its profiles, with TopSolid's implicit closure of open profiles). Loft takes profiles. Requires confirmation; does not save.",
                    p, input => a.CreateShape(operation, input), "Design3D", required.ToArray(), false,
                    ApiRefs.Kernel(kind == "extrude" ? "IShapes.CreateExtrudedShape" : kind == "revolve" ? "IShapes.CreateRevolvedShape" : "IShapes.CreateLoftedShape", "IShapes.GetShapeVolume", "ISketches2D.GetSections", "ISketches2D.GetPlane"),
                    input => { MutationReferences.Validate(input); if (operation != "loft")
                        { if ((input["section"] == null) == (input["sketch"] == null)) throw new ArgumentException("Supply exactly one explicit section or whole sketch.");
                            SpatialInput.Direction(input[operation == "extrude" ? "direction" : "axisDirection"]); } },
                    input => a.PreviewDocument(input), defaultLengthUnits: "mm",
                    defaults: kind == "loft" ? "Nonperiodic loft, no guides or end points, arc-length synchronization, 0.001 mm intersection tolerance, minimum faces, geometry simplification. No segment-to-segment matching. Surface=false." :
                        "Surface=false, centered=false. Extrusion has no draft. Supplied directions are normalized. Angles are degrees; lengths use the displayed units."));
            }
            register(new ToolDefinition("topsolid_create_through_drilling", "Create a through-hole drilling feature on an existing Design part shape. Supply an explicit right-handed drilling frame; X and Y axes must be perpendicular. Requires confirmation.",
                new JObject { ["documentId"] = Schema.Text("Target document revision."), ["shape"] = Schema.Element(), ["units"] = Schema.Choice("Length units; default mm.", "mm", "cm", "m"),
                    ["diameter"] = Schema.Number("Through-hole diameter in input units.", 0.001, 100000),
                    ["frame"] = Schema.Object(new JObject { ["origin"] = Vector("Drilling frame origin in input units"), ["xAxis"] = Vector("Drilling frame X direction"), ["yAxis"] = Vector("Drilling frame Y direction") }, "origin", "xAxis", "yAxis") },
                a.CreateThroughDrilling, "Design3D", new[] { "documentId", "shape", "diameter", "frame" }, false,
                ApiRefs.For("cad", "TopSolid.Cad.Design.Automating", "IParts.CreateDrillingOperation", "DrillingHolePrimitive", "IParts.IsPart"),
                p => { MutationReferences.Validate(p); SpatialInput.Frame((JObject)p["frame"], ModelingGeometry.Scale(p)); }, p => a.PreviewDocument(p, "cad"),
                "Add one operative through drilling feature to this part in an undoable modification. Does not generate a CAM operation or NC code; does not save.", defaultLengthUnits: "mm"));
        }
    }
}
