using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class DocumentActionTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_open_document", "Open the exact document revision in TopSolid. Opening can update the document, so user confirmation is required.",
                Target(), p => { a.PreviewDocument(p); var doc = a.Document(p); var old = doc.PdmDocumentId; TopSolidHost.Documents.Open(ref doc); var result = a.DocumentSummary(doc); result["originalDocumentId"] = old; result["opened"] = true; return result; },
                "Documents", new[] { "documentId" }, false, ApiRefs.Kernel("IDocuments.Open"), MutationReferences.Validate, p => a.PreviewDocument(p),
                "Open this document window. TopSolid may update it and change its revision during opening. This action is outside the geometry undo transaction."));
            register(new ToolDefinition("topsolid_save_document", "Save changes to the exact document revision. Requires explicit confirmation, including any synchronized documents listed in the preview.",
                Target(), p => { a.PreviewDocument(p); var doc = a.Document(p); TopSolidHost.Documents.Save(doc); return new JObject { ["operation"] = "save", ["documentId"] = doc.PdmDocumentId, ["saved"] = !TopSolidHost.Documents.IsDirty(doc), ["checkInPerformed"] = false }; },
                "Documents", new[] { "documentId" }, false, ApiRefs.Kernel("IDocuments.Save"), MutationReferences.Validate, p => a.PreviewDocument(p),
                "Persist this document's current changes to PDM. Saving is outside the geometry undo transaction; it is not check-in or NC export."));
            foreach (var action in new[] { "update", "rebuild", "rename" })
            {
                var operation = action;
                var properties = Target();
                if (operation == "rename") properties["name"] = Schema.Text("New document name.", 128);
                if (operation == "update") properties["allStages"] = Schema.Boolean("Update operations after the current stage too; default false.");
                register(new ToolDefinition("topsolid_" + operation + "_document", operation + " the target document in an undoable modification. Requires confirmation. Does not save.", properties,
                    p => a.Modify(p, operation + " document", "kernel", (doc, current) =>
                    {
                        if (operation == "update") TopSolidHost.Documents.Update(doc, (bool?)current["allStages"] ?? false);
                        else if (operation == "rebuild") TopSolidHost.Documents.Rebuild(doc);
                        else TopSolidHost.Documents.SetName(doc, (string)current["name"]);
                        var result = a.DocumentSummary(doc); result["action"] = operation; return result;
                    }), "Documents", operation == "rename" ? new[] { "documentId", "name" } : new[] { "documentId" }, false,
                    ApiRefs.Kernel("IApplication.StartModification", "IApplication.EndModification", "IDocuments.EnsureIsDirty", operation == "update" ? "IDocuments.Update" : operation == "rebuild" ? "IDocuments.Rebuild" : "IDocuments.SetName"),
                    MutationReferences.Validate, p => a.PreviewDocument(p)));
            }
        }
        internal static JObject Target() => new JObject { ["documentId"] = Schema.Text("Exact target document revision ID returned by a live tool.") };
    }
}
