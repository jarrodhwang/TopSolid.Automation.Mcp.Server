using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed partial class AutomationGateway
    {
        private EditStage editStage;
        internal JObject InEditStage(EditStage stage, Func<JObject> action)
        {
            var previous = editStage;
            try { editStage = stage; return action(); }
            finally { editStage = previous; }
        }
        public JObject PreviewDocument(JObject arguments, string module = "kernel")
        {
            ConnectModule(module);
            if (arguments["documentId"] == null) throw new ArgumentException("A change requires an explicit documentId from a live document query.");
            MutationReferences.Validate(arguments);
            var document = Document(arguments);
            foreach (var reference in arguments.DescendantsAndSelf().OfType<JObject>().Where(o => o["documentId"] != null && o["id"] != null))
                Element(new JObject { ["element"] = reference.DeepClone() });
            var result = new JObject { ["documentId"] = document.PdmDocumentId, ["name"] = TopSolidHost.Documents.GetName(document),
                ["type"] = TopSolidHost.Documents.GetTypeFullName(document), ["isDirty"] = TopSolidHost.Documents.IsDirty(document),
                ["affectedDocuments"] = AffectedDocuments(document), ["scopeNote"] = "TopSolid updates synchronized documents together. All documents listed here are included in this confirmation." };
            var stage = CamStages.Resolve(this, arguments, editStage);
            if (!stage.IsEmpty) result["requiredStage"] = AutomationValues.Json(stage);
            return result;
        }

        private JArray AffectedDocuments(DocumentId document)
        {
            var seen = new System.Collections.Generic.HashSet<DocumentId>();
            var pending = new System.Collections.Generic.Queue<DocumentId>(); pending.Enqueue(document);
            while (pending.Count > 0)
            {
                var id = pending.Dequeue(); if (!seen.Add(id)) continue;
                if (seen.Count > 50) throw new InvalidOperationException("The synchronized document group exceeds the 50-document preview limit.");
                if (TopSolidHost.Documents.IsSynchronized(id))
                    foreach (var related in TopSolidHost.Documents.GetSynchronizedDocuments(id))
                        if (!related.IsEmpty && !seen.Contains(related)) pending.Enqueue(related);
            }
            return new JArray(seen.OrderBy(id => id.PdmDocumentId, StringComparer.Ordinal).Select(DocumentSummary));
        }

        // Refresh ALL document-local handles after EnsureIsDirty creates a new minor revision.
        // External sourceDocumentId references (assembly definitions) deliberately retain their revision.
        public JObject Modify(JObject arguments, string title, string module, Func<DocumentId, JObject, JObject> action, EditStage stage = EditStage.None)
        {
            if (stage == EditStage.None) stage = editStage;
            PreviewDocument(arguments, module);
            var document = Document(arguments);
            var original = document.PdmDocumentId;
            CamStages.Resolve(this, arguments, stage);
            return ModificationScope.Run("AI: " + title,
                name => TopSolidHost.Application.StartModification(name, false), TopSolidHost.Application.EndModification,
                () =>
                {
                    TopSolidHost.Documents.EnsureIsDirty(ref document);
                    var currentArguments = MutationReferences.Rebase(arguments, document.PdmDocumentId);
                    CamStages.Enter(this, currentArguments, stage);
                    var result = action(document, currentArguments);
                    result["originalDocumentId"] = original;
                    result["documentId"] = document.PdmDocumentId;
                    result["pdmObjectId"] = AutomationValues.Json(TopSolidHost.Documents.GetPdmObject(document));
                    result["pdmMinorRevisionId"] = AutomationValues.Json(TopSolidHost.Documents.GetPdmMinorRevision(document));
                    result["saved"] = false;
                    return result;
                });
        }

        public static void RequireValid(ElementId id, string what)
        {
            if (id.IsEmpty || !TopSolidHost.Elements.Exists(id) || TopSolidHost.Elements.IsInvalid(id))
                throw new InvalidOperationException("TopSolid did not produce a valid " + what + ". The modification will be rolled back.");
        }

        public JObject PreviewPdm(JObject arguments, string key, bool owner = false)
        {
            EnsureConnected();
            var id = Pdm(arguments, key);
            var type = TopSolidHost.Pdm.GetType(id, out var extension);
            if (owner && type != PdmObjectType.WorkingProject && type != PdmObjectType.Folder)
                throw new ArgumentException("Choose a working project or ordinary folder as the creation destination.");
            return new JObject { ["pdmObjectId"] = AutomationValues.Json(id), ["name"] = TopSolidHost.Pdm.GetName(id), ["type"] = type.ToString(), ["extension"] = extension };
        }

        public JObject PdmCreationReadiness()
        {
            EnsureConnected();
            var fullName = TopSolidHost.Application.ActiveCommandFullName;
            var name = TopSolidHost.Application.ActiveCommandName;
            return new JObject { ["canCreatePdmObjects"] = string.IsNullOrEmpty(fullName) && string.IsNullOrEmpty(name),
                ["activeCommandName"] = name, ["activeCommandFullName"] = fullName };
        }
        private void RequirePdmCreationIdle()
        {
            var state = PdmCreationReadiness();
            DocumentCreationCatalog.RequireIdle((string)state["activeCommandName"] ?? (string)state["activeCommandFullName"]);
            DocumentCreationCatalog.RequireIdle((string)state["activeCommandFullName"]);
        }
        public JObject PreviewPdmCreation(string kind, JObject arguments)
        {
            RequirePdmCreationIdle();
            var target = kind == "project" ? new JObject { ["type"] = "New working project in connected PDM" } : PreviewPdm(arguments, "ownerId", true);
            target["newName"] = arguments["name"].DeepClone();
            if (kind == "document")
            {
                if (arguments["templateId"] != null)
                {
                    target["template"] = PreviewPdm(arguments, "templateId");
                    if ((string)target["template"]["type"] != PdmObjectType.TopSolidDocument.ToString())
                        throw new ArgumentException("The requested template must be a native TopSolid document.");
                    target["creationMode"] = "specificTemplate";
                }
                else
                {
                    target["extension"] = DocumentCreationCatalog.Extension((string)arguments["extension"]);
                    target["useDefaultTemplate"] = DocumentCreationCatalog.UseDefaultTemplate(arguments);
                    target["creationMode"] = DocumentCreationCatalog.UseDefaultTemplate(arguments) ? "defaultTemplate" : "empty";
                }
            }
            return target;
        }
        public JObject CreatePdmObject(string kind, JObject arguments)
        {
            // Recheck immediately before the irreversible PDM call, even after a
            // successful preview. Never cancel the user's active native command.
            RequirePdmCreationIdle();
            PdmObjectId id;
            if (kind == "project") id = TopSolidHost.Pdm.CreateProject((string)arguments["name"], false);
            else
            {
                PreviewPdm(arguments, "ownerId", true);
                var owner = Pdm(arguments, "ownerId");
                if (kind == "folder") id = TopSolidHost.Pdm.CreateFolder(owner, (string)arguments["name"]);
                else if (arguments["templateId"] != null)
                {
                    var template = Pdm(arguments, "templateId");
                    if (TopSolidHost.Pdm.GetType(template, out _) != PdmObjectType.TopSolidDocument)
                        throw new ArgumentException("The template must be a TopSolid document returned by the PDM tools.");
                    id = TopSolidHost.Pdm.CreateDocumentWithTemplate(owner, template);
                }
                else id = TopSolidHost.Pdm.CreateDocument(owner, DocumentCreationCatalog.Extension((string)arguments["extension"]), DocumentCreationCatalog.UseDefaultTemplate(arguments));
            }
            if (id.IsEmpty) throw new InvalidOperationException("TopSolid returned an empty PDM creation identifier.");
            // PDM calls cannot join a geometry transaction. Preserve a receipt if a later step fails.
            var receipt = new JObject { ["pdmObjectId"] = AutomationValues.Json(id), ["created"] = true, ["undoable"] = false };
            if (kind == "document")
            {
                receipt["creationMode"] = arguments["templateId"] != null ? "specificTemplate" : DocumentCreationCatalog.UseDefaultTemplate(arguments) ? "defaultTemplate" : "empty";
                if (arguments["templateId"] != null) receipt["templateId"] = arguments["templateId"].DeepClone();
                else { receipt["extension"] = DocumentCreationCatalog.Extension((string)arguments["extension"]); receipt["useDefaultTemplate"] = DocumentCreationCatalog.UseDefaultTemplate(arguments); }
            }
            try
            {
                if (kind == "document")
                {
                    TopSolidHost.Pdm.SetName(id, (string)arguments["name"]);
                    var document = TopSolidHost.Documents.GetDocument(id);
                    if (document.IsEmpty) throw new InvalidOperationException("Created PDM object has no accessible document revision.");
                    receipt["documentId"] = document.PdmDocumentId;
                }
                receipt["name"] = TopSolidHost.Pdm.GetName(id);
                receipt["complete"] = true;
            }
            catch (Exception ex) { throw new PartialChangeException("The PDM object was created, but completing its setup failed. Do not create a duplicate.", receipt, ex); }
            return receipt;
        }
    }

    internal static class MutationReferences
    {
        public static void Validate(JObject arguments)
        {
            var expected = (string)arguments["documentId"];
            if (string.IsNullOrWhiteSpace(expected)) throw new ArgumentException("An explicit target documentId is required.");
            foreach (var obj in arguments.DescendantsAndSelf().OfType<JObject>())
                if (obj["documentId"] != null && (string)obj["documentId"] != expected)
                    throw new ArgumentException("Every element and topology handle to modify must belong to the exact target document revision. Query fresh IDs first.");
        }
        public static JObject Rebase(JObject arguments, string currentDocumentId)
        {
            Validate(arguments);
            var current = (JObject)arguments.DeepClone();
            foreach (var obj in current.DescendantsAndSelf().OfType<JObject>())
                if (obj["documentId"] != null) obj["documentId"] = currentDocumentId;
            return current;
        }
    }

    internal sealed class PartialChangeException : Exception
    {
        public PartialChangeException(string message, JObject receipt, Exception inner) : base(message, inner) { Receipt = receipt; }
        public JObject Receipt { get; }
    }
}
