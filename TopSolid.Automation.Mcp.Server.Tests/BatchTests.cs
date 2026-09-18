using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void BatchTools()
        {
            var rows = Enumerable.Range(0, 211).ToArray(); var observed = new List<int>(); var offset = 0;
            do
            {
                var page = BatchRead.Page(rows, new JObject { ["offset"] = offset }, id => new JObject { ["id"] = id }, id => new JObject { ["name"] = "shared", ["text"] = new string('x', 1800) });
                Check(page.ToString(Formatting.None).Length < 60000, "Batch exceeded protocol result limit");
                Check((int)page["returned"] < 100, "Large results must shorten a page");
                observed.AddRange(((JArray)page["items"]).Select(row => (int)row["id"]));
                if (!(bool)page["hasMore"]) break;
                Check((int)page["nextOffset"] > offset, "Continuation did not advance"); offset = (int)page["nextOffset"];
            } while (true);
            Check(observed.SequenceEqual(rows), "Byte-limited pagination skipped or duplicated rows");
            var calls = 0;
            var partial = BatchRead.Page(new[] { 1, 2, 3 }, new JObject(), id => new JObject { ["id"] = id }, id =>
            { calls++; if (id == 2) throw new ArgumentException("stale handle"); return new JObject { ["name"] = "same" }; });
            Check(calls == 3 && (int)partial["failed"] == 1 && (int)partial["returned"] == 3, "One bad row discarded successful reads");
            Check((int)partial["items"][1]["id"] == 2 && (bool)partial["items"][1]["isError"], "Failed row lost identity");
            var huge = BatchRead.Page(new[] { 1, 2 }, new JObject(), id => new JObject { ["id"] = id }, id => new JObject { ["text"] = new string('x', id == 1 ? 65000 : 3) });
            Check((int)huge["failed"] == 1 && (int)huge["returned"] == 2 && huge.ToString().Contains("separately"), "Oversized row must be an explicit bounded error");
            Throws<TimeoutException>(() => BatchRead.Page(new[] { 1 }, new JObject(), id => new JObject(), id => throw new TimeoutException()));
            Throws<CommunicationException>(() => BatchRead.Page(new[] { 1 }, new JObject(), id => new JObject(), id => throw new CommunicationException()));
            var reordered = JObject.Parse("{\"id\":1,\"documentId\":\"rev\"}");
            var element = JObject.Parse("{\"documentId\":\"rev\",\"id\":1}");
            Throws<ArgumentException>(() => BatchInput.Unique(new[] { element, reordered }, "element"));

            using (var gateway = new AutomationGateway())
            {
                var registry = new ToolRegistry(gateway); var catalog = registry.List();
                JObject Shape(string tool) => (JObject)catalog.Single(t => (string)t["name"] == tool)["inputSchema"];
                var valid = new JObject { ["documentId"] = "rev", ["elements"] = new JArray(element) };
                Check(Throws<RpcException>(() => registry.Call("topsolid_delete_elements", valid)).Code == -32010, "Deletion executed without a client-issued confirmation");
                var crossDoc = (JObject)valid.DeepClone(); crossDoc["elements"][0]["documentId"] = "different";
                Check(Throws<RpcException>(() => registry.Call("topsolid_delete_elements", crossDoc)).Code == -32602, "Cross-document deletion reached native code");
                var large = (JObject)valid.DeepClone(); large["elements"] = new JArray(Enumerable.Range(1, 33).Select(i => new JObject { ["documentId"] = "rev", ["id"] = i }));
                Throws<RpcException>(() => Schema.Validate(large, Shape("topsolid_delete_elements")));
                var update = new JObject { ["documentId"] = "rev", ["changes"] = new JArray(new JObject { ["element"] = element.DeepClone() }) };
                Check(Throws<RpcException>(() => registry.Call("topsolid_update_elements", update)).Code == -32602, "Empty element update accepted");
                var parameter = new JObject { ["name"] = "Length", ["valueType"] = "Real", ["unitType"] = "Length", ["realValueSI"] = 0.025 };
                var parameters = new JObject { ["documentId"] = "rev", ["parameters"] = new JArray(parameter) };
                Schema.Validate(parameters, Shape("topsolid_create_parameters")); ParameterBatchTools.Validate(parameters, true);
                Check(Throws<RpcException>(() => registry.Call("topsolid_create_parameters", parameters)).Code == -32010, "Parameter creation bypassed confirmation");
                parameter["unitType"] = "999"; Throws<ArgumentException>(() => ParameterBatchTools.Validate(parameters, true)); parameter["unitType"] = "Length";
                parameter["textValue"] = "wrong"; Throws<ArgumentException>(() => ParameterBatchTools.Validate(parameters, true)); parameter.Remove("textValue");
                ((JArray)parameters["parameters"]).Add(parameter.DeepClone()); ParameterBatchTools.Validate(parameters, true);
                var reserved = CreationNames.Resolve("topsolid_create_parameters", parameters, () => new string[0]);
                Check((string)reserved.Arguments["parameters"][1]["name"] == "Length_1", "Duplicate parameter creation names must be reserved before confirmation");
                var sketch = JObject.Parse("{\"documentId\":\"rev\",\"placement\":\"xy\",\"profiles\":[{\"kind\":\"polyline\",\"closed\":true,\"points\":[{\"x\":0,\"y\":0},{\"x\":10,\"y\":0},{\"x\":0,\"y\":10}]}]}");
                Schema.Validate(sketch, Shape("topsolid_create_sketch_profiles")); SketchBatchActionTools.ValidateProfiles(sketch);
                Check(Throws<RpcException>(() => registry.Call("topsolid_create_sketch_profiles", sketch)).Code == -32010, "Sketch batch bypassed confirmation");
                sketch["profiles"][0]["closed"] = false; SketchBatchActionTools.ValidateProfiles(sketch);
                sketch["sectionMode"] = "perProfile"; Throws<RpcException>(() => Schema.Validate(sketch, Shape("topsolid_create_sketch_profiles"))); sketch.Remove("sectionMode");
                foreach (var flag in new[] { true, false }) {
                    var circle = JObject.Parse("{documentId:'rev',placement:'xy',radius:10}"); circle["createSection"] = flag;
                    Throws<RpcException>(() => Schema.Validate(circle, Shape("topsolid_create_circle2d")));
                }
                Check((bool)registry.List().Single(t => (string)t["name"] == "topsolid_create_sketch_section")["_meta"]["topsolid/requiresConfirmation"], "Sections need a separate confirmed tool");
                ((JArray)sketch["profiles"][0]["points"]).Add(JObject.Parse("{\"x\":0,\"y\":10}")); Throws<ArgumentException>(() => SketchBatchActionTools.ValidateProfiles(sketch));
                var features = new JObject { ["documentId"] = "rev", ["features"] = new JArray(new JObject { ["sketch"] = element.DeepClone(), ["direction"] = new JObject { ["x"] = 0, ["y"] = 0, ["z"] = 1 }, ["length"] = 10 }) };
                Schema.Validate(features, Shape("topsolid_extrude_sections")); ShapeBatchActionTools.Validate(features, "extrude");
                Check(Throws<RpcException>(() => registry.Call("topsolid_extrude_sections", features)).Code == -32010, "Shape batch bypassed confirmation");
                features["features"][0]["direction"]["z"] = 0; Throws<ArgumentException>(() => ShapeBatchActionTools.Validate(features, "extrude"));
                var rebased = MutationReferences.Rebase(features, "new-revision");
                Check((string)rebased["features"][0]["sketch"]["documentId"] == "new-revision" && (string)features["documentId"] == "rev", "Nested batch handles were not rebased immutably");
            }
            var edits = new List<int>(); var committed = false;
            Throws<InvalidOperationException>(() => ModificationScope.Run("batch", _ => true, (commit, _) => { committed = commit; if (!commit) edits.Clear(); }, () =>
            { foreach (var item in new[] { 1, 2, 3 }) { if (item == 3) throw new InvalidOperationException("last item fails"); edits.Add(item); } return edits.Count; }));
            Check(edits.Count == 0 && !committed, "Partial batch was committed on failure");
        }
    }
}
