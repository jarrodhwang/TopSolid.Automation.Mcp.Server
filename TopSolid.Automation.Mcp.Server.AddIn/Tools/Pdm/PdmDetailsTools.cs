using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class PdmDetailsTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_get_pdm_object_info", "Read a PDM object's name, description, type, state and owner.",
                new JObject { ["pdmObjectId"] = Schema.Text("PDM object ID returned by a tool.") },
                p => a.Read("kernel", () =>
                {
                    var id = a.Pdm(p);
                    return ObjectIdentity.Pdm(id);
                }), "Pdm", new[] { "pdmObjectId" }, true, ApiRefs.Kernel("IPdm.GetType", "IPdm.GetName", "IPdm.GetDescription", "IPdm.GetState", "IPdm.GetOwner")));
            register(new ToolDefinition("topsolid_list_pdm_children", "List immediate child folders and documents with names. Does not recursively traverse or modify PDM.",
                Schema.Page(new JObject { ["pdmObjectId"] = Schema.Text("Project or folder ID.") }),
                p => a.Read("kernel", () =>
                {
                    TopSolidHost.Pdm.GetConstituents(a.Pdm(p), out var folders, out var documents);
                    var values = folders.Select(id => new { id, kind = "folder" }).Concat(documents.Select(id => new { id, kind = "document" }));
                    return AutomationValues.Page(values, p, entry => new JObject { ["pdmObjectId"] = entry.id.Id, ["kind"] = entry.kind, ["name"] = TopSolidHost.Pdm.GetName(entry.id) });
                }), "Pdm", new[] { "pdmObjectId" }, true, ApiRefs.Kernel("IPdm.GetConstituents", "IPdm.GetName")));
        }
    }
}
