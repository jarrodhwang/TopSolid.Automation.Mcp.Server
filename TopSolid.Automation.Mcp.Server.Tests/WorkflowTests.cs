using System;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void WorkflowExpansion()
        {
            var references = JObject.Parse("{\"documentId\":\"old\",\"sourceDocumentId\":\"external\",\"profiles\":[{\"element\":{\"documentId\":\"old\",\"id\":42},\"label\":{\"type\":1,\"id\":7}}]}");
            var rebased = MutationReferences.Rebase(references, "new");
            Check((string)rebased["documentId"] == "new" && (string)rebased["profiles"][0]["element"]["documentId"] == "new", "Topology handles retained stale document revision");
            Check((string)rebased["sourceDocumentId"] == "external" && (string)references["documentId"] == "old", "Rebase changed external definition or approved arguments");
            references["profiles"][0]["element"]["documentId"] = "other";
            Throws<ArgumentException>(() => MutationReferences.Validate(references));
            var contour = JObject.Parse("{\"documentId\":\"d\",\"contour\":{\"start\":{\"x\":0,\"y\":0},\"closed\":true,\"segments\":[{\"kind\":\"line\",\"end\":{\"x\":20,\"y\":0}},{\"kind\":\"line\",\"end\":{\"x\":20,\"y\":10}},{\"kind\":\"line\",\"end\":{\"x\":0,\"y\":10}},{\"kind\":\"line\",\"end\":{\"x\":0,\"y\":0}}]}}");
            Check(Math.Abs(ContourGeometry.Validate(contour) - .06) < 1e-12, "Millimetre contour length wrong");
            contour["contour"]["closed"] = false;
            Throws<ArgumentException>(() => ContourGeometry.Validate(contour));
            contour["contour"]["closed"] = true;
            contour["contour"]["segments"][0]["center"] = new JObject { ["x"] = 10, ["y"] = 0 };
            Throws<ArgumentException>(() => ContourGeometry.Validate(contour));
            var arc = JObject.Parse("{\"documentId\":\"d\",\"contour\":{\"start\":{\"x\":10,\"y\":0},\"closed\":false,\"segments\":[{\"kind\":\"arc\",\"end\":{\"x\":0,\"y\":10},\"center\":{\"x\":0,\"y\":0},\"clockwise\":false}]}}");
            Check(Math.Abs(ContourGeometry.Validate(arc) - .01*Math.PI/2) < 1e-12, "CCW arc took wrong sweep");
            arc["contour"]["segments"][0]["clockwise"] = true;
            Check(Math.Abs(ContourGeometry.Validate(arc) - .01*3*Math.PI/2) < 1e-12, "CW arc took wrong sweep");
            arc["contour"]["segments"][0]["end"]["y"] = 11;
            Throws<ArgumentException>(() => ContourGeometry.Validate(arc));
            var vector = JObject.Parse("{\"x\":0,\"y\":0,\"z\":12}");
            Check(SpatialInput.Direction(vector).Z == 1, "Direction was not normalized");
            vector["z"] = 0; Throws<ArgumentException>(() => SpatialInput.Direction(vector));
            var translation = SpatialInput.Translation(JObject.Parse("{\"translation\":{\"x\":10,\"y\":20,\"z\":30},\"units\":\"mm\"}"));
            Check(translation.Tx == .01 && translation.Ty == .02 && translation.Tz == .03 && translation.R00 == 1 && translation.Si == 1, "Translation has wrong SI scale or transform matrix");
            var frame = JObject.Parse("{\"origin\":{\"x\":0,\"y\":0,\"z\":10},\"xAxis\":{\"x\":1,\"y\":0,\"z\":0},\"yAxis\":{\"x\":0,\"y\":1,\"z\":0}}");
            Check(SpatialInput.Frame(frame, .001).ZDirection.Z == 1, "Frame handedness incorrect");
            frame["yAxis"]["x"] = 1; Throws<ArgumentException>(() => SpatialInput.Frame(frame, .001));
            var scalar = JObject.Parse("{\"documentId\":\"d\",\"valueType\":\"Real\",\"realValueSI\":0.025,\"unitType\":\"Length\"}");
            ScalarInput.Validate(scalar); checks++;
            scalar["integerValue"] = 3; Throws<ArgumentException>(() => ScalarInput.Validate(scalar)); scalar.Remove("integerValue"); scalar.Remove("unitType");
            Throws<ArgumentException>(() => ScalarInput.Validate(scalar));
            PdmActionTools.ValidateDocument(new JObject { ["extension"] = ".TopPrt" }); checks++;
            Throws<ArgumentException>(() => PdmActionTools.ValidateDocument(new JObject()));
            Throws<ArgumentException>(() => PdmActionTools.ValidateDocument(new JObject { ["extension"] = ".TopPrt", ["templateId"] = "t" }));
            Throws<ArgumentException>(() => PdmActionTools.ValidateDocument(new JObject { ["extension"] = "../escape" }));
            var previews = 0;
            var fake = new ToolDefinition("create", "Create PDM object", new JObject { ["name"] = Schema.Text("name") },
                _ => throw new PartialChangeException("created, but setup failed", new JObject { ["pdmObjectId"] = "preserved-id", ["created"] = true }, new Exception("setup")),
                "Pdm", new[] { "name" }, false, preview: p => { previews++; return new JObject { ["name"] = p["name"] }; }, effect: "Creates a persistent PDM object outside the geometry transaction.");
            var registry = new ToolRegistry(new[] { fake }, _ => throw new Exception("Wrong document-only preview"));
            var input = new JObject { ["name"] = "Test" };
            Throws<RpcException>(() => registry.Call("create", input));
            Check(previews == 0, "Unapproved PDM action reached preview");
            var proposal = registry.Prepare("create", input);
            Check(previews == 1 && (string)proposal["inputLengthUnits"] == "Not applicable" && ((string)proposal["effect"]).Contains("persistent"), "PDM confirmation used geometry units/effect");
            var failure = registry.Call("create", input, (string)proposal["confirmationToken"]);
            Check((bool)failure["isError"] && failure.ToString().Contains("preserved-id"), "Non-undoable partial creation receipt was lost");
            var targetChanged = false; var executed = 0;
            var guarded = new ToolDefinition("guarded", "Target changes invalidate approval", new JObject(), _ => { executed++; return new JObject(); },
                readOnly: false, preview: _ => new JObject { ["affectedDocuments"] = new JArray(targetChanged ? "different-related-document" : "original-document") });
            var guardRegistry = new ToolRegistry(new[] { guarded }, _ => new JObject());
            proposal = guardRegistry.Prepare("guarded", new JObject()); targetChanged = true;
            var blocked = guardRegistry.Call("guarded", new JObject(), (string)proposal["confirmationToken"]);
            Check((bool)blocked["isError"] && executed == 0 && blocked.ToString().Contains("No action was executed"), "Changed synchronization scope executed using stale approval");
            Throws<RpcException>(() => guardRegistry.Call("guarded", new JObject(), (string)proposal["confirmationToken"]));
            var output = new StringWriter();
            new StdioMcpServer(new StringReader("\uFEFF{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-03-26\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}}\n"), output, guardRegistry).Run();
            Check(JObject.Parse(output.ToString())["result"] != null, "Leading .NET Framework UTF-8 BOM broke MCP initialization");
        }
    }
}
