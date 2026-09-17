using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Cad.Drafting.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class DraftingDetailsTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_get_drafting_info", "Read page count, projection mode and scale for a drafting document.",
                new JObject { ["documentId"] = Schema.Text("Document ID; omit for active.") }, p => a.Read("drafting", () =>
                {
                    var id = a.Document(p);
                    if (!TopSolidDraftingHost.Draftings.IsDrafting(id)) throw new ArgumentException("This document is not a drafting.");
                    return new JObject { ["documentId"] = id.PdmDocumentId, ["pageCount"] = TopSolidDraftingHost.Draftings.GetPageCount(id),
                        ["projectionMode"] = TopSolidDraftingHost.Draftings.GetProjectionMode(id).ToString(), ["scale"] = TopSolidDraftingHost.Draftings.GetScaleFactorParameterValue(id) };
                }), "Drafting", api: ApiRefs.For("drafting", "TopSolid.Cad.Drafting.Automating", "IDraftings.IsDrafting", "IDraftings.GetPageCount", "IDraftings.GetProjectionMode", "IDraftings.GetScaleFactorParameterValue")));
        }
    }
}
