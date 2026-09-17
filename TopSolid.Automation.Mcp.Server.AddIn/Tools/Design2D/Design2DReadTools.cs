// Generated from the reviewed allowlist in scripts/ApiReference/generate_read_tools.py.
// Each call is compiled against the matched 7.20 SDK. No generic API invocation.
using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;
using TopSolid.Cad.Design.Automating;
using TopSolid.Cad.Drafting.Automating;
using TopSolid.Cad.Electrode.Automating;
using TopSolid.Cam.NC.Kernel.Automating;
using TopSolid.Cae.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class Design2DReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_points2d", "List points2d.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Geometries2D.GetPoints(a.Document(p)), p)), "Design2D", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IGeometries2D.GetPoints.html" }));
            register(new ToolDefinition("topsolid_get_point2d", "Read point coordinates in metres.", new JObject { ["element"] = Schema.Element() },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Geometries2D.GetPointGeometry(a.Element(p)))), "Design2D", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IGeometries2D.GetPointGeometry.html" }));
        }
    }
}
