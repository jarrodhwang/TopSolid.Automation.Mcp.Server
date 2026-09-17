using System;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    // IDs describe different TopSolid domains. Never derive one by editing another ID string.
    internal static class ObjectIdentity
    {
        internal static readonly string[] PdmApi = ApiRefs.Kernel("IPdm.Exists", "IPdm.GetName", "IPdm.GetDescription", "IPdm.GetType", "IPdm.GetState", "IPdm.GetOwner");
        internal static readonly string[] DocumentApi = ApiRefs.Kernel("IDocuments.Exists", "IDocuments.GetName", "IDocuments.GetTypeGuid", "IDocuments.GetTypeFullName", "IDocuments.GetPdmObject", "IDocuments.GetPdmMinorRevision", "IPdm.GetMajorRevision", "IPdm.GetMajorRevisionText", "IPdm.GetMinorRevisionText", "IDocuments.GetUniversalId");
        internal static readonly string[] ElementApi = ApiRefs.Kernel("IElements.GetName", "IElements.GetFriendlyName", "IElements.GetTypeGuid", "IElements.GetTypeFullName", "IElements.HasName", "IElements.HasUniqueName", "IElements.HasSystemName", "IElements.IsRenamable", "IEntities.IsEntity", "IOperations.IsOperation", "IElements.GetOwner");
        public static JObject Pdm(PdmObjectId id)
        {
            var type = TopSolidHost.Pdm.GetType(id, out var extension);
            return new JObject { ["idKind"] = "PdmObjectId", ["pdmObjectId"] = id.Id,
                ["name"] = TopSolidHost.Pdm.GetName(id), ["description"] = TopSolidHost.Pdm.GetDescription(id),
                ["type"] = type.ToString(), ["extension"] = extension,
                ["state"] = TopSolidHost.Pdm.GetState(id).ToString(), ["ownerId"] = AutomationValues.Json(TopSolidHost.Pdm.GetOwner(id)) };
        }
        public static JObject Document(DocumentId id)
        {
            var minor = TopSolidHost.Documents.GetPdmMinorRevision(id);
            var major = TopSolidHost.Pdm.GetMajorRevision(minor);
            TopSolidHost.Documents.GetUniversalId(id, out var domain, out var name);
            return new JObject { ["idKind"] = "DocumentId", ["documentId"] = id.PdmDocumentId,
                ["name"] = TopSolidHost.Documents.GetName(id), ["type"] = TopSolidHost.Documents.GetTypeFullName(id),
                ["typeGuid"] = TopSolidHost.Documents.GetTypeGuid(id).ToString("D"),
                ["pdmObjectId"] = AutomationValues.Json(TopSolidHost.Documents.GetPdmObject(id)),
                ["pdmMinorRevisionId"] = AutomationValues.Json(minor), ["pdmMajorRevisionId"] = AutomationValues.Json(major),
                ["minorRevisionText"] = TopSolidHost.Pdm.GetMinorRevisionText(minor), ["majorRevisionText"] = TopSolidHost.Pdm.GetMajorRevisionText(major),
                ["universalId"] = domain == null || name == null ? JValue.CreateNull() : (JToken)new JObject { ["domain"] = domain, ["name"] = name } };
        }
        public static JObject Element(ElementId id) => new JObject {
            ["idKind"] = "ElementId", ["element"] = AutomationValues.Json(id),
            ["name"] = TopSolidHost.Elements.GetName(id), ["friendlyName"] = TopSolidHost.Elements.GetFriendlyName(id),
            ["type"] = TopSolidHost.Elements.GetTypeFullName(id), ["typeGuid"] = TopSolidHost.Elements.GetTypeGuid(id).ToString("D"),
            ["hasName"] = TopSolidHost.Elements.HasName(id), ["hasUniqueName"] = TopSolidHost.Elements.HasUniqueName(id),
            ["hasSystemName"] = TopSolidHost.Elements.HasSystemName(id), ["isRenamable"] = TopSolidHost.Elements.IsRenamable(id), ["isEntity"] = TopSolidHost.Entities.IsEntity(id),
            ["isOperation"] = TopSolidHost.Operations.IsOperation(id), ["owner"] = AutomationValues.Json(TopSolidHost.Elements.GetOwner(id)) };
        public static void RequireEntity(ElementId id)
        {
            if (!TopSolidHost.Entities.IsEntity(id)) throw new ArgumentException("This action requires an entity, not an operation or another element kind. Inspect the element's isEntity field.");
        }
        public static void ValidateRename(ElementId id, string name)
        {
            // HasName describes the current value; IsRenamable is the actual editing contract.
            ValidateNamePolicy(TopSolidHost.Elements.IsRenamable(id), TopSolidHost.Elements.HasSystemName(id), name);
            if (!TopSolidHost.Elements.HasUniqueName(id)) return;
            var existing = TopSolidHost.Elements.SearchByName(id.DocumentId, name);
            if (!existing.IsEmpty && !existing.Equals(id)) throw new ArgumentException("Another element already has that unique internal name in this document.");
        }
        internal static void ValidateNamePolicy(bool renamable, bool systemName, string name)
        {
            if (!renamable) throw new ArgumentException("TopSolid reports that this element's name cannot be modified.");
            if (systemName || name.StartsWith("$", StringComparison.Ordinal)) throw new ArgumentException("System element names are reserved; this rename tool only changes user names.");
        }
    }
}
