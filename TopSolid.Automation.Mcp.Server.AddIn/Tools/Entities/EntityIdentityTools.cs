using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class EntityIdentityTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_find_named_elements", "Find all exact internal-name or friendly-name matches in one document revision. Friendly names may be duplicated or translated: ambiguous results require explicit handle selection before changing anything. Scans native elements once, returns named, typed handles; never treats a type GUID as an element ID.",
                BatchRead.Paging(new JObject { ["documentId"] = Schema.Text("Exact document revision; omit for the currently edited document."), ["name"] = Schema.Text("Exact name to match, case-sensitive.", 256),
                    ["nameKind"] = Schema.Choice("Default friendlyName. Internal system names use their untranslated $... value.", "name", "friendlyName") }),
                p => a.Read("kernel", () => {
                    var doc = a.Document(p); var ids = TopSolidHost.Elements.GetElements(doc);
                    if (ids.Count > 10000) throw new ArgumentException("This document has over 10,000 elements. Narrow it with list_named_elements kind and select exact handles.");
                    var friendly = (string)p["nameKind"] != "name";
                    var matches = ids.Where(id => string.Equals(friendly ? TopSolidHost.Elements.GetFriendlyName(id) : TopSolidHost.Elements.GetName(id), (string)p["name"], StringComparison.Ordinal)).ToList();
                    var result = BatchRead.Page(matches, p, EntityBatchReadTools.Identity, ObjectIdentity.Element);
                    result["resolution"] = PdmIdentityTools.Resolution(matches.Count); result["documentId"] = doc.PdmDocumentId;
                    result["nameKind"] = friendly ? "friendlyName" : "name"; return result;
                }), "Entities", new[] { "name" }, api: ObjectIdentity.ElementApi.Concat(ApiRefs.Kernel("IElements.GetElements")).ToArray()));
        }
    }
}
