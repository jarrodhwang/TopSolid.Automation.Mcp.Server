using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void Persistence()
        {
            var reads = 0;
            var scope = PersistenceBatch.Expand(new[] { 0, 0 }, id => { reads++; return id == 0 ? new[] { 1, 2 } : id == 1 ? new[] { 2, 0 } : new int[0]; });
            Check(scope.SequenceEqual(new[] { 0, 1, 2 }) && reads == 3, "Recursive persistence must deduplicate roots/descendants and stop cycles");
            Throws<ArgumentException>(() => PersistenceBatch.Expand(new[] { 0 }, id => new[] { id + 1 }));
            var before = JArray.Parse("[{pdmObjectId:'a',name:'A',isDirty:true},{pdmObjectId:'b',name:'B',isDirty:true}]");
            var submissions = 0;
            JObject Clean(JObject row) { var r = (JObject)row.DeepClone(); r["isDirty"] = false; r["state"] = "ExclusiveModification"; return r; }
            var saved = PersistenceBatch.Execute("save", before, () => submissions++, Clean);
            Check(submissions == 1 && (bool)saved["saved"] && !(bool)saved["checkInPerformed"], "Batch save must submit once without claiming check-in");
            Check((bool)before[0]["isDirty"], "Readback mutated the approved snapshot");
            var dirty = PersistenceBatch.Execute("save", before, () => submissions++, row => (JObject)row.DeepClone());
            Check(!(bool)dirty["saved"] && (int)dirty["savedCount"] == 0, "A returning native call is not proof that documents are clean");
            var checkedIn = PersistenceBatch.Execute("checkIn", before, () => submissions++, row => { var r = Clean(row); r["state"] = (string)row["pdmObjectId"] == "a" ? "CheckedIn" : "ExclusiveModification"; return r; });
            Check((bool)checkedIn["complete"] && (int)checkedIn["checkedInCount"] == 1 && !(bool)checkedIn["checkInVerifiedForAll"], "Check-in must report unverified states without claiming every object checked in");
            var callsBeforeFailure = submissions;
            var partial = Throws<PartialChangeException>(() => PersistenceBatch.Execute("checkIn", before, () => { submissions++; throw new Exception("native partial failure"); }, row => {
                if ((string)row["pdmObjectId"] == "b") throw new Exception("read unavailable"); return Clean(row);
            }));
            Check(submissions == callsBeforeFailure + 1 && ((JArray)partial.Receipt["after"]).Count == 2 && partial.Receipt["after"][1]["readError"] != null && !(bool)partial.Receipt["complete"], "Partial write must retain every target and never retry");
            var empty = PersistenceBatch.Execute("save", new JArray(), () => { throw new Exception("empty submission"); }, Clean);
            Check((bool)empty["noChangesNeeded"] && !(bool)empty["nativeCallCompleted"], "Empty save must not claim a native write happened");
            Throws<ArgumentException>(() => PersistenceBatch.Execute("save", new JArray(new JObject { ["name"] = new string('x', 24001) }), () => { throw new Exception("Oversized preview reached write"); }, Clean));
            var snapshots = 0; var submitted = 0; var changed = false;
            var reviewed = new ToolDefinition("reviewed_batch", "Use exact reviewed scope", new JObject(), p => { throw new Exception("Batch rediscovered targets instead of using reviewed scope"); }, "Pdm", readOnly: false,
                preview: p => { snapshots++; return new JObject { ["ids"] = changed ? new JArray("a", "b") : new JArray("a") }; },
                executePrepared: (p, target) => { submitted++; Check(((JArray)target["ids"]).Count == 1, "Execution received unreviewed descendants"); return new JObject { ["done"] = true }; });
            var reviewedRegistry = new ToolRegistry(new[] { reviewed }, p => new JObject());
            var proposal = reviewedRegistry.Prepare("reviewed_batch", new JObject());
            reviewedRegistry.Call("reviewed_batch", new JObject(), (string)proposal["confirmationToken"]);
            Check(snapshots == 2 && submitted == 1, "Batch should inspect at prepare and recheck only, never rediscover execution scope");
            proposal = reviewedRegistry.Prepare("reviewed_batch", new JObject()); changed = true;
            Check((bool)reviewedRegistry.Call("reviewed_batch", new JObject(), (string)proposal["confirmationToken"])["isError"] && submitted == 1, "New descendants bypassed the confirmation scope");
            using (var gateway = new AutomationGateway())
            {
                var registry = new ToolRegistry(gateway);
                foreach (var tool in new[] { "topsolid_save_documents", "topsolid_check_in_pdm_objects" })
                {
                    var args = tool.EndsWith("pdm_objects") ? JObject.Parse("{pdmObjectIds:['project-id'],recursive:true}") : new JObject();
                    Check(Throws<RpcException>(() => registry.Call(tool, args)).Code == -32010, "Persistence bypassed confirmation before any native query");
                }
                Check(Throws<RpcException>(() => registry.Prepare("topsolid_save_documents", JObject.Parse("{scope:'openDirty',documentIds:['a']}"))).Code == -32602, "Ambiguous save scope was accepted");
                Check(Throws<RpcException>(() => registry.Prepare("topsolid_check_in_pdm_objects", JObject.Parse("{pdmObjectIds:['a','a']}"))).Code == -32602, "Duplicate check-in roots were accepted");
            }
        }
    }
}
