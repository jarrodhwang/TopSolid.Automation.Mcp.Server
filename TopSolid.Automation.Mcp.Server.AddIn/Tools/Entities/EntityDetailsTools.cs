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
            register(new ToolDefinition("topsolid_get_parameter_value", "Read a typed parameter entity value, including enums, color, date/time, tolerance, code and family. Unset values are explicit. Use inspect_parameters for editing context.",
                new JObject { ["element"] = Schema.Element() }, p => a.Read("kernel", () => Value(a.Element(p))), "Parameters", new[] { "element" }, true,
                ParameterValues.ReadApi));
        }
        internal static JObject Value(ElementId id) => ParameterValues.Native.Read(id);
    }
}
