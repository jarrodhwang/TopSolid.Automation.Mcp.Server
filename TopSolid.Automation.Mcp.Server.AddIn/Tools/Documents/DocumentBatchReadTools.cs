using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class DocumentBatchReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_document_summaries", "List open or loaded documents with names, type, dirty state and PDM ID in one page. Default scope open. Does not open or load documents. Follow nextOffset for remaining rows.",
                BatchRead.Paging(new JObject { ["scope"] = Schema.Choice("Collection; default open.", "open", "loaded") }),
                p => a.Read("kernel", () => BatchRead.Page((string)p["scope"] == "loaded" ? TopSolidHost.Documents.GetDocuments() : TopSolidHost.Documents.GetOpenDocuments(), p,
                    id => new JObject { ["documentId"] = id.PdmDocumentId }, id => {
                        var pdm = TopSolidHost.Documents.GetPdmObject(id); TopSolidHost.Pdm.GetType(pdm, out var extension);
                        return new JObject { ["name"] = TopSolidHost.Documents.GetName(id), ["type"] = TopSolidHost.Documents.GetTypeFullName(id),
                            ["isDirty"] = TopSolidHost.Documents.IsDirty(id), ["pdmObjectId"] = AutomationValues.Json(pdm), ["extension"] = extension };
                    })),
                "Documents", api: ApiRefs.Kernel("IDocuments.GetDocuments", "IDocuments.GetOpenDocuments", "IDocuments.GetName", "IDocuments.GetTypeFullName", "IDocuments.IsDirty", "IDocuments.GetPdmObject", "IPdm.GetType")));
            register(new ToolDefinition("topsolid_list_document_property_values", "Read document property names, values and unit metadata in pages, without individual property calls. Unsupported types and per-property failures are explicit. Follow nextOffset.",
                BatchRead.Paging(new JObject { ["documentId"] = Schema.Text("Document revision; default active.") }),
                p => a.Read("kernel", () => { var doc = a.Document(p); return BatchRead.Page(TopSolidHost.Documents.GetProperties(doc), p,
                    name => new JObject { ["name"] = name }, name => DocumentPropertyTools.Value(doc, name)); }), "Documents",
                api: ApiRefs.Kernel("IDocuments.GetProperties", "IDocuments.GetPropertyType", "IDocuments.GetPropertyRealValue", "IDocuments.GetPropertyRealUnit", "IDocuments.GetPropertyIntegerValue", "IDocuments.GetPropertyBooleanValue", "IDocuments.GetPropertyTextValue", "IDocuments.GetPropertyDateTimeValue")));
        }
    }
}
