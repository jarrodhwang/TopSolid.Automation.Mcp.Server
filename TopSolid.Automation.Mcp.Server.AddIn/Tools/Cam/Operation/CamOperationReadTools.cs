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
    internal static class CamOperationReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_cam_operations", "List cam operations.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("cam", () => AutomationValues.Page(TopSolidCamHost.Operations.GetOperations(a.Document(p)), p)), "Cam/Operation", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IOperations.GetOperations.html" }));
            register(new ToolDefinition("topsolid_list_cam_scenario", "List cam scenario.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("cam", () => AutomationValues.Page(TopSolidCamHost.Operations.GetScenarioOperations(a.Document(p)), p)), "Cam/Operation", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IOperations.GetScenarioOperations.html" }));
            register(new ToolDefinition("topsolid_list_cam_parameters", "List cam parameters.", Schema.Page(new JObject { ["element"] = Schema.Element(), ["preparationId"] = Schema.Text("Only for a preparation ID returned by CAM; use instead of element.", 36) }),
                p => a.Read("cam", () => AutomationValues.Page(TopSolidCamHost.Parameters.GetParameters(a.CamElement(p)), p)), "Cam/Operation", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IParameters.GetParameters.html" }));
        }
    }
}
