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
    internal static class CamMachineReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_get_cam_machine", "Get cam machine. Includes native friendly names; keep handles internal in user mode.", new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") },
                p => a.Read("cam", () => AutomationValues.Result(CamNames.Named(TopSolidCamHost.Documents.GetMachine(a.Document(p))))), "Cam/Machine", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IDocuments.GetMachine.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetFriendlyName.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetName.html" }));
            register(new ToolDefinition("topsolid_list_machine_tool_holders", "List machine tool holders. Includes native friendly names; keep handles internal in user mode.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("cam", () => AutomationValues.Page(TopSolidCamHost.Machines.GetToolHolders(a.Element(p)), p, id => CamNames.Named(id))), "Cam/Machine", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IMachineTools.GetToolHolders.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetFriendlyName.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetName.html" }));
            register(new ToolDefinition("topsolid_list_machine_part_holders", "List machine part holders. Includes native friendly names; keep handles internal in user mode.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("cam", () => AutomationValues.Page(TopSolidCamHost.Machines.GetPartHolders(a.Element(p)), p, id => CamNames.Named(id))), "Cam/Machine", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IMachineTools.GetPartHolders.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetFriendlyName.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetName.html" }));
            register(new ToolDefinition("topsolid_list_machine_magazines", "List machine magazines. Includes native friendly names; keep handles internal in user mode.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("cam", () => AutomationValues.Page(TopSolidCamHost.Machines.GetMagazines(a.Element(p)), p, id => CamNames.Named(id))), "Cam/Machine", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IMachineTools.GetMagazines.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetFriendlyName.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetName.html" }));
            register(new ToolDefinition("topsolid_list_machine_pockets", "List machine pockets. Includes native friendly names; keep handles internal in user mode.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("cam", () => AutomationValues.Page(TopSolidCamHost.Machines.GetPockets(a.Element(p)), p, id => CamNames.Named(id))), "Cam/Machine", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IMachineTools.GetPockets.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetFriendlyName.html", "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetName.html" }));
        }
    }
}
