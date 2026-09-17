using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal static class SketchSectionOptions
    {
        // A sketch drawing does not imply a filled modeling section. The combined
        // extrusion operation explicitly requests a solid and needs its section.
        internal static bool ForModel(string operation, JObject arguments) =>
            operation == "extruded_rectangle" || (bool?)arguments["createSection"] == true;
        internal static bool ForContour(JObject arguments) => (bool?)arguments["createSection"] == true;
        internal static string Mode(JObject arguments) => (string)arguments["sectionMode"] ?? "none";
    }
}
