using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class DocumentIdentityTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_inspect_document_identities", "Resolve exact document revisions to PDM object, major/minor revision IDs and text, name, type GUID and optional universal domain/name. A type GUID is not an object ID. Up to 100 revisions per call; no opening or saving.",
                BatchRead.Paging(new JObject { ["documentIds"] = Schema.Array(Schema.Text("Exact DocumentId returned by a tool."), 1, 100) }),
                p => a.Read("kernel", () => BatchRead.Page((JArray)p["documentIds"], p, v => new JObject { ["documentId"] = v.DeepClone() },
                    v => ObjectIdentity.Document(a.Document(new JObject { ["documentId"] = v.DeepClone() })))), "Documents", new[] { "documentIds" }, api: ObjectIdentity.DocumentApi));
            register(new ToolDefinition("topsolid_set_document_universal_id", "Set or remove a document's universal (domain,name) identifier. This is not a GUID or display name. Requires confirmation and an undoable document modification; does not save. Use deliberate application/company domain and unique name.",
                new JObject { ["documentId"] = Schema.Text("Exact document revision to modify."), ["remove"] = Schema.Boolean("True to remove the universal identifier; default false."),
                    ["domain"] = Schema.Text("Application/company universal domain.", 256), ["name"] = Schema.Text("Unique name within that domain.", 256) },
                p => a.Modify(p, "set universal identifier", "kernel", (doc, current) => {
                    var remove = (bool?)current["remove"] == true;
                    TopSolidHost.Documents.SetUniversalId(doc, remove ? null : (string)current["domain"], remove ? null : (string)current["name"]);
                    TopSolidHost.Documents.GetUniversalId(doc, out var domain, out var name);
                    if (domain != (remove ? null : (string)current["domain"]) || name != (remove ? null : (string)current["name"])) throw new InvalidOperationException("Universal identifier readback did not match. Rolling back.");
                    return new JObject { ["universalId"] = remove ? JValue.CreateNull() : (JToken)new JObject { ["domain"] = domain, ["name"] = name }, ["readBackVerified"] = true };
                }), "Documents", new[] { "documentId" }, false, ApiRefs.Kernel("IDocuments.SetUniversalId", "IDocuments.GetUniversalId", "IPdm.SearchDocumentByUniversalId", "IDocuments.GetPdmObject"),
                ValidateUniversal, p => {
                    var preview = a.PreviewDocument(p); var doc = a.Document(p);
                    TopSolidHost.Documents.GetUniversalId(doc, out var domain, out var name);
                    preview["currentUniversalId"] = new JObject { ["domain"] = domain, ["name"] = name };
                    if ((bool?)p["remove"] != true) {
                        var existing = TopSolidHost.Pdm.SearchDocumentByUniversalId(PdmObjectId.Empty, (string)p["domain"], (string)p["name"]);
                        if (!existing.IsEmpty && !existing.Equals(TopSolidHost.Documents.GetPdmObject(doc))) throw new ArgumentException("That universal identifier resolves to another PDM document. Choose another domain/name.");
                    }
                    return preview;
                }));
        }
        internal static void ValidateUniversal(JObject p)
        {
            MutationReferences.Validate(p);
            bool remove = (bool?)p["remove"] == true;
            if (remove ? p["domain"] != null || p["name"] != null : p["domain"] == null || p["name"] == null)
                throw new ArgumentException("Provide domain and name to set, or remove=true without either field.");
        }
    }
}
