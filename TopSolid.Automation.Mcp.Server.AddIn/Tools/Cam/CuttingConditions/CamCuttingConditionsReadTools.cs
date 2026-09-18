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
            register(new ToolDefinition("topsolid_get_cutting_conditions_document", "Read the linked cutting-condition LIBRARY DOCUMENT only when explicitly requested. For ordinary cutting conditions, speeds, feeds or operation edits use list_cam_parameters and get_cam_parameter_value.", new JObject { ["element"] = Schema.Element() },
                p => a.Read("cam", () => AutomationValues.Result(TopSolidCamHost.Operations.GetCurrentCuttingConditionsDocument(a.Element(p)))), "Cam/CuttingConditions", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IOperations.GetCurrentCuttingConditionsDocument.html" }));
            register(new ToolDefinition("topsolid_get_cutting_conditions_abacus", "Read the linked cutting-condition LIBRARY ABACUS only when explicitly requested. For ordinary operation cutting conditions use list_cam_parameters.", new JObject { ["element"] = Schema.Element() },
                p => a.Read("cam", () => AutomationValues.Result(TopSolidCamHost.Operations.GetCurrentCuttingConditionsAbacus(a.Element(p)))), "Cam/CuttingConditions", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IOperations.GetCurrentCuttingConditionsAbacus.html" }));
            register(new ToolDefinition("topsolid_list_cutting_conditions_documents", "List available cutting-condition LIBRARY DOCUMENTS only when explicitly requested. An empty list does not mean the operation has no cutting conditions; inspect list_cam_parameters instead.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("cam", () => AutomationValues.Page(TopSolidCamHost.Operations.GetAllCuttingConditionsDocuments(a.Element(p)), p)), "Cam/CuttingConditions", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IOperations.GetAllCuttingConditionsDocuments.html" }));
        }
    }
}
