using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class PdmLifecycleTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            var change = Schema.Object(new JObject { ["pdmObjectId"] = Schema.Text("Exact PDM object selected from live results."),
                ["name"] = Schema.Text("New PDM name. Names need not be unique.", 128), ["description"] = new JObject { ["type"] = "string", ["maxLength"] = 1024 } }, "pdmObjectId");
            register(new ToolDefinition("topsolid_update_pdm_objects", "Update names/descriptions of up to 32 exact PDM objects after confirmation. Persistent PDM metadata changes, outside document modification/undo. Reads back each result; returns partial receipts on failure and never retries.",
                new JObject { ["changes"] = Schema.Array(change, 1, 32) }, p => {
                    a.ConnectModule("kernel");
                    return ExecuteBatch((JArray)p["changes"], (entry, receipt) => {
                        var id = a.Pdm(entry); var fields = new JArray(); receipt["completedProperties"] = fields;
                        if (entry["name"] != null) { TopSolidHost.Pdm.SetName(id, (string)entry["name"]); fields.Add("name"); }
                        if (entry["description"] != null) { TopSolidHost.Pdm.SetDescription(id, (string)entry["description"]); fields.Add("description"); }
                        var after = ObjectIdentity.Pdm(id); receipt["after"] = after;
                        if (entry["name"] != null && (string)after["name"] != (string)entry["name"] || entry["description"] != null && (string)after["description"] != (string)entry["description"])
                            throw new InvalidOperationException("PDM metadata readback differs. Changes cannot be rolled back by this server.");
                        receipt["readBackVerified"] = true;
                    });
                }, "Pdm", new[] { "changes" }, false, ObjectIdentity.PdmApi.Concat(ApiRefs.Kernel("IPdm.SetName", "IPdm.SetDescription")).ToArray(), ValidateUpdates,
                p => Preview(a, ((JArray)p["changes"]).Select(v => v["pdmObjectId"]), false, false),
                "Change the listed PDM names/descriptions persistently. This does not rename a document-local element and is not a geometry undo transaction. Earlier successful changes remain if a later object fails."));
            register(new ToolDefinition("topsolid_delete_pdm_documents", "Delete up to 32 exact PDM document objects after confirmation. No folders/projects, revision purge or annihilation. This is PDM deletion, not deletion of elements inside a document. Outside document undo; partial outcomes are possible.",
                new JObject { ["pdmObjectIds"] = Schema.Array(Schema.Text("Exact document PdmObjectId."), 1, 32) }, p => {
                    a.ConnectModule("kernel"); var ids = ((JArray)p["pdmObjectIds"]).Select(v => a.Pdm(new JObject { ["pdmObjectId"] = v.DeepClone() })).ToList();
                    var receipt = new JObject { ["requestedPdmObjectIds"] = p["pdmObjectIds"].DeepClone(), ["undoable"] = false, ["outcome"] = "submitted" };
                    try {
                        TopSolidHost.Pdm.DeleteSeveral(ids);
                        var states = new JArray(ids.Select(id => {
                            var exists = TopSolidHost.Pdm.Exists(id);
                            return new JObject { ["pdmObjectId"] = id.Id, ["exists"] = exists,
                                ["state"] = exists ? TopSolidHost.Pdm.GetState(id).ToString() : null };
                        }));
                        receipt["after"] = states;
                        if (states.Any(s => (bool)s["exists"] && (string)s["state"] != PdmObjectState.Deleted.ToString()))
                            throw new InvalidOperationException("Some objects are still present without Deleted state. Inspect the reported PDM states before further action.");
                        receipt["outcome"] = "deleted"; return receipt;
                    } catch (Exception ex) { receipt["outcome"] = "uncertain"; throw new PartialChangeException("PDM deletion was submitted and may have affected some objects. Do not automatically retry.", receipt, ex); }
                }, "Pdm", new[] { "pdmObjectIds" }, false, ObjectIdentity.PdmApi.Concat(ApiRefs.Kernel("IPdm.DeleteSeveral")).ToArray(),
                p => BatchInput.Unique((JArray)p["pdmObjectIds"], "PDM object"), p => Preview(a, (JArray)p["pdmObjectIds"], true, false),
                "Delete exactly the listed PDM documents. TopSolid manages reference/dependency constraints; this is outside the geometry undo transaction. No folders, projects, permanent purge, or automatic retry."));
            register(new ToolDefinition("topsolid_restore_pdm_documents", "Restore up to 32 deleted PDM document objects by their retained exact IDs after confirmation. This restores deleted objects, not an older document revision. Persistent action outside document undo.",
                new JObject { ["pdmObjectIds"] = Schema.Array(Schema.Text("Exact deleted document PdmObjectId from a prior receipt or live PDM query."), 1, 32) }, p => {
                    a.ConnectModule("kernel"); return ExecuteBatch(new JArray(((JArray)p["pdmObjectIds"]).Select(v => new JObject { ["pdmObjectId"] = v.DeepClone() })), (entry, receipt) => {
                        var id = new PdmObjectId((string)entry["pdmObjectId"]); TopSolidHost.Pdm.Restore(id); var after = ObjectIdentity.Pdm(id); receipt["after"] = after;
                        if (IsDeleted(TopSolidHost.Pdm.GetState(id))) throw new InvalidOperationException("The object remains deleted after Restore. Inspect PDM before retrying.");
                        receipt["readBackVerified"] = true;
                    });
                }, "Pdm", new[] { "pdmObjectIds" }, false, ObjectIdentity.PdmApi.Concat(ApiRefs.Kernel("IPdm.Restore")).ToArray(),
                p => BatchInput.Unique((JArray)p["pdmObjectIds"], "PDM object"), p => Preview(a, (JArray)p["pdmObjectIds"], true, true),
                "Restore exactly these deleted PDM document objects. This is persistent, outside document undo, and does not restore an older revision. Earlier restores remain if a later item fails."));
        }
        internal static bool IsDeleted(PdmObjectState state) => state == PdmObjectState.Deleted;
        internal static JObject Preview(AutomationGateway a, global::System.Collections.Generic.IEnumerable<JToken> ids, bool documentsOnly, bool deleted)
        {
            a.ConnectModule("kernel"); var targets = new JArray();
            foreach (var token in ids) {
                var id = deleted ? new PdmObjectId((string)token) : a.Pdm(new JObject { ["pdmObjectId"] = token.DeepClone() });
                if (id.IsEmpty) throw new ArgumentException("An exact PDM object ID is required.");
                var type = TopSolidHost.Pdm.GetType(id, out _);
                ValidateTarget(type, TopSolidHost.Pdm.GetState(id), documentsOnly, deleted);
                var row = ObjectIdentity.Pdm(id);
                if (type == PdmObjectType.TopSolidDocument && !deleted) { var doc = TopSolidHost.Documents.GetDocument(id); row["latestDocumentId"] = AutomationValues.Json(doc); }
                targets.Add(row);
            }
            return new JObject { ["targets"] = targets, ["undoable"] = false, ["scope"] = documentsOnly ? "Exact PDM document objects only" : "Exact PDM metadata objects" };
        }
        internal static void ValidateTarget(PdmObjectType type, PdmObjectState state, bool documentsOnly, bool deleted)
        {
            if (documentsOnly && type != PdmObjectType.TopSolidDocument && type != PdmObjectType.UnknownDocument)
                throw new ArgumentException("Select document objects only. This tool does not delete or restore folders, projects, shortcuts or resource containers.");
            if (!documentsOnly && type != PdmObjectType.WorkingProject && type != PdmObjectType.LibraryProject && type != PdmObjectType.Folder && type != PdmObjectType.TopSolidDocument && type != PdmObjectType.UnknownDocument)
                throw new ArgumentException("Metadata updates require an ordinary project, folder or document object.");
            if (IsDeleted(state) != deleted) throw new ArgumentException(deleted ? "Restore requires a deleted object." : "A deleted object must be restored before this action.");
        }
        internal static void ValidateUpdates(JObject p)
        {
            BatchInput.Unique(((JArray)p["changes"]).Select(v => v["pdmObjectId"]), "PDM object");
            if (((JArray)p["changes"]).Cast<JObject>().Any(v => v["name"] == null && v["description"] == null)) throw new ArgumentException("Every PDM update needs a name or description.");
        }
        internal static JObject ExecuteBatch(JArray entries, Action<JObject, JObject> change)
        {
            var receipts = new JArray(); var result = new JObject { ["items"] = receipts, ["undoable"] = false, ["complete"] = false };
            foreach (JObject entry in entries) {
                var receipt = new JObject { ["pdmObjectId"] = entry["pdmObjectId"].DeepClone(), ["outcome"] = "submitted" }; receipts.Add(receipt);
                try { change(entry, receipt); receipt["outcome"] = "completed"; }
                catch (Exception ex) { receipt["outcome"] = "uncertain"; result["notAttempted"] = entries.Count - receipts.Count;
                    throw new PartialChangeException("A persistent PDM batch stopped. Review completed and uncertain receipts; earlier changes are not rolled back. Do not automatically retry.", result, ex); }
            }
            result["complete"] = true; return result;
        }
    }
}
