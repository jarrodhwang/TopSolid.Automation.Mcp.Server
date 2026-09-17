using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools.Documents
{
    internal static class DocumentTools
    {
        public static ToolDefinition Active(AutomationGateway automation)
        {
            return new ToolDefinition("topsolid_get_active_document",
                "Get the document currently being edited in TopSolid, including its name and documentId; explicitly reports when there is no active document. Read-only; does not open any document.",
                new JObject(), arguments => automation.GetActiveDocument(), "Documents", api: ApiRefs.Kernel("IDocuments.EditedDocument", "IDocuments.GetName"));
        }
        public static ToolDefinition Info(AutomationGateway automation)
        {
            return new ToolDefinition("topsolid_get_document_info",
                "Get TopSolid document name, type, type GUID, unsaved state, PDM object ID and verified file extension for creating another document of this type. Omit documentId for the currently edited document. Never invent IDs or extensions.",
                new JObject
                {
                    ["documentId"] = new JObject
                    {
                        ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 2048,
                        ["description"] = "Optional opaque documentId previously returned by a TopSolid tool. Omit for the active document."
                    }
                }, arguments => automation.GetDocumentInfo((string)arguments["documentId"]), "Documents", api: ApiRefs.Kernel("IDocuments.GetTypeFullName", "IDocuments.GetTypeGuid", "IDocuments.IsDirty", "IDocuments.GetPdmObject", "IPdm.GetType"));
        }
    }
}
