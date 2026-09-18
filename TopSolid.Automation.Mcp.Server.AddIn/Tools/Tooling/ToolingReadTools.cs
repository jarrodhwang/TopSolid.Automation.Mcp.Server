// Generated from the reviewed allowlist in scripts/ApiReference/generate_read_tools.py.
// Each call is compiled against the matched 7.20 SDK. No generic API invocation.
using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;
using TopSolid.Cad.Design.Automating;
using TopSolid.Cad.Drafting.Automating;
using TopSolid.Cad.Electrode.Automating;
using TopSolid.Cam.NC.Kernel.Automating;
using TopSolid.Cae.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ToolingReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_get_part_material", "Get part material.", new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") },
                p => a.Read("cad", () => AutomationValues.Result(TopSolidDesignHost.Parts.GetMaterial(a.Document(p)))), "Tooling", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IParts.GetMaterial.html" }));
            register(new ToolDefinition("topsolid_get_base_document", "Get base document.", new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") },
                p => a.Read("cad", () => AutomationValues.Result(TopSolidDesignHost.Tools.GetBaseDocument(a.Document(p)))), "Tooling", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.ITools.GetBaseDocument.html" }));
            register(new ToolDefinition("topsolid_list_cam_tools", "List cam tools. Includes native friendly names; keep handles internal in user mode.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("cam", () => AutomationValues.Page(TopSolidCamHost.Documents.GetTools(a.Document(p), false), p, id => CamNames.Named(id))), "Tooling", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IDocuments.GetTools.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetFriendlyName.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetName.html" }));
        }
    }
}
