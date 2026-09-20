using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class PdmDetailsTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_get_pdm_object_info", "Read a PDM object's name, description, type, state and owner.",
                new JObject { ["pdmObjectId"] = Schema.Text("PDM object ID returned by a tool.") },
                p => a.Read("kernel", () =>
                {
                    var id = a.Pdm(p);
                    return ObjectIdentity.Pdm(id);
                }), "Pdm", new[] { "pdmObjectId" }, true, ApiRefs.Kernel("IPdm.GetType", "IPdm.GetName", "IPdm.GetDescription", "IPdm.GetState", "IPdm.GetOwner")));
        register(new ToolDefinition("topsolid_list_pdm_children", "List immediate child folders and documents with names. Document rows include verified native extension/type and resolved document identity when available. Does not recursively traverse or modify PDM.",
                Schema.Page(new JObject { ["pdmObjectId"] = Schema.Text("Project or folder ID.") }),
                p => a.Read("kernel", () =>
                {
                    var parent = a.Pdm(p);
                    TopSolidHost.Pdm.GetConstituents(parent, out var folders, out var documents);
                    var parentName = TopSolidHost.Pdm.GetName(parent);
                    var loaded = new HashSet<DocumentId>(TopSolidHost.Documents.GetDocuments());
                    var values = folders.Select(id => new { id, kind = "folder" }).Concat(documents.Select(id => new { id, kind = "document" }));
                    return AutomationValues.Page(values, p, entry => Child(entry.id, entry.kind, parentName, loaded));
                }), "Pdm", new[] { "pdmObjectId" }, true,
                ApiRefs.Kernel("IPdm.GetConstituents", "IPdm.GetName", "IPdm.GetType", "IDocuments.GetDocument", "IDocuments.GetDocuments", "IDocuments.Exists", "IDocuments.GetTypeFullName")));
        }

        private static JObject Child(PdmObjectId id, string kind, string parentName, HashSet<DocumentId> loaded)
        {
            var row = new JObject { ["pdmObjectId"] = id.Id, ["kind"] = kind, ["name"] = TopSolidHost.Pdm.GetName(id) };
            if (!string.IsNullOrWhiteSpace(parentName)) row["parentName"] = parentName;
            if (kind != "document") return row;

            // A constituent is a PDM object, not a document revision. Resolve the
            // latest revision and native extension here so the picker can choose
            // the correct TopSolid artwork and preview target without a second
            // model round. This is read-only and never opens the document.
            try
            {
                var pdmType = TopSolidHost.Pdm.GetType(id, out var extension);
                if (!string.IsNullOrWhiteSpace(extension)) row["extension"] = extension;
                row["pdmType"] = pdmType.ToString();
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or global::System.Runtime.InteropServices.COMException)
            {
                // Preserve the PDM row if a transient/untyped child cannot expose its extension.
            }
            try
            {
                var document = TopSolidHost.Documents.GetDocument(id);
                if (!document.IsEmpty)
                {
                    row["documentId"] = document.PdmDocumentId;
                    row["isLoaded"] = loaded.Contains(document);
                    if (TopSolidHost.Documents.Exists(document))
                        row["typeFullName"] = TopSolidHost.Documents.GetTypeFullName(document);
                }
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or global::System.Runtime.InteropServices.COMException)
            {
                // Some PDM states have no resolvable revision yet. Keep the
                // verified extension and PDM identity; the client will show a
                // precise not-loaded/unavailable preview state.
            }
            return row;
        }
    }
}
