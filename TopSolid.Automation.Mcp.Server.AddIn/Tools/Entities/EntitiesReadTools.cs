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
    internal static class EntitiesReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_elements", "List elements.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Elements.GetElements(a.Document(p)), p)), "Entities", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetElements.html" }));
            register(new ToolDefinition("topsolid_find_element", "Find an element with a unique name. Elements without unique names are not found by this API.", new JObject { ["documentId"] = Schema.Text("Document revision ID; omit for active."), ["name"] = Schema.Text("Exact system or element name.",256) },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Elements.SearchByName(a.Document(p), (string)p["name"]))), "Entities", new[] { "name" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.SearchByName.html" }));
            register(new ToolDefinition("topsolid_list_parameters", "List parameters.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Parameters.GetParameters(a.Document(p)), p)), "Entities", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IParameters.GetParameters.html" }));
            register(new ToolDefinition("topsolid_list_functions", "List functions.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Entities.GetFunctions(a.Document(p)), p)), "Entities", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IEntities.GetFunctions.html" }));
            register(new ToolDefinition("topsolid_list_publishings", "List publishings.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Entities.GetPublishings(a.Document(p)), p)), "Entities", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IEntities.GetPublishings.html" }));
        }
    }
}
