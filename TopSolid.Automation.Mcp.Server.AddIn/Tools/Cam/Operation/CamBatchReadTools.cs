using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Cam.NC.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class CamBatchReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_cam_operation_summaries", "List CAM operations with description, update state, tool and part in one request. Does not calculate, simulate, postprocess or produce NC. Follow nextOffset for remaining rows.",
                BatchRead.Paging(new JObject { ["documentId"] = Schema.Text("CAM document revision; default active.") }),
                p => a.Read("cam", () => BatchRead.Page(TopSolidCamHost.Operations.GetOperations(a.Document(p)).Select(id => new ElementExId(id)), p,
                    id => new JObject { ["operation"] = AutomationValues.Json(id) }, id => new JObject {
                        ["description"] = TopSolidCamHost.Operations.GetDescription(id), ["upToDate"] = TopSolidCamHost.Operations.IsUpToDate(id),
                        ["tool"] = AutomationValues.Json(TopSolidCamHost.Operations.GetTool(id)), ["part"] = AutomationValues.Json(TopSolidCamHost.Operations.GetPart(id)) })),
                "Cam/Operation", api: ApiRefs.Cam("IOperations.GetOperations", "IOperations.GetDescription", "IOperations.IsUpToDate", "IOperations.GetTool", "IOperations.GetPart")));
        }
    }
}
