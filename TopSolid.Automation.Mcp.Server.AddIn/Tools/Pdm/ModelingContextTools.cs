using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ModelingContextTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_get_modeling_context",
                "Resolve an EXISTING named working project and document to PDM and current DocumentId in ONE read, including type and whether already open. Prefer this over list_projects + search_documents + resolve_pdm_documents. Returns every duplicate (up to 64); never pick an ambiguous/incomplete result. Does not open/create/change anything. Use get_active_document only when the user means the active document.",
                new JObject { ["projectName"] = Schema.Text("Exact friendly working project name (case-insensitive).", 256),
                    ["documentName"] = Schema.Text("Exact PDM document name within that project.", 256) },
                p => a.Read("kernel", () => Read((string)p["projectName"], (string)p["documentName"])), "Pdm", new[] { "projectName", "documentName" },
                api: ApiRefs.Kernel("IPdm.GetProjects", "IPdm.GetName", "IPdm.SearchDocumentByName", "IPdm.GetType", "IPdm.GetOwner",
                    "IDocuments.GetDocument", "IDocuments.Exists", "IDocuments.GetPdmObject", "IDocuments.GetTypeFullName", "IDocuments.GetOpenDocuments")));
        }
        private static JObject Read(string projectName, string documentName)
        {
            var result = PdmCreationContextTools.FindProjects(projectName);
            var projects = (JArray)result["projectMatches"]; var rows = new JArray(); result["documents"] = rows;
            if (!(bool)result["complete"] || projects.Count != 1) return Finish(result);
            var project = new PdmObjectId((string)projects[0]["pdmObjectId"]);
            var ids = TopSolidHost.Pdm.SearchDocumentByName(project, documentName).Distinct().ToArray();
            var open = new HashSet<DocumentId>(TopSolidHost.Documents.GetOpenDocuments());
            if (ids.Length > 64) result["complete"] = false;
            foreach (var id in ids.Take(64))
            {
                try
                {
                    var name = TopSolidHost.Pdm.GetName(id);
                    if (!string.Equals(name, documentName, StringComparison.OrdinalIgnoreCase)) continue;
                    var type = TopSolidHost.Pdm.GetType(id, out var extension);
                    var row = new JObject { ["pdmObjectId"] = id.Id, ["name"] = name, ["pdmType"] = type.ToString(),
                        ["extension"] = extension, ["ownerId"] = AutomationValues.Json(TopSolidHost.Pdm.GetOwner(id)), ["documentId"] = null };
                    if (type == PdmObjectType.TopSolidDocument)
                    {
                        var doc = TopSolidHost.Documents.GetDocument(id);
                        if (!doc.IsEmpty && TopSolidHost.Documents.Exists(doc))
                        {
                            if (!TopSolidHost.Documents.GetPdmObject(doc).Equals(id)) throw new InvalidOperationException("Backing document does not match the requested PDM object.");
                            row["documentId"] = AutomationValues.Json(doc); row["documentType"] = TopSolidHost.Documents.GetTypeFullName(doc);
                            row["isOpen"] = open.Contains(doc); row["revisionSelection"] = "latestMinorRevision";
                        }
                    }
                    rows.Add(row);
                }
                catch (Exception ex) { result["complete"] = false; ((JArray)result["errors"]).Add(new JObject { ["pdmObjectId"] = id.Id, ["message"] = AutomationGateway.Describe(ex) }); }
            }
            return Finish(result);
        }
        internal static JObject Finish(JObject result)
        {
            var projects = (JArray)result["projectMatches"]; var docs = (JArray)result["documents"];
            var unique = (bool)result["complete"] && projects.Count == 1 && docs.Count == 1 && docs[0]["documentId"]?.Type == JTokenType.String;
            result["canChooseUniqueDocument"] = unique;
            result["nextStep"] = unique ? "Use this documentId for the requested sketch/model action. If isOpen=true do not open it again. Changes require confirmation."
                : "Do not choose a target. Resolve missing/ambiguous project or document names, or incomplete lookup errors, with the user.";
            return result;
        }
    }
}
