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
    internal static class ElectrodeReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_electrodes", "List electrodes.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("electrode", () => AutomationValues.Page(TopSolidElectrodeHost.Electrodes.GetElectrodes(a.Document(p), false), p)), "Electrode", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/electrode/TopSolid.Cad.Electrode.Automating.IElectrodes.GetElectrodes.html" }));
            register(new ToolDefinition("topsolid_list_electrode_mandrels", "List electrode mandrels.", Schema.Page(new JObject { ["element"] = Schema.Element() }),
                p => a.Read("electrode", () => AutomationValues.Page(TopSolidElectrodeHost.Electrodes.GetElectrodeMandrels(a.Element(p)), p)), "Electrode", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/electrode/TopSolid.Cad.Electrode.Automating.IElectrodes.GetElectrodeMandrels.html" }));
        }
    }
}
