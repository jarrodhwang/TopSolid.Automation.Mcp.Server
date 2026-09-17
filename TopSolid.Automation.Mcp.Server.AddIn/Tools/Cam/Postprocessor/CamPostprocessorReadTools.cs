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
    internal static class CamPostprocessorReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_get_postprocessor_id", "Get postprocessor id.", new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") },
                p => a.Read("cam", () => AutomationValues.Result(TopSolidCamHost.NCPostProcessor.GetNCPostProcessorId(a.Document(p)))), "Cam/Postprocessor", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.INCPostProcessor.GetNCPostProcessorId.html" }));
        }
    }
}
