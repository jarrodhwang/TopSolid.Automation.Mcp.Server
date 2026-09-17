using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class PdmActionTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_get_template_projects", "Get user/company document and project template project IDs only when the user explicitly requests a template. Ordinary document creation uses an extension with no template; do not browse these projects to create a plain document.", new JObject(),
                p => a.Read("kernel", () => { TopSolidHost.Pdm.GetTemplates(out var userDocs, out var companyDocs, out var userProjects, out var companyProjects);
                    return new JObject { ["userDocumentTemplates"] = AutomationValues.Json(userDocs), ["companyDocumentTemplates"] = AutomationValues.Json(companyDocs),
                        ["userProjectTemplates"] = AutomationValues.Json(userProjects), ["companyProjectTemplates"] = AutomationValues.Json(companyProjects) }; }),
                "Pdm", api: ApiRefs.Kernel("IPdm.GetTemplates")));
            foreach (var kind in new[] { "project", "folder", "document" })
            {
                var operation = kind;
                var properties = new JObject { ["name"] = Schema.Text("Name of the new " + kind + ".", 128) };
                if (kind != "project") properties["ownerId"] = Schema.Text("Destination working project or folder ID, from a PDM query.");
                if (kind == "document")
                {
                    properties["templateId"] = Schema.Text("Existing native document template PDM ID, ONLY when the user requests that template. Mutually exclusive with extension.");
                    properties["extension"] = Schema.Text("Native extension including dot: .TopPrt part, .TopAsm assembly, or another value from the user/local catalog/live query. No loaded example required. .TopPrj uses create_project; .pdf/.png/etc require import.", 64);
                    properties["useDefaultTemplate"] = Schema.Boolean("Default FALSE: create an empty document by extension. Set true ONLY if the user explicitly requests the configured default template. Not valid with templateId.");
                }
                register(new ToolDefinition("topsolid_create_" + kind, "Create a " + kind + " in PDM after explicit confirmation. " + (kind == "document" ? "Default: empty document by extension, no template or loaded/open example required. Use templateId only for a requested specific template. " : "") + "Creation is not undoable. Return its real ID for subsequent tools; do not retry after an uncertain result.",
                    properties, p => a.CreatePdmObject(operation, p), "Pdm", kind == "project" ? new[] { "name" } : new[] { "ownerId", "name" }, false,
                    ApiRefs.Kernel(kind == "project" ? "IPdm.CreateProject" : kind == "folder" ? "IPdm.CreateFolder" : "IPdm.CreateDocument",
                        "IPdm.CreateDocumentWithTemplate", "IPdm.GetType", "IPdm.SetName", "IDocuments.GetDocument", "IApplication.ActiveCommandName", "IApplication.ActiveCommandFullName"),
                    kind == "document" ? (Action<JObject>)ValidateDocument : null,
                    p => a.PreviewPdmCreation(operation, p), "Create one new " + kind + " in PDM. Persistent and outside geometry undo. No automatic check-in, opening or deletion.",
                    defaults: kind == "document" ? "useDefaultTemplate=false; no template unless explicitly selected" : null));
            }
        }
        internal static void ValidateDocument(JObject p)
        {
            if ((p["templateId"] == null) == (p["extension"] == null)) throw new ArgumentException("Supply exactly one templateId or extension.");
            if (p["templateId"] != null && p["useDefaultTemplate"] != null) throw new ArgumentException("useDefaultTemplate only applies with extension.");
            if (p["extension"] != null) DocumentCreationCatalog.Extension((string)p["extension"]);
        }
    }
}
