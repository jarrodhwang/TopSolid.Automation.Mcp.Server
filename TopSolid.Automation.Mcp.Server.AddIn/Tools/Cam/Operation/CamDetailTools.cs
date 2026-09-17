using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Cam.NC.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class CamDetailTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_get_cam_operation_info", "Read an existing CAM operation's description, update state, tool and part. Does not calculate a toolpath.",
                new JObject { ["element"] = Schema.Element() }, p => a.Read("cam", () =>
                {
                    var id = a.CamElement(p);
                    if (!TopSolidCamHost.Operations.IsOperation(id)) throw new ArgumentException("This element is not a CAM operation.");
                    return new JObject { ["operation"] = AutomationValues.Json(id), ["description"] = TopSolidCamHost.Operations.GetDescription(id),
                        ["upToDate"] = TopSolidCamHost.Operations.IsUpToDate(id), ["tool"] = AutomationValues.Json(TopSolidCamHost.Operations.GetTool(id)),
                        ["part"] = AutomationValues.Json(TopSolidCamHost.Operations.GetPart(id)) };
                }), "Cam/Operation", new[] { "element" }, true, ApiRefs.Cam("IOperations.IsOperation", "IOperations.GetDescription", "IOperations.IsUpToDate", "IOperations.GetTool", "IOperations.GetPart")));
            register(new ToolDefinition("topsolid_get_cam_parameter_value", "Read a named CAM parameter. Use the exact parameter name returned by list_cam_parameters; smart values include their API type.",
                new JObject { ["element"] = Schema.Element(), ["preparationId"] = Schema.Text("Alternative preparation ID.", 36), ["name"] = Schema.Text("Exact parameter name.", 512) },
                p => a.Read("cam", () => AutomationValues.Result(TopSolidCamHost.Parameters.GetNamedValue(a.CamElement(p), (string)p["name"]))),
                "Cam/Operation", new[] { "name" }, true, ApiRefs.Cam("IParameters.GetNamedValue")));
        }
    }
}
