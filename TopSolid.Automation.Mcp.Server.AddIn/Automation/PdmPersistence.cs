using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    // Bounded traversal follows ownership, never references to library/dependency objects.
    internal static class PersistenceBatch
    {
        internal const int MaximumTargets = 128;
        internal static void ValidatePreview(JArray targets)
        {
            if (targets.ToString(Newtonsoft.Json.Formatting.None).Length > 24000)
                throw new ArgumentException("This persistence preview is too large to preserve complete before/after receipts. Select a smaller explicit scope; nothing was submitted.");
        }
        internal static List<T> Expand<T>(IEnumerable<T> roots, Func<T, IEnumerable<T>> children)
        {
            var pending = new Queue<T>(roots); var seen = new HashSet<T>(); var result = new List<T>();
            while (pending.Count > 0)
            {
                var id = pending.Dequeue(); if (!seen.Add(id)) continue;
                if (seen.Count > MaximumTargets) throw new ArgumentException("The persistence preview exceeds 128 objects. Select a smaller folder or explicit document set; nothing was submitted.");
                result.Add(id);
                foreach (var child in children(id)) if (!seen.Contains(child)) pending.Enqueue(child);
            }
            return result;
        }

        // A batch API can fail after changing some targets. Always retain all IDs,
        // perform best-effort readback, and never automatically repeat the write.
        internal static JObject Execute(string operation, JArray before, Action submit, Func<JObject, JObject> read)
        {
            ValidatePreview(before);
            var receipt = new JObject { ["operation"] = operation, ["undoable"] = false, ["requestedCount"] = before.Count,
                ["nativeCallCompleted"] = false, ["noChangesNeeded"] = before.Count == 0, ["complete"] = false, ["before"] = before.DeepClone() };
            Exception failure = null;
            try { if (before.Count > 0) { submit(); receipt["nativeCallCompleted"] = true; } }
            catch (Exception ex) { failure = ex; }
            var after = new JArray(); receipt["after"] = after;
            foreach (JObject row in before)
            {
                try { after.Add(read(row)); }
                catch (Exception ex) { var error = AutomationGateway.Describe(ex); after.Add(new JObject { ["pdmObjectId"] = row["pdmObjectId"].DeepClone(), ["readError"] = error.Length > 160 ? error.Substring(0, 160) : error }); failure = failure ?? ex; }
            }
            receipt["complete"] = failure == null;
            if (operation == "save")
            {
                receipt["savedCount"] = after.Count(r => (bool?)r["isDirty"] == false);
                receipt["saved"] = failure == null && after.All(r => (bool?)r["isDirty"] == false);
                receipt["checkInPerformed"] = false;
            }
            else
            {
                receipt["checkedInCount"] = after.Count(r => (string)r["state"] == "CheckedIn");
                receipt["checkInVerifiedForAll"] = failure == null && after.All(r => (string)r["state"] == "CheckedIn");
            }
            if (failure != null) throw new PartialChangeException("The " + operation + " batch may have partially changed PDM. Inspect these receipts before another action; do not automatically retry.", receipt, failure);
            return receipt;
        }
    }

    internal sealed partial class AutomationGateway
    {
        private void RequirePersistenceIdle()
        {
            EnsureConnected();
            var active = TopSolidHost.Application.ActiveCommandFullName;
            var name = TopSolidHost.Application.ActiveCommandName;
            if (!string.IsNullOrEmpty(active) || !string.IsNullOrEmpty(name))
                throw new InvalidOperationException("Finish or cancel the active TopSolid command before saving or checking in: " + (string.IsNullOrEmpty(name) ? active : name));
        }
        private static JObject PersistenceRow(PdmObjectId id)
        {
            var type = TopSolidHost.Pdm.GetType(id, out _);
            if (type != PdmObjectType.WorkingProject && type != PdmObjectType.Folder && type != PdmObjectType.TemplatesFolder && type != PdmObjectType.TemplatesDefaultFolder && type != PdmObjectType.TopSolidDocument && type != PdmObjectType.UnknownDocument)
                throw new ArgumentException("Unsupported persistence target: " + id.Id + " (" + type + "). Select a working project, ordinary folder, or document.");
            return new JObject { ["pdmObjectId"] = id.Id, ["name"] = TopSolidHost.Pdm.GetName(id), ["type"] = type.ToString(),
                ["state"] = TopSolidHost.Pdm.GetState(id).ToString(), ["isDirty"] = TopSolidHost.Pdm.IsDirty(id) };
        }
        public JObject PreviewCheckIn(JObject p)
        {
            RequirePersistenceIdle();
            var roots = ((JArray)p["pdmObjectIds"]).Select(id => Pdm(new JObject { ["pdmObjectId"] = id.DeepClone() })).ToList();
            var recursive = (bool?)p["recursive"] ?? false;
            var targets = PersistenceBatch.Expand(roots, id => {
                if (!recursive) return Enumerable.Empty<PdmObjectId>();
                TopSolidHost.Pdm.GetConstituents(id, out var folders, out var documents);
                return folders.Concat(documents);
            });
            var rows = new JArray(targets.OrderBy(id => id.Id, StringComparer.Ordinal).Select(PersistenceRow));
            PersistenceBatch.ValidatePreview(rows);
            return new JObject { ["operation"] = "checkIn", ["recursive"] = recursive, ["roots"] = p["pdmObjectIds"].DeepClone(),
                ["targets"] = rows, ["count"] = rows.Count, ["complete"] = true, ["undoable"] = false };
        }
        public JObject CheckInPdmObjects(JObject p) => CheckInPdmObjects(p, PreviewCheckIn(p));
        public JObject CheckInPdmObjects(JObject p, JObject preview)
        {
            RequirePersistenceIdle();
            var targets = ((JArray)preview["targets"]).Select(row => new PdmObjectId((string)row["pdmObjectId"])).ToList();
            // Expand recursion during the confirmed preview, then submit that
            // exact set. Native recursion could include newly inserted children
            // that were never part of the reviewed scope.
            return PersistenceBatch.Execute("checkIn", (JArray)preview["targets"],
                () => TopSolidHost.Pdm.CheckInSeveral(targets, false),
                row => PersistenceRow(new PdmObjectId((string)row["pdmObjectId"])));
        }
        public JObject PreviewSaveDocuments(JObject p)
        {
            RequirePersistenceIdle();
            var scope = (string)p["scope"] ?? "openDirty";
            var selected = p["documentIds"] is JArray ids
                ? ids.Select(id => Document(new JObject { ["documentId"] = id.DeepClone() })).ToList()
                : scope == "loadedDirty" ? TopSolidHost.Documents.GetDocuments() : TopSolidHost.Documents.GetOpenDocuments();
            if (selected.Count > 1000) throw new ArgumentException("More than 1000 documents are in scope. Select explicit documents.");
            if (p["documentIds"] != null)
            {
                var loaded = new HashSet<DocumentId>(TopSolidHost.Documents.GetDocuments());
                if (selected.Any(doc => !loaded.Contains(doc))) throw new ArgumentException("Explicit save targets must be loaded document revisions. Refresh document summaries before saving.");
            }
            // Saving a synchronized document also affects its synchronized group.
            var dirty = selected.Where(doc => TopSolidHost.Documents.IsDirty(doc) || TopSolidHost.Pdm.IsDirty(TopSolidHost.Documents.GetPdmObject(doc)));
            var targets = PersistenceBatch.Expand(dirty, doc => TopSolidHost.Documents.IsSynchronized(doc)
                ? TopSolidHost.Documents.GetSynchronizedDocuments(doc) : Enumerable.Empty<DocumentId>());
            var rows = new JArray(targets.OrderBy(doc => doc.PdmDocumentId, StringComparer.Ordinal).Select(doc => {
                var row = PersistenceRow(TopSolidHost.Documents.GetPdmObject(doc));
                if (!TopSolidHost.Documents.GetDocument(new PdmObjectId((string)row["pdmObjectId"])).Equals(doc))
                    throw new ArgumentException("A selected document is not its current PDM document revision. Refresh document summaries; no save was submitted.");
                row["documentId"] = doc.PdmDocumentId;
                row["isDirty"] = TopSolidHost.Documents.IsDirty(doc) || (bool)row["isDirty"];
                return row;
            }));
            PersistenceBatch.ValidatePreview(rows);
            return new JObject { ["operation"] = "save", ["scope"] = p["documentIds"] == null ? scope : "explicitDocuments",
                ["targets"] = rows, ["count"] = rows.Count, ["complete"] = true, ["includesSynchronizedDocuments"] = true,
                ["checkInPerformed"] = false, ["undoable"] = false };
        }
        public JObject SaveDocuments(JObject p) => SaveDocuments(p, PreviewSaveDocuments(p));
        public JObject SaveDocuments(JObject p, JObject preview)
        {
            RequirePersistenceIdle(); var rows = (JArray)preview["targets"];
            var ids = rows.Select(row => new PdmObjectId((string)row["pdmObjectId"])).Distinct().ToList();
            return PersistenceBatch.Execute("save", rows, () => TopSolidHost.Pdm.SaveSeveral(ids, false), row => {
                var after = PersistenceRow(new PdmObjectId((string)row["pdmObjectId"]));
                after["documentId"] = row["documentId"].DeepClone();
                after["isDirty"] = (bool)after["isDirty"] || TopSolidHost.Documents.IsDirty(new DocumentId((string)row["documentId"]));
                return after;
            });
        }
    }
}
