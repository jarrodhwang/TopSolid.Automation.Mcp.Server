using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class EntityStructureTools
    {
        internal static readonly string[] Api = EntityBatchReadTools.InfoApi.Concat(ApiRefs.Kernel("IElements.GetParent", "IElements.GetConstituents", "IElements.IsModifiable", "IEntities.IsFolder", "IEntities.IsSetDefinition", "IEntities.IsOccurrence", "IEntities.IsShortcut", "IEntities.GetShortcutTarget", "IEntities.GetOccurrenceDefinition", "IEntities.GetOccurrenceSource", "IOperations.GetChildren")).ToArray();
        internal static JObject Context(ElementId id)
        {
            var row = EntityBatchReadTools.Info(id); row["parentOperation"] = AutomationValues.Json(TopSolidHost.Elements.GetParent(id));
            if ((bool)row["isEntity"]) {
                row["isFolder"] = TopSolidHost.Entities.IsFolder(id); row["isSetDefinition"] = TopSolidHost.Entities.IsSetDefinition(id);
                row["isShortcut"] = TopSolidHost.Entities.IsShortcut(id); row["isOccurrence"] = TopSolidHost.Entities.IsOccurrence(id);
                if ((bool)row["isShortcut"]) row["shortcutTarget"] = Named(TopSolidHost.Entities.GetShortcutTarget(id));
                if ((bool)row["isOccurrence"]) { row["occurrenceDefinition"] = Named(TopSolidHost.Entities.GetOccurrenceDefinition(id)); row["occurrenceSource"] = Named(TopSolidHost.Entities.GetOccurrenceSource(id)); }
            }
            return row;
        }
        private static JToken Named(ElementId id) => id.IsEmpty ? JValue.CreateNull() : (JToken)new JObject { ["element"] = AutomationValues.Json(id), ["friendlyName"] = TopSolidHost.Elements.GetFriendlyName(id), ["type"] = TopSolidHost.Elements.GetTypeFullName(id) };
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_inspect_entity_structure", "Batch-read entity kinds, owner folders, parent operations and shortcut/occurrence targets with names. Owner and parent operation are different relationships. Set contents may be nested sets or shortcuts, not direct geometric entities.",
                BatchRead.Paging(new JObject { ["elements"] = Schema.Array(Schema.Element(), 1, 100) }),
                p => a.Read("kernel", () => BatchRead.Page((JArray)p["elements"], p, v => new JObject { ["element"] = v.DeepClone() }, v => Context(a.Element(new JObject { ["element"] = v.DeepClone() })))), "Entities", new[] { "elements" }, api: Api));
            register(new ToolDefinition("topsolid_list_entity_children", "Read named direct constituents of a folder/composite/set, or generated children of an operation. Paginated, non-recursive. Shortcut targets are resolved; no assumption that constituents are geometry.",
                BatchRead.Paging(new JObject { ["element"] = Schema.Element() }), p => a.Read("kernel", () => {
                    var id = a.Element(p); var operation = TopSolidHost.Operations.IsOperation(id);
                    var page = BatchRead.Page(operation ? TopSolidHost.Operations.GetChildren(id) : TopSolidHost.Elements.GetConstituents(id), p, EntityBatchReadTools.Identity, Context);
                    page["relationship"] = operation ? "operationChildren" : "constituents"; page["parent"] = AutomationValues.Json(id); return page;
                }), "Entities", new[] { "element" }, api: Api));
            var folders = DocumentActionTools.Target(); folders["folders"] = Schema.Array(Schema.Object(new JObject { ["parentFolder"] = Schema.Element(), ["name"] = Schema.Text("New user folder name.", 128) }, "parentFolder", "name"), 1, 32);
            register(new ToolDefinition("topsolid_create_entity_folders", "Create up to 32 document entity folders under verified existing folder entities in one confirmed transaction. These are document-tree folders, distinct from PDM folders. No save.", folders,
                p => a.Modify(p, "create entity folders", "kernel", (doc, current) => {
                    PreflightFolders(a, current); var rows = new JArray();
                    foreach (JObject item in current["folders"]) {
                        var parent = a.Element(new JObject { ["element"] = item["parentFolder"].DeepClone() });
                        var id = TopSolidHost.Entities.CreateFolder(parent); AutomationGateway.RequireValid(id, "entity folder"); CreationNames.SetAndVerify(TopSolidHost.Elements, id, (string)item["name"]);
                        if (!TopSolidHost.Entities.IsFolder(id) || !TopSolidHost.Elements.GetOwner(id).Equals(parent) || TopSolidHost.Elements.GetName(id) != (string)item["name"]) throw new InvalidOperationException("Folder name/owner readback differs; rolling back.");
                        rows.Add(Context(id));
                    }
                    return new JObject { ["items"] = rows, ["created"] = rows.Count };
                }), "Entities", new[] { "documentId", "folders" }, false, Api.Concat(ApiRefs.Kernel("IEntities.CreateFolder", "IElements.SetName", "IElements.SearchByName")).ToArray(), ValidateFolders,
                p => { var preview = a.PreviewDocument(p); preview["parents"] = PreflightFolders(a, p); return preview; }));
            var move = DocumentActionTools.Target(); move["elements"] = Schema.Array(Schema.Element(), 1, 32); move["destinationFolder"] = Schema.Element();
            register(new ToolDefinition("topsolid_move_entities", "Move up to 32 document entities into an existing entity folder, appended in input order. Checks ownership cycles and native modifiability. Moves organization only; geometry transforms use translate tools. Confirmed, undoable, no save.", move,
                p => a.Modify(p, "move entities", "kernel", (doc, current) => {
                    PreflightMove(a, current); var destination = a.Element(new JObject { ["element"] = current["destinationFolder"].DeepClone() }); var rows = new JArray();
                    foreach (var item in (JArray)current["elements"]) {
                        var id = a.Element(new JObject { ["element"] = item.DeepClone() }); TopSolidHost.Entities.MoveEntity(id, destination, -1);
                        if (!TopSolidHost.Elements.GetOwner(id).Equals(destination)) throw new InvalidOperationException("Entity owner readback differs; rolling back.");
                        rows.Add(Context(id));
                    }
                    return new JObject { ["items"] = rows, ["moved"] = rows.Count };
                }), "Entities", new[] { "documentId", "elements", "destinationFolder" }, false, Api.Concat(ApiRefs.Kernel("IEntities.MoveEntity")).ToArray(), p => BatchInput.Elements(p),
                p => { var preview = a.PreviewDocument(p); preview["entities"] = PreflightMove(a, p); preview["destination"] = Context(a.Element(new JObject { ["element"] = p["destinationFolder"].DeepClone() })); return preview; }));
        }
        internal static void ValidateFolders(JObject p)
        {
            MutationReferences.Validate(p);
            foreach (JObject item in p["folders"]) ParameterValueInput.ValidateName((string)item["name"]);
        }
        private static void RequireFolder(ElementId id) { if (!TopSolidHost.Entities.IsFolder(id) || !TopSolidHost.Elements.IsModifiable(id)) throw new ArgumentException("Target must be a modifiable document entity folder."); }
        private static JArray PreflightFolders(AutomationGateway a, JObject p)
        {
            var doc = a.Document(p); var rows = new JArray();
            foreach (JObject item in p["folders"]) {
                var parent = a.Element(new JObject { ["element"] = item["parentFolder"].DeepClone() }); RequireFolder(parent);
                if (!TopSolidHost.Elements.SearchByName(doc, (string)item["name"]).IsEmpty || TopSolidHost.Elements.GetConstituents(parent).Any(id => TopSolidHost.Elements.GetName(id) == (string)item["name"])) throw new ArgumentException("Folder name already exists at this destination.");
                rows.Add(Context(parent));
            }
            return rows;
        }
        internal static void RequireAcyclic(ElementId id, ElementId destination, Func<ElementId, ElementId> owner)
        {
            var seen = new HashSet<ElementId>(); var current = destination;
            while (!current.IsEmpty) {
                if (current.Equals(id)) throw new ArgumentException("Cannot move an entity into itself or its descendant.");
                if (!seen.Add(current) || seen.Count > 1024) throw new ArgumentException("Existing ownership is cyclic or too deep to validate.");
                current = owner(current);
            }
        }
        private static JArray PreflightMove(AutomationGateway a, JObject p)
        {
            var destination = a.Element(new JObject { ["element"] = p["destinationFolder"].DeepClone() }); RequireFolder(destination); var rows = new JArray();
            foreach (var item in (JArray)p["elements"]) {
                var id = a.Element(new JObject { ["element"] = item.DeepClone() }); ObjectIdentity.RequireEntity(id);
                if (!TopSolidHost.Elements.IsModifiable(id)) throw new ArgumentException("TopSolid reports the entity is not modifiable.");
                RequireAcyclic(id, destination, TopSolidHost.Elements.GetOwner); rows.Add(Context(id));
            }
            return rows;
        }
    }
}
