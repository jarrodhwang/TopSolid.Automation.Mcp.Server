using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;
using TopSolid.Cam.NC.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class CamActionTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            var target = DocumentActionTools.Target(); target["element"] = Schema.Element();
            register(new ToolDefinition("topsolid_execute_cam_operation", "Calculate/execute one existing CAM operation inside TopSolid after explicit confirmation. Does not generate or export NC code, start a machine, or assert collision safety.", target,
                p => a.Modify(p, "calculate CAM operation", "cam", (doc, current) =>
                {
                    var id = a.Element(current); var extended = new ElementExId(id);
                    if (!TopSolidCamHost.Operations.IsOperation(extended)) throw new ArgumentException("Select an existing CAM operation.");
                    TopSolidCamHost.Operations.Execute(id);
                    AutomationGateway.RequireValid(id, "CAM operation");
                    var updated = TopSolidCamHost.Operations.IsUpToDate(extended);
                    if (!updated) throw new InvalidOperationException("The CAM operation did not become up to date.");
                    return new JObject { ["operation"] = AutomationValues.Json(id), ["upToDate"] = updated, ["ncGenerated"] = false, ["collisionSafetyVerified"] = false };
                }), "Cam/Operation", new[] { "documentId", "element" }, false,
                ApiRefs.Cam("IOperations.Execute", "IOperations.IsOperation", "IOperations.IsUpToDate"), MutationReferences.Validate, p => a.PreviewDocument(p, "cam"),
                "Calculate the selected existing CAM operation and update the document. This may take time. No NC output or machine execution. Does not save."));
            var parameters = DocumentActionTools.Target(); parameters["element"] = Schema.Element(); parameters["name"] = Schema.Text("Exact parameter ID Name returned by list_cam_parameters, including its categories.", 512); parameters.Merge(ScalarInput.Properties());
            parameters["textValue"] = Schema.TextValue("New CAM text value; an empty string clears the value.", 4096);
            register(new ToolDefinition("topsolid_set_cam_parameter_value", "Change one parameter INSIDE an existing CAM operation, including scalar cutting conditions, strategy and tool settings. First inspect list_cam_parameters or get_cam_parameter_value for exact name, valueType, unitType, readOnly and native enum choices. Real values use SI; never guess Length for cutting speed. Composite Bound/FeedRate/SpindleRate/Tool/Geometry types have no documented setter and are rejected. Requires confirmation.", parameters,
                p => a.Modify(p, "set CAM parameter", "cam", (doc, current) =>
                {
                    var operation = a.CamElement(current); var access = CamParameterValues.Native;
                    var parameter = access.Resolve(operation, (string)current["name"]);
                    var result = access.Set(parameter, current);
                    AutomationGateway.RequireValid(a.Element(current), "CAM parameter owner");
                    result["operationName"] = CamNames.OperationName(operation); result["ncGenerated"] = false; result["recalculationMayBeRequired"] = true;
                    return result;
                }), "Cam/Operation", new[] { "documentId", "element", "name", "valueType" }, false,
                CamParameterValues.ReadApi.Concat(ApiRefs.Cam("IParameters.SetValue", "IOperations.GetNCOperation")).Concat(ApiRefs.Kernel("IElements.GetFriendlyName", "IElements.GetName", "IElements.GetTypeFullName", "IElements.IsInvalid")).ToArray(), ScalarInput.Validate,
                p => {
                    var preview = a.PreviewDocument(p, "cam"); var operation = a.CamElement(p); var access = CamParameterValues.Native;
                    var parameter = access.Resolve(operation, (string)p["name"]); var value = access.Preflight(parameter, p, out var before);
                    preview["operationName"] = CamNames.OperationName(operation); preview["parameterName"] = before["displayName"].DeepClone();
                    CamNames.DescribeOperation(preview, operation);
                    preview["parameter"] = before; preview["currentValue"] = before["displayValue"]?.DeepClone(); preview["proposedValue"] = AutomationValues.Json(value);
                    preview["valueType"] = before["valueType"].DeepClone(); preview["unitType"] = before["unitType"]?.DeepClone();
                    preview["replacesDefinition"] = (string)before["smartType"] != "Basic";
                    return preview;
                },
                "Change this scalar CAM parameter and update the document. Recalculation may be required. Does not generate NC code or save."));
        }
    }
}
