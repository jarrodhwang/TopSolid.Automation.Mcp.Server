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
            register(new ToolDefinition("topsolid_set_cam_parameter_value", "Set one existing scalar CAM parameter using its exact listed ID name and type. No partial-name matching. Bound/feedrate/spindle/tool/geometry parameter types need dedicated adapters and are rejected. Requires confirmation.", parameters,
                p => a.Modify(p, "set CAM parameter", "cam", (doc, current) =>
                {
                    var parameter = Resolve(a, current);
                    var type = TopSolidCamHost.Parameters.GetType(parameter).ToString();
                    if (type != (string)current["valueType"]) throw new ArgumentException("CAM parameter type does not match valueType.");
                    var before = TopSolidCamHost.Parameters.GetValue(parameter);
                    SmartObject value;
                    switch (type)
                    {
                        case "Real":
                            if (!(before is SmartReal real) || real.UnitType == UnitType.None || real.UnitType.ToString() != (string)current["unitType"])
                                throw new ArgumentException("Query the real parameter's concrete unit type before changing it.");
                            value = new SmartReal(real.UnitType, (double)current["realValueSI"]); break;
                        case "Integer": value = new SmartInteger((int)current["integerValue"]); break;
                        case "Boolean": value = new SmartBoolean((bool)current["booleanValue"]); break;
                        case "Text": value = new SmartText((string)current["textValue"]); break;
                        default: throw new ArgumentException("Only scalar Real, Integer, Boolean and Text CAM parameters can be changed here.");
                    }
                    var changed = TopSolidCamHost.Parameters.SetValue(parameter, value);
                    return new JObject { ["parameter"] = AutomationValues.Json(parameter), ["changed"] = changed,
                        ["value"] = AutomationValues.Json(TopSolidCamHost.Parameters.GetValue(parameter)), ["ncGenerated"] = false };
                }), "Cam/Operation", new[] { "documentId", "element", "name", "valueType" }, false,
                ApiRefs.Cam("IParameters.SetValue", "IParameters.GetParameters", "IParameters.GetType", "IParameters.GetValue"), ScalarInput.Validate,
                p => { var preview = a.PreviewDocument(p, "cam"); var parameter = Resolve(a, p); preview["parameterName"] = TopSolidCamHost.Parameters.GetFullName(parameter); preview["currentValue"] = AutomationValues.Json(TopSolidCamHost.Parameters.GetValue(parameter)); return preview; },
                "Change this scalar CAM parameter and update the document. Recalculation may be required. Does not generate NC code or save."));
        }
        private static ParameterId Resolve(AutomationGateway a, JObject p)
        {
            var matches = TopSolidCamHost.Parameters.GetParameters(new ElementExId(a.Element(p))).Where(v => v.Name == (string)p["name"]).ToList();
            if (matches.Count != 1) throw new ArgumentException("Use one exact parameter ID name returned by list_cam_parameters. Partial names and unknown parameters are rejected.");
            return matches[0];
        }
    }
}
