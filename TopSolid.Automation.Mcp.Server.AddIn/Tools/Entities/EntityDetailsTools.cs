using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class EntityDetailsTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_get_element_info", "Read an element's name, display name, type, owner and invalid state.", new JObject { ["element"] = Schema.Element() },
                p => a.Read("kernel", () =>
                {
                    var id = a.Element(p);
                    return EntityBatchReadTools.Info(id);
                }), "Entities", new[] { "element" }, true, EntityBatchReadTools.InfoApi));
            register(new ToolDefinition("topsolid_get_parameter_value", "Read a scalar document parameter by its element ID. Reports unsupported types explicitly; real values are SI with unit metadata.",
                new JObject { ["element"] = Schema.Element() }, p => a.Read("kernel", () => Value(a.Element(p))), "Entities", new[] { "element" }, true,
                ApiRefs.Kernel("IParameters.GetParameterType", "IParameters.GetRealValue", "IParameters.GetRealUnit", "IParameters.GetIntegerValue", "IParameters.GetBooleanValue", "IParameters.GetTextValue", "IParameters.GetDateTimeValue", "IParameters.GetEnumerationText", "IParameters.GetUserEnumerationText")));
        }
        internal static JObject Value(ElementId id)
        {
            var type = TopSolidHost.Parameters.GetParameterType(id);
            var result = new JObject { ["element"] = AutomationValues.Json(id), ["type"] = type.ToString(), ["supported"] = true };
            object value;
            switch (type)
            {
                case ParameterType.Real:
                    value = TopSolidHost.Parameters.GetRealValue(id);
                    TopSolidHost.Parameters.GetRealUnit(id, out var unit, out var symbol);
                    result["unitType"] = unit.ToString(); result["unitSymbol"] = symbol; result["valueConvention"] = "SI"; break;
                case ParameterType.Integer: value = TopSolidHost.Parameters.GetIntegerValue(id); break;
                case ParameterType.Boolean: value = TopSolidHost.Parameters.GetBooleanValue(id); break;
                case ParameterType.Text: value = TopSolidHost.Parameters.GetTextValue(id); result["localized"] = false; break;
                case ParameterType.DateTime: value = TopSolidHost.Parameters.GetDateTimeValue(id); break;
                case ParameterType.Enumeration: value = TopSolidHost.Parameters.GetEnumerationText(id); break;
                case ParameterType.UserEnumeration: value = TopSolidHost.Parameters.GetUserEnumerationText(id); break;
                default: result["supported"] = false; result["message"] = "This parameter type has no value adapter yet."; return result;
            }
            result["value"] = AutomationValues.Json(value); return result;
        }
    }
}
