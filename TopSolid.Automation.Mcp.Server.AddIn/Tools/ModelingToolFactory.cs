using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ModelingToolFactory
    {
        private const string Root = "https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.";
        public static ToolDefinition Create(AutomationGateway a, string operation)
        {
            if (operation != "rectangle2d" && operation != "circle2d" && operation != "polyline3d" && operation != "extruded_rectangle") throw new ArgumentException("Unknown modeling operation.");
            var properties = new JObject
            {
                ["documentId"] = Schema.Text("Explicit document revision ID to modify. Always obtain from a document tool first."),
                ["units"] = Schema.Choice("Input length units, default mm. Converted to SI metres.", "mm", "cm", "m"),
                ["name"] = Schema.Text("Optional name of the new sketch.", 128)
            };
            var required = new JArray("documentId");
            if (operation == "polyline3d")
            {
                properties["points"] = Schema.Array(Schema.Object(new JObject { ["x"] = Schema.Number("X coordinate."), ["y"] = Schema.Number("Y coordinate."), ["z"] = Schema.Number("Z coordinate.") }, "x", "y", "z"), 2, 128);
                properties["closed"] = Schema.Boolean("Close the last point to the first; default false. Do not repeat the first point.");
                required.Add("points");
            }
            else
            {
                properties["x"] = Schema.Number("Origin X in input units, default 0.");
                properties["y"] = Schema.Number("Origin Y in input units, default 0.");
                properties["z"] = Schema.Number("Origin Z in input units, default 0. Use 0 for placement=2d.");
                if (operation != "extruded_rectangle") {
                    properties["placement"] = Schema.Choice("2d for a 2D document; xy, xz or yz for a 2D sketch in a 3D document.", "2d", "xy", "xz", "yz"); required.Add("placement");
                    properties["createSection"] = Schema.Boolean("Default false: draw a sketch/profile only. Set true only when the user explicitly requests a section or a modeling operation needs one.");
                    properties["createSection"]["default"] = false;
                }
                foreach (var dimension in operation == "circle2d" ? new[] { "radius" } : operation == "extruded_rectangle" ? new[] { "width", "height", "depth" } : new[] { "width", "height" })
                { properties[dimension] = Schema.Number("Positive " + dimension + " in input units.", 0.001, 100000); required.Add(dimension); }
            }
            var category = operation == "polyline3d" ? "Sketch3D" : operation == "extruded_rectangle" ? "Design3D" : "Sketch2D";
            var service = operation == "polyline3d" ? "ISketches3D" : "ISketches2D";
            var apis = new List<string>(ApiRefs.Kernel("IApplication.StartModification", "IApplication.EndModification", "IDocuments.EnsureIsDirty"));
            foreach (var method in new[] { "StartModification", "EndModification", "CreateVertex", "CreateProfile", "CreateBuildingOperation" }) apis.Add(Root + service + "." + method + ".html");
            if (operation == "polyline3d") { apis.Add(Root + service + ".CreateSketch.html"); apis.Add(Root + service + ".CleanSketch.html"); }
            else { apis.Add(Root + service + ".CreateSketchIn2D.html"); apis.Add(Root + service + ".CreateSketchIn3D.html"); apis.Add(Root + service + ".CreateSection.html"); }
            apis.Add(Root + service + (operation == "circle2d" ? ".CreateCircleSegment.html" : ".CreateLineSegment.html"));
            if (operation == "extruded_rectangle") apis.Add(Root + "IShapes.CreateExtrudedShape.html");
            return new ToolDefinition("topsolid_create_" + operation,
                "Create native " + operation.Replace('_', ' ') + (category == "Sketch2D" ? ". Draws a sketch/profile without a section by default" : "") + ". Requires explicit user confirmation of a server-prepared preview. Target document must already exist. Does not save.",
                properties, p => a.CreateModel(operation, p), category, required.ToObject<string[]>(), false, apis.ToArray(), p => ModelingGeometry.Validate(operation, p),
                defaults: "Omitted x/y/z are 0. Omitted closed and createSection are false. Sketch drawings do not create sections by default. The combined rectangle extrusion creates its required section and extrudes along +Z from XY.", defaultLengthUnits: "mm");
        }
    }
}
