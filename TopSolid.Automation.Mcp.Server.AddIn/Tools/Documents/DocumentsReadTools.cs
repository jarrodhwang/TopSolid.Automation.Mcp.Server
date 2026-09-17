// Generated from the reviewed allowlist in scripts/ApiReference/generate_read_tools.py.
// Each call is compiled against the matched 7.20 SDK. No generic API invocation.
using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;
using TopSolid.Cad.Design.Automating;
using TopSolid.Cad.Drafting.Automating;
using TopSolid.Cad.Electrode.Automating;
using TopSolid.Cam.NC.Kernel.Automating;
using TopSolid.Cae.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class DocumentsReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_documents", "List open documents. IDs identify exact revisions.", Schema.Page(new JObject()),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Documents.GetOpenDocuments(), p)), "Documents", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IDocuments.GetOpenDocuments.html" }));
            register(new ToolDefinition("topsolid_list_loaded_documents", "List loaded documents.", Schema.Page(new JObject()),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Documents.GetDocuments(), p)), "Documents", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IDocuments.GetDocuments.html" }));
            register(new ToolDefinition("topsolid_get_document_references", "Get document references.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Documents.GetReferencedDocuments(a.Document(p), false), p)), "Documents", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IDocuments.GetReferencedDocuments.html" }));
            register(new ToolDefinition("topsolid_list_document_properties", "List document properties.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Documents.GetProperties(a.Document(p)), p)), "Documents", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IDocuments.GetProperties.html" }));
        }
    }
}
