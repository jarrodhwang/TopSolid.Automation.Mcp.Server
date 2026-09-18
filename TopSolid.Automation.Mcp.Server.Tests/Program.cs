using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static int checks;
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 1 && args[0] == "--probe-sketch-reads") return SketchReadProbe();
                Confirmation(); Transactions(); Geometry(); SchemaAndCatalog(); WorkflowExpansion(); PagedNames(); BatchTools(); PdmDateSorting(); ObjectIdentityRules(); SketchPlans(); DocumentCreation(); Persistence(); SketchPrimitives(); SketchReliability(); ParameterEntities(); ModelingWorkflows(); CreationNaming(); CamParameters();
                Console.WriteLine("PASS: " + checks + " server checks. No TopSolid connection or CAD mutation was performed."); return 0;
            }
            catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        private static T Throws<T>(Action action) where T : Exception
        { try { action(); } catch (T e) { checks++; return e; } throw new Exception("Expected " + typeof(T).Name); }
        private static void PagedNames()
        {
            var values = Enumerable.Range(0, 101).ToArray();
            var projected = 0;
            Func<int, JToken> name = id => { projected++; return new JObject { ["pdmObjectId"] = id, ["name"] = "Shared name" }; };
            var first = AutomationValues.Page(values, new JObject(), name, 100);
            Check(((JArray)first["items"]).Count == 100 && (bool)first["hasMore"] && projected == 100, "Default name page must resolve only 100 requested names");
            var last = AutomationValues.Page(values, new JObject { ["offset"] = 100 }, name, 100);
            Check(((JArray)last["items"]).Count == 1 && !(bool)last["hasMore"] && projected == 101, "Name pagination lost or repeated rows");
            Check((int)last["items"][0]["pdmObjectId"] == 100, "Duplicate friendly names must preserve separate IDs");
            Check(((JArray)AutomationValues.Page(values, new JObject { ["limit"] = 3 }, name, 100)["items"]).Count == 3, "Explicit page size must override the default");
            Check(((JArray)AutomationValues.Page(values, new JObject())["items"]).Count == 25, "Other tool page defaults changed");
        }
        private static void Confirmation()
        {
            var executed = 0; var previews = 0;
            var tool = new ToolDefinition("write", "Fake additive test operation", new JObject { ["documentId"] = Schema.Text("ID"), ["width"] = Schema.Number("width", 1, 100) },
                p => { executed++; return new JObject { ["done"] = true }; }, "Sketch2D", new[] { "documentId", "width" }, false);
            var registry = new ToolRegistry(new[] { tool }, p => { previews++; return new JObject { ["name"] = "Test part", ["documentId"] = p["documentId"] }; });
            var args = new JObject { ["documentId"] = "fixed-revision", ["width"] = 20 };
            Check(Throws<RpcException>(() => registry.Call("write", args)).Code == -32010, "Missing approval not rejected");
            Check(executed == 0 && previews == 0, "Unapproved tool touched the adapter");
            var proposal = registry.Prepare("write", args);
            Check(previews == 1 && executed == 0, "Preview must not execute");
            var token = (string)proposal["confirmationToken"];
            var modified = (JObject)args.DeepClone(); modified["width"] = 21;
            Throws<RpcException>(() => registry.Call("write", modified, token));
            Throws<RpcException>(() => registry.Call("write", args, token));
            Check(executed == 0, "Modified arguments or consumed ticket executed");
            proposal = registry.Prepare("write", args); token = (string)proposal["confirmationToken"];
            Check(!(bool)registry.Call("write", args, token)["isError"] && executed == 1, "Approved change did not execute once");
            Throws<RpcException>(() => registry.Call("write", args, token));
            Check(executed == 1, "Confirmation replay executed twice");
            modified["width"] = -1;
            Throws<RpcException>(() => registry.Prepare("write", modified));
            Check(previews == 3, "Invalid geometry reached preview");
            var now = DateTime.UtcNow; var store = new ConfirmationStore(() => now);
            proposal = store.Prepare(tool, args, new JObject()); now = now.AddMinutes(3);
            Throws<RpcException>(() => store.Consume((string)proposal["confirmationToken"], "write", args));
            proposal = store.Prepare(tool, args, new JObject());
            Throws<RpcException>(() => store.Consume((string)proposal["confirmationToken"], "different-tool", args));
            proposal = store.Prepare(tool, args, new JObject()); args["width"] = 90;
            Throws<RpcException>(() => store.Consume((string)proposal["confirmationToken"], "write", args));
            Check(executed == 1, "Expired, wrong-name or changed arguments executed");
        }
        private static void Transactions()
        {
            var ends = new List<string>(); var actions = 0;
            Action<bool, bool> end = (commit, update) => ends.Add(commit + "/" + update);
            Throws<InvalidOperationException>(() => ModificationScope.Run("busy", _ => false, end, () => ++actions));
            Check(actions == 0 && ends.Count == 0, "Busy document was touched");
            Check(ModificationScope.Run("success", _ => true, end, () => ++actions) == 1, "Modification result lost");
            Check(ends.SequenceEqual(new[] { "True/True" }), "Successful transaction not committed and updated once");
            ends.Clear();
            Throws<ArgumentException>(() => ModificationScope.Run<int>("failure", _ => true, end, () => throw new ArgumentException("geometry")));
            Check(ends.SequenceEqual(new[] { "False/False" }), "Failed transaction not rolled back");
            ends.Clear();
            Throws<InvalidOperationException>(() => ModificationScope.Run("commit failure", _ => true, (commit, update) =>
            { end(commit, update); if (commit) throw new InvalidOperationException("commit failed"); }, () => 1));
            Check(ends.SequenceEqual(new[] { "True/True", "False/False" }), "Commit failure did not attempt rollback");
            var error = Throws<AggregateException>(() => ModificationScope.Run<int>("rollback failure", _ => true,
                (_, __) => throw new InvalidOperationException("rollback failed"), () => throw new ArgumentException("geometry")));
            Check(error.InnerExceptions.Count == 2 && error.Message.Contains("could not be confirmed"), "Uncertain rollback was not retained");
        }
        private static void SchemaAndCatalog()
        {
            using (var gateway = new AutomationGateway())
            {
                var registry = new ToolRegistry(gateway); var list = registry.List();
                Check(list.Count == 187, "Domain registration is incomplete");
                Check(list.Select(t => (string)t["name"]).Distinct().Count() == list.Count, "Duplicate tool names");
                Check(list.Count(t => !(bool)t["annotations"]["readOnlyHint"]) == 59, "Unexpected write capabilities");
                foreach (var tool in list)
                {
                    Check((bool)tool["inputSchema"]["additionalProperties"] == false, "Open argument object");
                    var references = (JArray)tool["_meta"]["topsolid/api"];
                    foreach (var apiReference in references)
                    {
                        var localFile = (string)apiReference;
                        Check(!localFile.StartsWith("https://help.topsolid.com/", StringComparison.OrdinalIgnoreCase),
                            "Runtime tool metadata must not expose a website reference: " + (string)tool["name"] + " -> " + localFile);
                        Check(localFile.StartsWith("TopSolid.Automation/", StringComparison.Ordinal),
                            "Runtime API metadata must use the bundled local reference root");
                        if (localFile.IndexOf("/embedded/", StringComparison.Ordinal) < 0)
                            Check(File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, localFile.Replace('/', Path.DirectorySeparatorChar))),
                                "Runtime API metadata points to a missing local reference file");
                    }
                }
                var circle = (JObject)list.Single(t => (string)t["name"] == "topsolid_create_circle2d")["inputSchema"];
                var valid = JObject.Parse("{\"documentId\":\"revision\",\"placement\":\"2d\",\"radius\":10}");
                Schema.Validate(valid, circle);
                foreach (var bad in new JToken[] { new JValue(-1), new JValue(double.NaN), new JValue(double.PositiveInfinity), new JValue("ten"), JValue.CreateNull() })
                { valid["radius"] = bad; Throws<RpcException>(() => Schema.Validate(valid, circle)); }
                valid["radius"] = 10; valid["confirmed"] = true;
                Throws<RpcException>(() => Schema.Validate(valid, circle));
                var topology = new ElementItemId(new ElementId(new DocumentId("revision"), 42), new ItemLabel(1, 2, "", ""));
                Schema.Validate(AutomationValues.Json(topology), Schema.Item()); checks++;
                var parameterSchema = (JObject)list.Single(t => (string)t["name"] == "topsolid_get_cam_parameter_value")["inputSchema"];
                foreach (var camElement in new[] { new TopSolid.Cam.NC.Kernel.Automating.ElementExId(topology.ElementId), new TopSolid.Cam.NC.Kernel.Automating.ElementExId(Guid.NewGuid()) })
                {
                    var parameter = new TopSolid.Cam.NC.Kernel.Automating.ParameterId(camElement, "NCOperation.TestParameter");
                    Schema.Validate(AutomationValues.Json(parameter), parameterSchema); checks++;
                }
                var values = AutomationValues.Page(Enumerable.Range(0, 250), new JObject { ["offset"] = 240, ["limit"] = 25 });
                Check(((JArray)values["items"]).Count == 10 && !(bool)values["hasMore"] && (int)values["total"] == 250, "Pagination incorrect");
                var search = registry.Call("topsolid_search_api_reference", new JObject { ["query"] = "ISketches2D.CreateSketchIn2D", ["module"] = "kernel" });
                Check(!(bool)search["isError"] && search.ToString().Contains("CreateSketchIn2D"), "Reference search failed offline");
                Check(search.ToString().IndexOf("https://help.topsolid.com/", StringComparison.OrdinalIgnoreCase) < 0 &&
                      search.ToString().IndexOf("localFile", StringComparison.Ordinal) >= 0,
                    "Reference search exposed a website URL instead of a local file");
                var reference = registry.Call("topsolid_get_api_reference", new JObject { ["symbol"] = "TopSolid.Kernel.Automating.ISketches2D.CreateSketchIn2D" });
                Check(!(bool)reference["isError"] && reference.ToString().Contains("EnsureIsDirty"), "Cached method contract missing");
                Check(reference.ToString().IndexOf("https://help.topsolid.com/", StringComparison.OrdinalIgnoreCase) < 0 &&
                      reference.ToString().IndexOf("localFile", StringComparison.Ordinal) >= 0,
                    "Reference lookup exposed a website URL instead of a local file");
                var capabilities = registry.Call("topsolid_get_capabilities", new JObject());
                Check(!(bool)capabilities["isError"] && capabilities.ToString().Contains("Cam/Operation"), "Capability categories missing");
                Check(AutomationGateway.FormatVersion(720400107) == "7.20.400.107", "Incorrect TopSolid version decoding");
                Check(typeof(TopSolidHost).Assembly.GetName().Version.ToString() == "7.20.400.107", "Stale SDK binary in test output");
            }
        }
        private static void Geometry()
        {
            Check(ModelingGeometry.Scale(new JObject()) == 0.001, "Default units must be millimetres");
            Check(ModelingGeometry.Scale(new JObject { ["units"] = "cm" }) * 10 == 0.1, "Centimetre conversion failed");
            Check(ModelingGeometry.Scale(new JObject { ["units"] = "m" }) == 1, "Metre conversion failed");
            foreach (var placement in new[] { "xy", "xz", "yz" })
            {
                var point = ModelingGeometry.Plane(placement, new Point3D(1, 2, 3)).ToAbsolute(new Point2D(0.02, 0.01));
                var expected = placement == "xy" ? new Point3D(1.02, 2.01, 3) : placement == "xz" ? new Point3D(1.02, 2, 3.01) : new Point3D(1, 2.02, 3.01);
                Check(Math.Abs(point.X - expected.X) < 1e-10 && Math.Abs(point.Y - expected.Y) < 1e-10 && Math.Abs(point.Z - expected.Z) < 1e-10, "Wrong translated sketch plane " + placement);
            }
            Throws<ArgumentException>(() => ModelingGeometry.Validate("rectangle2d", new JObject { ["placement"] = "2d", ["z"] = 1 }));
            var points = JArray.Parse("[{\"x\":0,\"y\":0,\"z\":0},{\"x\":10,\"y\":0,\"z\":0}]");
            var args = new JObject { ["points"] = points, ["closed"] = true };
            Throws<ArgumentException>(() => ModelingGeometry.Validate("polyline3d", args));
            args["closed"] = false; ModelingGeometry.Validate("polyline3d", args); checks++;
            points[1] = points[0].DeepClone();
            Throws<ArgumentException>(() => ModelingGeometry.Validate("polyline3d", args));
        }
    }
}
