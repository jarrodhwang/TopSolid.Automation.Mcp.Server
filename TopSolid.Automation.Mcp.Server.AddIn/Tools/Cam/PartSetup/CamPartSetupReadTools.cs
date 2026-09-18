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
    internal static class CamPartSetupReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_cam_parts", "List cam parts. Includes native friendly names; keep handles internal in user mode.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("cam", () => AutomationValues.Page(TopSolidCamHost.Documents.GetParts(a.Document(p)), p, id => CamNames.Named(id))), "Cam/PartSetup", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IDocuments.GetParts.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetFriendlyName.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetName.html" }));
            register(new ToolDefinition("topsolid_is_part_setup_document", "Is part setup document.", new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") },
                p => a.Read("cam", () => AutomationValues.Result(TopSolidCamHost.PartSettingDocuments.IsPartSetting(a.Document(p)))), "Cam/PartSetup", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IPartSettingDocument.IsPartSetting.html" }));
            register(new ToolDefinition("topsolid_list_cam_coordinate_systems", "List cam coordinate systems. Includes native friendly names; keep handles internal in user mode.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("cam", () => AutomationValues.Page(TopSolidCamHost.Documents.GetWCSs(a.Document(p)), p, id => CamNames.Named(id))), "Cam/PartSetup", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IDocuments.GetWCSs.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetFriendlyName.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetName.html" }));
        }
    }
}
