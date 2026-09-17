using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class DocumentPropertyTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_get_document_property", "Read a scalar property using its exact full name from list_document_properties. Unsupported property types are reported explicitly.",
                new JObject { ["documentId"] = Schema.Text("Document ID; omit for active."), ["name"] = Schema.Text("Exact property full name.", 512) },
                p => a.Read("kernel", () => Value(a.Document(p), (string)p["name"])), "Documents", new[] { "name" }, true,
                ApiRefs.Kernel("IDocuments.GetPropertyType", "IDocuments.GetPropertyRealValue", "IDocuments.GetPropertyRealUnit", "IDocuments.GetPropertyIntegerValue", "IDocuments.GetPropertyBooleanValue", "IDocuments.GetPropertyTextValue", "IDocuments.GetPropertyDateTimeValue")));
        }
        internal static JObject Value(DocumentId id, string name)
        {
            var type = TopSolidHost.Documents.GetPropertyType(id, name);
            var result = new JObject { ["documentId"] = id.PdmDocumentId, ["name"] = name, ["type"] = type.ToString(), ["supported"] = true };
            object value;
            switch (type)
            {
                case PropertyType.Real:
                    value = TopSolidHost.Documents.GetPropertyRealValue(id, name);
                    TopSolidHost.Documents.GetPropertyRealUnit(id, name, out var unit, out var symbol);
                    result["unitType"] = unit.ToString(); result["unitSymbol"] = symbol; result["valueConvention"] = "SI"; break;
                case PropertyType.Integer: value = TopSolidHost.Documents.GetPropertyIntegerValue(id, name); break;
                case PropertyType.Boolean: value = TopSolidHost.Documents.GetPropertyBooleanValue(id, name); break;
                case PropertyType.Text: value = TopSolidHost.Documents.GetPropertyTextValue(id, name); break;
                case PropertyType.DateTime: value = TopSolidHost.Documents.GetPropertyDateTimeValue(id, name); break;
                default: result["supported"] = false; result["message"] = "This property type has no value adapter yet."; return result;
            }
            result["value"] = AutomationValues.Json(value); return result;
        }
    }
}
