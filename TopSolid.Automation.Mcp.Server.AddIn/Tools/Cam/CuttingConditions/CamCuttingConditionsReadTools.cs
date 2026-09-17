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
    internal static class CamCuttingConditionsReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_get_cutting_conditions_document", "Get cutting conditions document.", new JObject { ["element"] = Schema.Element() },
                p => a.Read("cam", () => AutomationValues.Result(TopSolidCamHost.Operations.GetCurrentCuttingConditionsDocument(a.Element(p)))), "Cam/CuttingConditions", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IOperations.GetCurrentCuttingConditionsDocument.html" }));
            register(new ToolDefinition("topsolid_get_cutting_conditions_abacus", "Get cutting conditions abacus.", new JObject { ["element"] = Schema.Element() },
                p => a.Read("cam", () => AutomationValues.Result(TopSolidCamHost.Operations.GetCurrentCuttingConditionsAbacus(a.Element(p)))), "Cam/CuttingConditions", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IOperations.GetCurrentCuttingConditionsAbacus.html" }));
            register(new ToolDefinition("topsolid_list_cutting_conditions_documents", "List cutting conditions documents.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("cam", () => AutomationValues.Page(TopSolidCamHost.Operations.GetAllCuttingConditionsDocuments(a.Element(p)), p)), "Cam/CuttingConditions", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IOperations.GetAllCuttingConditionsDocuments.html" }));
        }
    }
}
