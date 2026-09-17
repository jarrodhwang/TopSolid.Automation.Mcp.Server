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
    internal static class AssemblyReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_assembly_parts", "List assembly parts.", Schema.Page(new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }),
                p => a.Read("cad", () => AutomationValues.Page(TopSolidDesignHost.Assemblies.GetParts(a.Document(p)), p)), "Assembly", new string[0], true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IAssemblies.GetParts.html" }));
            register(new ToolDefinition("topsolid_get_occurrence_document", "Get occurrence document.", new JObject { ["element"] = Schema.Element() },
                p => a.Read("cad", () => AutomationValues.Result(TopSolidDesignHost.Assemblies.GetOccurrenceDefinition(a.Element(p)))), "Assembly", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IAssemblies.GetOccurrenceDefinition.html" }));
            register(new ToolDefinition("topsolid_get_occurrence_transform", "Get occurrence transform.", new JObject { ["element"] = Schema.Element() },
                p => a.Read("kernel", () => AutomationValues.Result(TopSolidHost.Geometries3D.GetOccurrenceDefinitionTransform(a.Element(p)))), "Assembly", new[] { "element" }, true, new[] { "https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IGeometries3D.GetOccurrenceDefinitionTransform.html" }));
        }
    }
}
