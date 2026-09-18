using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ElementPropertyTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register) => register(new ToolDefinition("topsolid_read_element_properties",
            "Read an element's property names, localized labels, types and scalar values in one paginated call. Optional names selects exact full names. These are element properties, not IParameters entities; this interface has no generic property setter.",
            BatchRead.Paging(new JObject { ["element"] = Schema.Element(), ["names"] = Schema.Array(Schema.Text("Exact property full name.", 512), 1, 100) }),
            p => a.Read("kernel", () => {
                var id = a.Element(p); var names = p["names"] is JArray selected ? selected.Select(v => (string)v).ToList() : TopSolidHost.Elements.GetProperties(id);
                var page = BatchRead.Page(names, p, name => new JObject { ["fullName"] = name }, name => Value(id, name));
                page["element"] = AutomationValues.Json(id); page["propertyOwnerKind"] = "ElementId"; return page;
            }), "Entities", new[] { "element" }, api: ApiRefs.Kernel("IElements.GetProperties", "IElements.GetPropertyType", "IElements.GetPropertyLocalizedName", "IElements.GetPropertyLocalizedDomainName", "IElements.GetPropertyRealValue", "IElements.GetPropertyRealUnit", "IElements.GetPropertyIntegerValue", "IElements.GetPropertyBooleanValue", "IElements.GetPropertyTextValue", "IElements.GetPropertyDateTimeValue", "PropertyType")));
        private static JObject Value(ElementId id, string name)
        {
            var e = TopSolidHost.Elements; var type = e.GetPropertyType(id, name); var row = new JObject { ["type"] = type.ToString(), ["supported"] = true };
            if (type == PropertyType.None) { row["supported"] = false; row["reason"] = "Element does not have this property."; return row; }
            row["localizedName"] = e.GetPropertyLocalizedName(id, name); row["localizedDomainName"] = e.GetPropertyLocalizedDomainName(id, name);
            switch (type) {
                case PropertyType.Real: row["value"] = e.GetPropertyRealValue(id, name); e.GetPropertyRealUnit(id, name, out var unit, out var symbol); row["unitType"] = unit.ToString(); row["unitSymbol"] = symbol; row["valueConvention"] = "SI"; break;
                case PropertyType.Integer: row["value"] = e.GetPropertyIntegerValue(id, name); break;
                case PropertyType.Boolean: row["value"] = e.GetPropertyBooleanValue(id, name); break;
                case PropertyType.Text: row["value"] = e.GetPropertyTextValue(id, name); break;
                case PropertyType.DateTime: var date = e.GetPropertyDateTimeValue(id, name); row["value"] = date.ToString("o", CultureInfo.InvariantCulture); row["dateTimeKind"] = date.Kind.ToString(); break;
                default: row["supported"] = false; row["reason"] = "No documented scalar getter for this property type."; break;
            }
            return row;
        }
    }
}
