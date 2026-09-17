using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class PdmBatchReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_inspect_pdm_objects", "Read friendly names, descriptions, type, state and owner for up to 100 supplied PDM IDs in one call. Continue with nextOffset if needed. One inaccessible object does not discard the other results.",
                BatchRead.Paging(new JObject { ["pdmObjectIds"] = Schema.Array(Schema.Text("Exact returned PDM object ID."), 1, 100) }),
                p => a.Read("kernel", () => BatchRead.Page((JArray)p["pdmObjectIds"], p, id => new JObject { ["pdmObjectId"] = id.DeepClone() }, token =>
                {
                    var id = a.Pdm(new JObject { ["pdmObjectId"] = token.DeepClone() });
                    return ObjectIdentity.Pdm(id);
                })), "Pdm", new[] { "pdmObjectIds" }, api: ApiRefs.Kernel("IPdm.GetName", "IPdm.GetDescription", "IPdm.GetType", "IPdm.GetState", "IPdm.GetOwner")));
        }
    }
}
