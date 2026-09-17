using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class EntityBatchActionTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            var update = DocumentActionTools.Target();
            update["changes"] = Schema.Array(Schema.Object(new JObject { ["element"] = Schema.Element(), ["name"] = Schema.Text("New unique name.", 128),
                ["description"] = Schema.Text("New description.", 1024), ["comment"] = Schema.Text("New comment.", 1024), ["visible"] = Schema.Boolean("Show or hide.") }, "element"), 1, BatchInput.MaximumChanges);
            register(new ToolDefinition("topsolid_update_elements", "Change names, descriptions, comments and visibility for up to 32 elements in one confirmed undoable modification. Unspecified properties stay unchanged. Does not save.", update,
                p => a.Modify(p, "update elements", "kernel", (doc, current) =>
                {
                    var result = new JArray();
                    foreach (JObject change in current["changes"])
                    {
                        var id = a.Element(change);
                        if (change["name"] != null) { ObjectIdentity.ValidateRename(id, (string)change["name"]); TopSolidHost.Elements.SetName(id, (string)change["name"]); }
                        if (change["description"] != null) TopSolidHost.Elements.SetDescription(id, (string)change["description"]);
                        if (change["comment"] != null) TopSolidHost.Elements.SetComment(id, (string)change["comment"]);
                        if (change["visible"] != null) { if ((bool)change["visible"]) TopSolidHost.Elements.Show(id); else TopSolidHost.Elements.Hide(id); }
                        if (change["name"] != null && TopSolidHost.Elements.GetName(id) != (string)change["name"] ||
                            change["description"] != null && TopSolidHost.Elements.GetDescription(id) != (string)change["description"] ||
                            change["comment"] != null && TopSolidHost.Elements.GetComment(id) != (string)change["comment"] ||
                            change["visible"] != null && TopSolidHost.Elements.IsVisible(id) != (bool)change["visible"])
                            throw new InvalidOperationException("Element property readback differs from the requested change; rolling back the batch.");
                        var row = EntityBatchReadTools.Identity(id); row["updatedProperties"] = new JArray(change.Properties().Where(prop => prop.Name != "element").Select(prop => prop.Name));
                        row["readBackVerified"] = true; result.Add(row);
                    }
                    return new JObject { ["items"] = result, ["changed"] = result.Count };
                }), "Entities", new[] { "documentId", "changes" }, false,
                EntityBatchReadTools.InfoApi.Concat(ApiRefs.Kernel("IElements.SetName", "IElements.SearchByName", "IElements.SetDescription", "IElements.SetComment", "IElements.GetDescription", "IElements.GetComment", "IElements.Show", "IElements.Hide")).ToArray(),
                ValidateUpdates, p => { foreach (JObject change in p["changes"]) if (change["name"] != null) { a.ConnectModule("kernel"); ObjectIdentity.ValidateRename(a.Element(change), (string)change["name"]); } return Preview(a, p, "changes", true, Current); }));

            var targets = DocumentActionTools.Target(); targets["elements"] = Schema.Array(Schema.Element(), 1, BatchInput.MaximumChanges);
            register(new ToolDefinition("topsolid_delete_elements", "Delete up to 32 explicitly selected document elements using native DeleteSeveral. Requires confirmation; dependent features can be affected. One undoable modification, no save or PDM deletion. Refresh document state after this action.", targets,
                p => a.Modify(p, "delete elements", "kernel", (doc, current) =>
                {
                    var ids = ((JArray)current["elements"]).Select(v => a.Element(new JObject { ["element"] = v.DeepClone() })).ToList();
                    if (ids.Any(id => !TopSolidHost.Elements.IsDeletable(id))) throw new ArgumentException("The batch contains an element TopSolid does not allow to be deleted.");
                    TopSolidHost.Elements.DeleteSeveral(ids);
                    if (ids.Any(TopSolidHost.Elements.Exists)) throw new InvalidOperationException("TopSolid did not delete every requested element; rolling back the batch.");
                    return new JObject { ["deletedElements"] = AutomationValues.Json(ids), ["deleted"] = ids.Count, ["refreshRequired"] = true };
                }), "Entities", new[] { "documentId", "elements" }, false,
                EntityBatchReadTools.InfoApi.Concat(ApiRefs.Kernel("IElements.DeleteSeveral", "IElements.Exists")).ToArray(),
                p => BatchInput.Elements(p), p => Preview(a, p, "elements", false, id =>
                {
                    var row = EntityBatchReadTools.Info(id);
                    if (!(bool)row["deletable"]) throw new ArgumentException("An element is not deletable.");
                    return row;
                }), "Delete the listed document elements. Dependent features may become invalid or be removed by TopSolid. Includes synchronized documents. One undoable transaction; no save, PDM deletion or automatic retry."));

            var translate = (JObject)targets.DeepClone(); translate["translation"] = ShapeWorkflowTools.Vector("Translation in input length units");
            translate["units"] = Schema.Choice("Length units; default mm.", "mm", "cm", "m");
            register(new ToolDefinition("topsolid_translate_elements", "Translate up to 32 entities by the same vector in one confirmed modification. Creates native transform operations. Does not copy entities or save.", translate,
                p => a.Modify(p, "translate elements", "kernel", (doc, current) =>
                {
                    var transform = SpatialInput.Translation(current); var rows = new JArray();
                    foreach (var item in (JArray)current["elements"])
                    {
                        var id = a.Element(new JObject { ["element"] = item.DeepClone() });
                        ObjectIdentity.RequireEntity(id);
                        var operation = TopSolidHost.Entities.Transform(id, transform); AutomationGateway.RequireValid(operation, "transform operation");
                        rows.Add(new JObject { ["element"] = AutomationValues.Json(id), ["operation"] = AutomationValues.Json(operation) });
                    }
                    return new JObject { ["items"] = rows, ["changed"] = rows.Count };
                }), "Entities", new[] { "documentId", "elements", "translation" }, false,
                EntityBatchReadTools.InfoApi.Concat(ApiRefs.Kernel("IEntities.Transform", "Transform3D.SetTranslation")).ToArray(), p => BatchInput.Elements(p),
                p => Preview(a, p, "elements", false, id => { ObjectIdentity.RequireEntity(id); return EntityBatchReadTools.Info(id); }), defaultLengthUnits: "mm"));
        }
        internal static void ValidateUpdates(JObject p)
        {
            BatchInput.Elements(p, "changes", true);
            foreach (JObject change in p["changes"])
                if (change.Properties().Count() == 1) throw new ArgumentException("Every element change needs at least one property to update.");
        }
        internal static JObject Current(ElementId id)
        {
            var row = EntityBatchReadTools.Info(id); row["description"] = TopSolidHost.Elements.GetDescription(id); row["comment"] = TopSolidHost.Elements.GetComment(id); return row;
        }
        internal static JObject Preview(AutomationGateway a, JObject p, string array, bool entries, Func<ElementId, JObject> detail)
        {
            var preview = a.PreviewDocument(p);
            preview["targets"] = new JArray(((JArray)p[array]).Select(v =>
            {
                var id = a.Element(new JObject { ["element"] = (entries ? v["element"] : v).DeepClone() });
                var row = EntityBatchReadTools.Identity(id); row.Merge(detail(id)); return row;
            }));
            return preview;
        }
    }
}
