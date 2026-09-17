using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Cae.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class CaeDetailsTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_get_cae_result_ranges", "Read existing von Mises and displacement extrema. Does not run a solver. Verify the result units in the CAE document before engineering interpretation.",
                new JObject { ["documentId"] = Schema.Text("CAE result document ID; omit for active.") }, p => a.Read("cae", () =>
                {
                    var id = a.Document(p);
                    TopSolidCaeHost.Results.GetVonMisesMinAndMaxResults(id, out var stressMin, out var stressMax);
                    TopSolidCaeHost.Results.GetDisplacementMinAndMaxResults(id, out var displacementMin, out var displacementMax);
                    return new JObject { ["documentId"] = id.PdmDocumentId, ["vonMises"] = new JObject { ["min"] = stressMin, ["max"] = stressMax },
                        ["displacement"] = new JObject { ["min"] = displacementMin, ["max"] = displacementMax }, ["units"] = "API result units; these method references do not specify unit symbols." };
                }), "Cae", api: ApiRefs.For("cae", "TopSolid.Cae.Kernel.Automating", "IResults.GetVonMisesMinAndMaxResults", "IResults.GetDisplacementMinAndMaxResults")));
        }
    }
}
