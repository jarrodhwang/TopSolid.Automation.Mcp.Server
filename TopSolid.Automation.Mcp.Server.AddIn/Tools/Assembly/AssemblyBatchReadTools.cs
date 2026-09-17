using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;
using TopSolid.Cad.Design.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class AssemblyBatchReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_assembly_occurrences", "List assembly parts with occurrence names, definition document names/IDs and transforms together. Rows represent occurrences, so repeated definitions remain separate. Follow nextOffset.",
                BatchRead.Paging(new JObject { ["documentId"] = Schema.Text("Assembly revision; default active.") }),
                p => a.Read("cad", () => BatchRead.Page(TopSolidDesignHost.Assemblies.GetParts(a.Document(p)), p, EntityBatchReadTools.Identity, id =>
                {
                    var definition = TopSolidDesignHost.Assemblies.GetOccurrenceDefinition(id);
                    return new JObject { ["name"] = TopSolidHost.Elements.GetFriendlyName(id), ["definition"] = definition.IsEmpty ? null : a.DocumentSummary(definition),
                        ["transform"] = AutomationValues.Json(TopSolidHost.Geometries3D.GetOccurrenceDefinitionTransform(id)) };
                })), "Assembly", api: ApiRefs.For("cad", "TopSolid.Cad.Design.Automating", "IAssemblies.GetParts", "IAssemblies.GetOccurrenceDefinition")
                    .Concat(ApiRefs.Kernel("IElements.GetFriendlyName", "IDocuments.GetName", "IGeometries3D.GetOccurrenceDefinitionTransform")).ToArray()));
        }
    }
}
