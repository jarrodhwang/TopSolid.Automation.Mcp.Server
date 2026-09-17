using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void ObjectIdentityRules()
        {
            Check(PdmIdentityTools.Resolution(0) == "notFound" && PdmIdentityTools.Resolution(1) == "unique" && PdmIdentityTools.Resolution(2) == "ambiguous", "Duplicate friendly names must remain ambiguous");
            Throws<ArgumentException>(() => ObjectIdentity.ValidateNamePolicy(true, true, "Renamed"));
            Throws<ArgumentException>(() => ObjectIdentity.ValidateNamePolicy(true, false, "$InventedSystemName"));
            Throws<ArgumentException>(() => ObjectIdentity.ValidateNamePolicy(false, false, "User name"));
            ObjectIdentity.ValidateNamePolicy(true, false, "User name"); // Editability comes from IsRenamable, not HasName.
            DocumentIdentityTools.ValidateUniversal(JObject.Parse("{documentId:'rev',domain:'company',name:'material'}"));
            DocumentIdentityTools.ValidateUniversal(JObject.Parse("{documentId:'rev',remove:true}"));
            Throws<ArgumentException>(() => DocumentIdentityTools.ValidateUniversal(JObject.Parse("{documentId:'rev',domain:'company'}")));
            Throws<ArgumentException>(() => DocumentIdentityTools.ValidateUniversal(JObject.Parse("{documentId:'rev',remove:true,name:'material'}")));
            Throws<ArgumentException>(() => PdmLifecycleTools.ValidateUpdates(JObject.Parse("{changes:[{pdmObjectId:'x'}]}")));
            Throws<ArgumentException>(() => PdmLifecycleTools.ValidateUpdates(JObject.Parse("{changes:[{pdmObjectId:'x',name:'a'},{pdmObjectId:'x',name:'b'}]}")));
            foreach (var type in new[] { PdmObjectType.WorkingProject, PdmObjectType.LibraryProject, PdmObjectType.Folder, PdmObjectType.RecycleBinFolder, PdmObjectType.Shortcut })
                Throws<ArgumentException>(() => PdmLifecycleTools.ValidateTarget(type, PdmObjectState.Default, true, false));
            PdmLifecycleTools.ValidateTarget(PdmObjectType.TopSolidDocument, PdmObjectState.Deleted, true, true);
            Throws<ArgumentException>(() => PdmLifecycleTools.ValidateTarget(PdmObjectType.TopSolidDocument, PdmObjectState.Deleted, true, false));
            Throws<ArgumentException>(() => PdmLifecycleTools.ValidateTarget(PdmObjectType.TopSolidDocument, PdmObjectState.Default, true, true));
            var called = 0;
            var failure = Throws<PartialChangeException>(() => PdmLifecycleTools.ExecuteBatch(JArray.Parse("[{pdmObjectId:'a'},{pdmObjectId:'b'},{pdmObjectId:'c'}]"), (entry, receipt) => {
                called++; receipt["completedProperties"] = new JArray("name");
                if (called == 2) throw new InvalidOperationException("Readback unavailable after submission");
            }));
            Check(called == 2 && (int)failure.Receipt["notAttempted"] == 1, "Stop persistent batch after first failure; never retry or run remaining entries");
            Check((string)failure.Receipt["items"][0]["outcome"] == "completed" && (string)failure.Receipt["items"][1]["outcome"] == "uncertain", "Partial PDM receipt retains completed and uncertain IDs");
            Check(!(bool)failure.Receipt["undoable"] && !(bool)failure.Receipt["complete"], "Persistent PDM batch must not claim rollback/completion");
            var success = PdmLifecycleTools.ExecuteBatch(JArray.Parse("[{pdmObjectId:'a'}]"), (entry, receipt) => receipt["readBackVerified"] = true);
            Check((bool)success["complete"] && (bool)success["items"][0]["readBackVerified"], "Successful PDM batch receipt");
            using (var automation = new AutomationGateway()) {
                var registry = new ToolRegistry(automation);
                foreach (var name in new[] { "topsolid_update_pdm_objects", "topsolid_delete_pdm_documents", "topsolid_restore_pdm_documents", "topsolid_set_document_universal_id" }) {
                    var tool = registry.List().Single(t => (string)t["name"] == name);
                    Check(!(bool)tool["annotations"]["readOnlyHint"] && (bool)tool["_meta"]["topsolid/requiresConfirmation"], "New persistent/identity write must require confirmation");
                }
                var model = registry.Call("topsolid_get_object_model", new JObject());
                Check(!(bool)model["isError"] && model.ToString().Contains("TYPE shared"), "Offline model guide must distinguish type GUID from instance identity");
            }
        }
    }
}
