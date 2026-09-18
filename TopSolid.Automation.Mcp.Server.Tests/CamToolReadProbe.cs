using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;
using TopSolid.Kernel.Automating;
using TopSolid.Cam.NC.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static int CamToolReadProbe()
        {
            using (var gateway = new AutomationGateway())
            {
                gateway.ConnectModule("cam");
                var active = TopSolidHost.Documents.EditedDocument;
                var doc = TopSolidHost.Documents.GetOpenDocuments().First(d => TopSolidHost.Documents.GetTypeFullName(d).EndsWith(".MillTurnDocument", StringComparison.Ordinal));
                var dirty = TopSolidHost.Documents.IsDirty(doc);
                var result = new JArray();
                foreach (var tool in TopSolidCamHost.Documents.GetTools(doc, true).Take(8))
                {
                    var row = new JObject { ["identity"] = AutomationValues.Json(tool), ["name"] = TopSolidHost.Elements.GetFriendlyName(tool),
                        ["type"] = TopSolidHost.Elements.GetTypeFullName(tool) };
                    var values = new JArray(); row["parameters"] = values;
                    foreach (var p in TopSolidCamHost.Tools.GetParameters(tool))
                    {
                        var value = new JObject { ["name"] = p.Name };
                        try { value["value"] = TopSolidCamHost.Parameters.ToInvariantStringValue(p); }
                        catch (Exception error) { value["error"] = error.GetType().Name; }
                        values.Add(value);
                    }
                    result.Add(row);
                }
                Check(TopSolidHost.Documents.IsDirty(doc) == dirty, "Tool inspection changed document dirty state");
                Directory.CreateDirectory("artifacts/cam-tools");
                File.WriteAllText("artifacts/cam-tools/native-tool-parameters.json", result.ToString());
                var summaries = new JArray(TopSolidCamHost.Operations.GetOperations(doc).Select(id => CamNames.Operation(new ElementExId(id))));
                File.WriteAllText("artifacts/cam-tools/native-operation-cards.json", summaries.ToString());
                var page = CamNames.OperationPage(TopSolidCamHost.Operations.GetOperations(doc), new JObject { ["limit"] = 100 });
                var scenario = CamNames.OperationPage(TopSolidCamHost.Operations.GetScenarioOperations(doc), new JObject { ["limit"] = 100 });
                File.WriteAllText("artifacts/cam-tools/native-operation-list.json", page.ToString());
                File.WriteAllText("artifacts/cam-tools/native-operation-scenario.json", scenario.ToString());
                foreach (var summary in summaries)
                {
                    var match = ((JArray)page["items"]).Single(r => (string)r["name"] == (string)summary["operationName"]);
                    Check((string)match["toolDisplayName"] == (string)summary["toolDisplayName"], "Simple operation list lost native tooling");
                }
                Check(TopSolidHost.Documents.IsDirty(doc) == dirty, "Operation presentation changed the document");
                Check(TopSolidHost.Documents.EditedDocument == active, "Tool inspection switched the edited document");
                foreach (var row in summaries) Console.WriteLine(row["operationName"] + " | " + row["toolDisplayName"] + " | " + row["toolFunction"]);
                Console.WriteLine("Read-only tool metadata probe: " + result.Count + " tools; document dirty state unchanged.");
                return 0;
            }
        }
    }
}
