using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class PdmIdentityTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_resolve_pdm_documents", "Resolve up to 100 PDM objects to verified backing DocumentIds, keeping both ID kinds, names and document type. Projects/libraries may have metadata documents; do not assume they are modeling documents. Returns null where no document exists. No opening; use explicit minor revisions for historical work.",
                BatchRead.Paging(new JObject { ["pdmObjectIds"] = Schema.Array(Schema.Text("PdmObjectId, not DocumentId or type GUID."), 1, 100) }),
                p => a.Read("kernel", () => BatchRead.Page((JArray)p["pdmObjectIds"], p, v => new JObject { ["pdmObjectId"] = v.DeepClone() }, v => {
                    var id = a.Pdm(new JObject { ["pdmObjectId"] = v.DeepClone() }); var row = ObjectIdentity.Pdm(id);
                    var doc = TopSolidHost.Documents.GetDocument(id); var valid = !doc.IsEmpty && TopSolidHost.Documents.Exists(doc);
                    if (valid && !TopSolidHost.Documents.GetPdmObject(doc).Equals(id)) throw new InvalidOperationException("Backing document does not map to the requested PDM object.");
                    row["documentId"] = valid ? AutomationValues.Json(doc) : JValue.CreateNull();
                    row["documentType"] = valid ? TopSolidHost.Documents.GetTypeFullName(doc) : null;
                    row["revisionSelection"] = valid ? "latestMinorRevision" : "noBackingDocument"; return row;
                })), "Pdm", new[] { "pdmObjectIds" }, api: ObjectIdentity.PdmApi.Concat(ApiRefs.Kernel("IDocuments.GetDocument", "IDocuments.Exists", "IDocuments.GetPdmObject", "IDocuments.GetTypeFullName")).ToArray()));
            register(new ToolDefinition("topsolid_find_pdm_documents", "Find document candidates by exact PDM name or universal (domain,name). Names can match multiple objects; preserve all candidates and ask for selection when ambiguous. Optional projectId scopes the query. Results include friendly names, owner, type and exact PDM IDs.",
                BatchRead.Paging(new JObject { ["name"] = Schema.Text("Exact PDM document name or universal name.", 256), ["domain"] = Schema.Text("With name, use universal-identifier lookup instead of display-name search.", 256), ["projectId"] = Schema.Text("Optional working/library project PdmObjectId. Omit to search all projects.") }),
                p => a.Read("kernel", () => {
                    var project = PdmObjectId.Empty;
                    if (p["projectId"] != null) { project = a.Pdm(p, "projectId"); var type = TopSolidHost.Pdm.GetType(project, out _);
                        if (type != PdmObjectType.WorkingProject && type != PdmObjectType.LibraryProject) throw new ArgumentException("projectId must identify a working or library project, not a folder or document."); }
                    var ids = p["domain"] == null ? TopSolidHost.Pdm.SearchDocumentByName(project, (string)p["name"]) :
                        new[] { TopSolidHost.Pdm.SearchDocumentByUniversalId(project, (string)p["domain"], (string)p["name"]) }.Where(id => !id.IsEmpty).ToList();
                    var result = BatchRead.Page(ids, p, id => new JObject { ["pdmObjectId"] = id.Id }, ObjectIdentity.Pdm);
                    result["resolution"] = Resolution(ids.Count); result["lookupKind"] = p["domain"] == null ? "pdmName" : "universalId"; return result;
                }), "Pdm", new[] { "name" }, api: ObjectIdentity.PdmApi.Concat(ApiRefs.Kernel("IPdm.SearchDocumentByName", "IPdm.SearchDocumentByUniversalId")).ToArray()));
            register(new ToolDefinition("topsolid_list_pdm_document_revisions", "List every major/minor revision relationship of one TopSolid PDM document, with revision text and exact historical DocumentId. IDs remain opaque; do not derive them by editing strings. Does not open or restore revisions.",
                BatchRead.Paging(new JObject { ["pdmObjectId"] = Schema.Text("TopSolid document PdmObjectId, not a revision ID.") }),
                p => a.Read("kernel", () => {
                    var id = a.Pdm(p);
                    if (TopSolidHost.Pdm.GetType(id, out _) != PdmObjectType.TopSolidDocument) throw new ArgumentException("Revision-to-DocumentId mapping requires a TopSolid document PDM object.");
                    var rows = TopSolidHost.Pdm.GetMajorRevisions(id).SelectMany(major => TopSolidHost.Pdm.GetMinorRevisions(major).Select(minor => new { major, minor })).ToList();
                    return BatchRead.Page(rows, p, row => new JObject { ["pdmObjectId"] = id.Id, ["pdmMajorRevisionId"] = AutomationValues.Json(row.major), ["pdmMinorRevisionId"] = AutomationValues.Json(row.minor) },
                        row => new JObject { ["majorRevisionText"] = TopSolidHost.Pdm.GetMajorRevisionText(row.major), ["minorRevisionText"] = TopSolidHost.Pdm.GetMinorRevisionText(row.minor),
                            ["documentId"] = AutomationValues.Json(TopSolidHost.Documents.GetMinorRevisionDocument(row.minor)) });
                }), "Pdm", new[] { "pdmObjectId" }, api: ApiRefs.Kernel("IPdm.GetType", "IPdm.GetMajorRevisions", "IPdm.GetMinorRevisions", "IPdm.GetMajorRevisionText", "IPdm.GetMinorRevisionText", "IDocuments.GetMinorRevisionDocument")));
        }
        internal static string Resolution(int count) => count == 0 ? "notFound" : count == 1 ? "unique" : "ambiguous";
    }
}
