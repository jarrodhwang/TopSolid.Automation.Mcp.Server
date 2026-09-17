using System;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ObjectModelTools
    {
        public static void Register(Action<ToolDefinition> register) => register(new ToolDefinition("topsolid_get_object_model", "Read the TopSolid identifier/name hierarchy and CRUD rules from the Design Automation Guide and 7.20 API contracts. Offline; no TopSolid connection needed.", new JObject(), p => new JObject {
            ["identifiers"] = new JObject {
                ["PdmObjectId"] = "Opaque PDM object identity: project, folder, document, etc. Not a GUID or document revision. Validate Exists and GetType.",
                ["PdmMajorRevisionId"] = "Major revision belonging to a PDM document object.",
                ["PdmMinorRevisionId"] = "Minor revision belonging to a major revision. Revision text is a label, not an ID.",
                ["DocumentId"] = "One TopSolid document minor revision. GetDocument(PdmObjectId) selects its backing document revision; projects/libraries can resolve to metadata documents. Verify Documents.Exists and type. GetMinorRevisionDocument selects an explicit minor revision.",
                ["ElementId"] = "DocumentId plus document-local element identifier. An element may be an entity, an operation or another kind.",
                ["ElementItemId"] = "ElementId plus ItemLabel for a face, edge, sketch profile/section or other topology item.",
                ["typeGuid"] = "GUID identifying a TYPE shared by many objects. Never use it to select an individual object.",
                ["universalId"] = "Document domain/name pair used by SearchDocumentByUniversalId; separate from GUIDs and display names." },
            ["names"] = new JArray("PDM names and friendly element names can be duplicated. Keep every candidate and select by exact ID.",
                "Elements.GetName is the internal name; GetFriendlyName is display text. HasUniqueName/HasSystemName describe the name contract. IsRenamable determines whether the name may change; HasName is not edit permission. System names start with $ and are translated for display.",
                "SearchByName resolves uniquely named elements by their real internal name, not an arbitrary translated display string."),
            ["crud"] = new JArray("Document content: StartModification -> EnsureIsDirty(ref DocumentId) -> rebase target element/item handles -> execute -> EndModification. Save is separate.",
                "PDM object creation, metadata updates, deletion and restoration are separate persistent operations, outside document undo. Retain partial receipts and never automatically retry uncertain changes.",
                "Entity transforms require an entity; operation and entity identities are not interchangeable. Element deletion affects the document; PDM deletion affects its managed object.",
                "Sketch edits have their own StartModification/EndModification scope inside the document transaction. Geometry is in SI; sketch coordinates are relative to the sketch frame/plane.",
                "A created document is resolved from its returned PDM ID. Never guess a template extension, document type, ID, or unique name from real-world terminology."),
            ["guidePages"] = "Installed Design Automation Guide: printed pages 9-10, 12-15, 18-19, 22-23, 26-27, 34-38. The installed guide is EN v7.11; bindings are verified against the 7.20 contracts/SDK." }, "System",
            api: ApiRefs.Kernel("IDocuments.GetDocument", "IDocuments.GetMinorRevisionDocument", "IDocuments.EnsureIsDirty", "IElements.GetTypeGuid", "IElements.HasUniqueName", "IElements.HasSystemName", "IElements.IsRenamable", "IElements.GetFriendlyName", "IPdm.SearchDocumentByUniversalId")));
    }
}
