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
    internal static class DraftingReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_drafting_views", "List drafting views.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("drafting", () => AutomationValues.Page(TopSolidDraftingHost.Draftings.GetDraftingViews(a.Document(p)), p)), "Drafting", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/drafting/TopSolid.Cad.Drafting.Automating.IDraftings.GetDraftingViews.html" }));
            register(new ToolDefinition("topsolid_get_drafting_view_title", "Get drafting view title.", new JObject { ["element"] = Schema.Element() },
                p => a.Read("drafting", () => AutomationValues.Result(TopSolidDraftingHost.Draftings.GetViewTitle(a.Element(p)))), "Drafting", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/drafting/TopSolid.Cad.Drafting.Automating.IDraftings.GetViewTitle.html" }));
        }
    }
}
