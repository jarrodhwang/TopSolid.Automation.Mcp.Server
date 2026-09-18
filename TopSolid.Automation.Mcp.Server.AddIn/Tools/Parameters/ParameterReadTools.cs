using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ParameterReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_inspect_parameters", "Inspect up to 100 parameter entities with typed values, names, units, parent operations, relay sources and edit guidance in one call. Optional includeDefinition/includeConstraints add native details. Use exact parameter ElementIds, not creation-operation or CAM ParameterId handles.",
                BatchRead.Paging(new JObject { ["elements"] = Schema.Array(Schema.Element(), 1, 100), ["includeDefinition"] = Schema.Boolean("Read supported smart creation definitions from parent operations; default false."), ["includeConstraints"] = Schema.Boolean("Read native real parameter constraints; default false.") }),
                p => a.Read("kernel", () => BatchRead.Page((JArray)p["elements"], p, v => new JObject { ["element"] = v.DeepClone() }, v => {
                    var id = a.Element(new JObject { ["element"] = v.DeepClone() }); var row = ParameterValues.Native.Read(id, true, (bool?)p["includeConstraints"] == true);
                    if ((bool?)p["includeDefinition"] == true && (bool?)row["supported"] == true) row["definition"] = ParameterExpressionTools.Inspect(id);
                    return row;
                })), "Parameters", new[] { "elements" }, api: ParameterValues.ReadApi.Concat(ParameterValues.ConstraintApi).Concat(ParameterExpressionTools.ReadApi).ToArray()));
            register(new ToolDefinition("topsolid_get_parameter_choices", "Read native enumeration keys/texts or Real/Color/Code possible values with pagination. Keys are not ordinal positions. Honors user-enumeration parameter restrictions. Empty non-strict choices do not mean no values are valid.",
                BatchRead.Paging(new JObject { ["element"] = Schema.Element() }), p => a.Read("kernel", () => ParameterValues.Native.Choices(a.Element(p), p)), "Parameters", new[] { "element" },
                api: ParameterValues.ReadApi.Concat(ParameterValues.ChoicesApi).ToArray()));
        }
    }
}
