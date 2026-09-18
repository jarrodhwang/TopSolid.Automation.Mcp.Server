using System;
using System.Linq;
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
                    return CamNames.Operation(id);
                }), "Cam/Operation", new[] { "element" }, true, ApiRefs.Cam("IOperations.IsOperation", "IOperations.GetDescription", "IOperations.IsUpToDate", "IOperations.GetTool", "IOperations.GetPart", "ITools.GetParameters", "IParameters.ToInvariantStringValue").Concat(ApiRefs.Kernel("IElements.GetFriendlyName", "IElements.GetName")).ToArray()));
            register(new ToolDefinition("topsolid_get_cam_parameter_value", "Inspect one parameter inside a CAM operation: friendly name, native type, value, SI unit type, read-only state, allowed enum values and edit guidance; composites include their bound/feed/spindle values. Use the exact full name from list_cam_parameters. Cutting conditions normally refer to these operation parameters.",
                new JObject { ["element"] = Schema.Element(), ["preparationId"] = Schema.Text("Alternative preparation ID.", 36), ["name"] = Schema.Text("Exact parameter name.", 512) },
                p => a.Read("cam", () => { var access = CamParameterValues.Native; return access.Read(access.Resolve(a.CamElement(p), (string)p["name"])); }),
                "Cam/Operation", new[] { "name" }, true, CamParameterValues.ReadApi));
        }
    }
}
