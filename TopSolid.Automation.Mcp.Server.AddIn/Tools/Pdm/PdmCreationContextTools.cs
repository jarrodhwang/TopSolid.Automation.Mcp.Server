using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class PdmCreationContextTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_document_types",
                "Read the local extension catalog without connecting to TopSolid. Native .Top... documents default to empty creation by extension, without templates or loaded examples. .TopPrj uses create_project; external formats require a source-file import. Catalog presence does not prove module/license availability. Use this for an unfamiliar type, not repeatedly for a known .TopPrt part.",
                Schema.Page(new JObject { ["filter"] = Schema.Text("Optional extension substring, case-insensitive.", 64) }),
                p => { var rows = DocumentCreationCatalog.Entries().OfType<JObject>(); var filter = (string)p["filter"];
                    if (!string.IsNullOrEmpty(filter)) rows = rows.Where(r => ((string)r["extension"]).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
                    var result = AutomationValues.Page(rows.ToArray(), p, x => x, 100);
                    result["source"] = "Installation extension list supplied by user on 2026-09-16; not a live capability/license list."; return result; },
                "Pdm", api: ApiRefs.Kernel("IPdm.CreateDocument", "IPdm.CreateDocumentWithTemplate", "IPdm.CreateProject")));
            register(new ToolDefinition("topsolid_get_document_creation_context",
                "Resolve a working project name case-insensitively and empty native document creation options in one MCP read. Reads working-project names only, never loaded documents or templates. extension defaults to .TopPrt; provide another .Top... extension when requested. Returns all matching working projects and active-command readiness. A part can be created with no part currently open. Stop on ambiguous/incomplete results; templates are only for an explicit template request.",
                new JObject { ["projectName"] = Schema.Text("Exact localized friendly name of the destination working project, never a GUID.", 256),
                    ["extension"] = Schema.Text("Native extension including dot; default .TopPrt. No template is used.", 64) },
                p => a.Read("kernel", () => Read(a, (string)p["projectName"], (string)p["extension"] ?? DocumentCreationCatalog.PartExtension)), "Pdm", new[] { "projectName" },
                api: ApiRefs.Kernel("IPdm.GetProjects", "IPdm.GetName", "IApplication.ActiveCommandName", "IApplication.ActiveCommandFullName", "IPdm.CreateDocument"),
                validate: p => DocumentCreationCatalog.Extension((string)p["extension"] ?? DocumentCreationCatalog.PartExtension)));
            register(new ToolDefinition("topsolid_create_part_document",
                "Create one empty native .TopPrt part in an exact working project/folder after confirmation. Supply only ownerId and name. Uses IPdm.CreateDocument(owner,.TopPrt,false): no template lookup, no default template, no loaded/open part prerequisite. Returns real PDM and document IDs; does not open, model or save. Use create_document with templateId only when the user requests a specific template.",
                new JObject { ["ownerId"] = Schema.Text("Exact working project/folder PDM ID from a live PDM tool."), ["name"] = Schema.Text("Requested part file name.", 128) },
                p => a.CreatePdmObject("document", DocumentCreationCatalog.PartArguments(p)), "Pdm", new[] { "ownerId", "name" }, false,
                ApiRefs.Kernel("IPdm.CreateDocument", "IPdm.SetName", "IPdm.GetName", "IPdm.Exists", "IPdm.GetType", "IDocuments.GetDocument", "IApplication.ActiveCommandName", "IApplication.ActiveCommandFullName"),
                preview: p => a.PreviewPdmCreation("document", DocumentCreationCatalog.PartArguments(p)),
                effect: "Create one persistent empty .TopPrt part, without a template. Outside geometry undo. Opening, sketching and saving are separate confirmed actions.",
                defaults: "extension=.TopPrt; useDefaultTemplate=false; no template"));
        }
        private static JObject Read(AutomationGateway a, string projectName, string extension)
        {
            // SearchProjectByName does not reliably return case variants on this
            // host. Compare every working-project name to preserve all duplicates.
            var ids = TopSolidHost.Pdm.GetProjects(true, false).Distinct().ToArray();
            var errors = new JArray(); var projects = new List<JObject>();
            foreach (var id in ids.Take(1000))
            {
                try { projects.Add(new JObject { ["pdmObjectId"] = id.Id, ["name"] = TopSolidHost.Pdm.GetName(id) }); }
                catch (Exception ex) { errors.Add(new JObject { ["pdmObjectId"] = id.Id, ["message"] = AutomationGateway.Describe(ex) }); }
            }
            var result = Describe(projectName, projects, extension, ids.Length <= 1000 && errors.Count == 0);
            result["errors"] = errors; result["readiness"] = a.PdmCreationReadiness(); return result;
        }
        internal static JObject Describe(string projectName, IEnumerable<JObject> projects, string extension, bool complete)
        {
            var matches = new JArray(projects.Where(r => string.Equals((string)r["name"], projectName, StringComparison.OrdinalIgnoreCase)));
            var creation = DocumentCreationCatalog.Describe(extension);
            return new JObject { ["complete"] = complete, ["projectMatches"] = matches, ["creation"] = creation,
                ["documentTypes"] = new JArray(creation.DeepClone()), ["canChooseUniqueProject"] = complete && matches.Count == 1,
                ["nextStep"] = !complete ? "Incomplete lookup; do not create." : matches.Count != 1 ? "Select one exact destination project; do not guess." :
                    "Use create_part_document(ownerId,name) for .TopPrt, or create_document(ownerId,name,extension) for another native type. No template or loaded document is required. Review readiness, then obtain confirmation. Only browse templates if the user explicitly requested one." };
        }
    }
}
