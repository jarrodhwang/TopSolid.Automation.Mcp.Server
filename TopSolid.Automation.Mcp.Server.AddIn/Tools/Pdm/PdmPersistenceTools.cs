using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class PdmPersistenceTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            var api = ApiRefs.Kernel("IPdm.GetType", "IPdm.GetName", "IPdm.GetState", "IPdm.IsDirty", "IApplication.ActiveCommandName", "IApplication.ActiveCommandFullName");
            register(new ToolDefinition("topsolid_check_in_pdm_objects", "CHECK IN exact PDM objects using the native CheckInSeveral API. For an entire working project, pass its PdmObjectId with recursive=true. One confirmation includes all owned constituents, up to 128 targets; never traverses library references. Saving alone is NOT check-in. Reads back actual PDM states; partial/uncertain outcomes are never retried.",
                new JObject { ["pdmObjectIds"] = Schema.Array(Schema.Text("Exact working project, folder or document PdmObjectId."), 1, 32),
                    ["recursive"] = Schema.Boolean("Include owned descendants. Set true only for an explicitly requested whole project/folder check-in. Default false.") },
                a.CheckInPdmObjects, "Pdm", new[] { "pdmObjectIds" }, false,
                Join(api, "IPdm.CheckInSeveral", "IPdm.GetConstituents"), p => BatchInput.Unique((JArray)p["pdmObjectIds"], "PDM object"), a.PreviewCheckIn,
                "Check in these objects and, when recursive, the listed owned descendants through native PDM. This can persist modified data and release modification ownership. Outside document undo. Inspect returned states; a successful save is never proof of check-in.", executePrepared: a.CheckInPdmObjects));
            register(new ToolDefinition("topsolid_save_documents", "Save modified documents in ONE native batch and ONE confirmation. Default scope=openDirty selects modified OPEN documents, including synchronized partners. loadedDirty is available only for an explicit request to save all loaded documents, including dependencies. Alternatively supply exact documentIds. No preliminary document listing is needed. Maximum 128 affected documents; no check-in.",
                new JObject { ["scope"] = Schema.Choice("Default openDirty. loadedDirty includes loaded dependency documents and must be explicitly requested.", "openDirty", "loadedDirty"),
                    ["documentIds"] = Schema.Array(Schema.Text("Exact loaded document revision ID. Mutually exclusive with scope."), 1, 128) },
                a.SaveDocuments, "Documents", new string[0], false,
                Join(api, "IPdm.SaveSeveral", "IDocuments.GetDocuments", "IDocuments.GetDocument", "IDocuments.GetOpenDocuments", "IDocuments.IsDirty", "IDocuments.IsSynchronized", "IDocuments.GetSynchronizedDocuments", "IDocuments.GetPdmObject"),
                p => { if (p["scope"] != null && p["documentIds"] != null) throw new ArgumentException("Supply either scope or documentIds, not both.");
                    if (p["documentIds"] is JArray ids) BatchInput.Unique(ids, "document"); }, a.PreviewSaveDocuments,
                "Persist changes to the listed modified documents and synchronized partners in one native PDM batch. No check-in, geometry undo or automatic retry. The preview is rechecked immediately before execution.", executePrepared: a.SaveDocuments));
        }
        private static string[] Join(string[] basis, params string[] names) => global::System.Linq.Enumerable.ToArray(global::System.Linq.Enumerable.Concat(basis, ApiRefs.Kernel(names)));
    }
}
