using System;
using System.Collections.Generic;
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
        private static void CreationNaming()
        {
            var names = new HashSet<string>(new[] { "Cylinder", "Cylinder_1", "Cylinder_2", "Cylinder profile", "Slot_Sketch" }, StringComparer.OrdinalIgnoreCase);
            var snapshots = 0;
            CreationNames Resolve(string tool, JObject p) => CreationNames.Resolve(tool, p, () => { snapshots++; return names.ToArray(); });
            var auto = JObject.Parse("{documentId:'rev',diameter:100,height:350}");
            Check(Resolve("topsolid_create_cylinder", auto) == null && snapshots == 0, "Automatic names must not enumerate document elements");
            Check(CylinderPlan.Parse(auto, .35).SketchArguments(auto)["name"] == null, "Cylinder source sketch still gets a fixed default name");
            auto["name"] = "Cylinder";
            var allocated = Resolve("topsolid_create_cylinder", auto);
            Check((string)allocated.Arguments["name"] == "Cylinder_3" && (string)auto["name"] == "Cylinder", "Collision resolution mutated original arguments or reused a name");
            Check(CylinderPlan.Parse(allocated.Arguments, .35).SketchArguments(allocated.Arguments)["name"] == null, "Named shapes must still leave their source sketch automatically named");
            var batch = JObject.Parse("{documentId:'rev',sketches:[{name:'Slot_Sketch'},{name:'Slot_Sketch'},{name:'SLOT_SKETCH'},{placement:'xy'}]}");
            var before = snapshots; var plan = Resolve("topsolid_create_sketches2d", batch);
            Check(snapshots == before + 1 && (string)plan.Arguments["sketches"][0]["name"] == "Slot_Sketch_1" && (string)plan.Arguments["sketches"][1]["name"] == "Slot_Sketch_2" && (string)plan.Arguments["sketches"][2]["name"] == "SLOT_SKETCH_3", "Batch reservation must use one snapshot and include earlier entries");
            Check(plan.Arguments["sketches"][3]["name"] == null && (string)plan.Receipt["assignments"][1]["argument"] == "sketches[1].name", "Automatic sketch naming or receipt input mapping changed");
            Check(CreationNames.Allocate("Cylinder_2", names) == "Cylinder_3", "Existing numeric suffix must increment instead of nesting _2_1");
            var longName = new string('x', 128); names.Add(longName);
            var truncated = CreationNames.Allocate(longName, names);
            Check(truncated.Length == 128 && truncated.EndsWith("_1", StringComparison.Ordinal), "Suffix exceeded the tool name length");
            foreach (var bad in new[] { " ", "$Reserved", "a\nb" }) Throws<ArgumentException>(() => CreationNames.Allocate(bad, names));
            Check(Resolve("topsolid_create_project", JObject.Parse("{name:'Cylinder'}")) == null && Resolve("topsolid_rename_element", JObject.Parse("{name:'Cylinder'}")) == null, "PDM creation or explicit renames were silently changed");

            // Preparation returns exact names; neither declining nor a collision
            // introduced after confirmation can execute a different name silently.
            var executed = 0; var actualName = "";
            var tool = new ToolDefinition("topsolid_create_cylinder", "naming fixture", new JObject { ["documentId"] = Schema.Text("ID"), ["name"] = Schema.Text("Name", 100) },
                p => { executed++; actualName = (string)p["name"]; return new JObject { ["name"] = actualName }; }, "Design3D", new[] { "documentId", "name" }, false);
            var registry = new ToolRegistry(new[] { tool }, p => new JObject { ["documentId"] = p["documentId"] }, Resolve);
            var args = JObject.Parse("{documentId:'rev',name:'Cylinder'}");
            var count = snapshots; Throws<RpcException>(() => registry.Call(tool.Name, args));
            Check(executed == 0 && snapshots == count, "Unconfirmed name resolution touched native state");
            var proposal = registry.Prepare(tool.Name, args);
            var expected = (string)Resolve(tool.Name, args).Arguments["name"];
            var receipt = registry.Call(tool.Name, args, (string)proposal["confirmationToken"]);
            Check(!(bool)receipt["isError"] && actualName == expected && executed == 1 && receipt.ToString().Contains("assignedName"), "Execution or receipt lost the confirmed name");
            proposal = registry.Prepare(tool.Name, args); names.Add(expected);
            receipt = registry.Call(tool.Name, args, (string)proposal["confirmationToken"]);
            Check((bool)receipt["isError"] && executed == 1, "Name collision after preview must reject execution");

            var id = new ElementId(new DocumentId("rev"), 12); var stored = "auto";
            var api = new ContractProxy<IElements>((call, values) => {
                if (call.MethodName == "SetName") { stored = (string)values[1]; return null; }
                if (call.MethodName == "GetName") return stored;
                throw new InvalidOperationException("Unexpected API call");
            });
            CreationNames.SetAndVerify(api.Interface, id, null); Check(api.Calls.Count == 0, "Omitted names must never clear native names");
            CreationNames.SetAndVerify(api.Interface, id, "Cylinder_4"); Check(stored == "Cylinder_4", "Resolved name was not set");
            api.Handle = (call, values) => call.MethodName == "GetName" ? "wrong" : null;
            Throws<InvalidOperationException>(() => CreationNames.SetAndVerify(api.Interface, id, "Cylinder_5"));

            var profile = new ElementItemId(id, new ItemLabel(112, 1, "p12(1)", null)); var closed = true;
            var sketches = new ContractProxy<ISketches2D>((call, values) => call.MethodName == "IsSketch" ? (object)true : call.MethodName == "GetProfiles" ? new List<ElementItemId> { profile } : call.MethodName == "IsProfileClosed" ? closed : throw new InvalidOperationException("Unexpected sketch API"));
            ExplicitSectionTools.ValidateProfiles(sketches.Interface, id, new List<ElementItemId> { profile });
            closed = false; Throws<ArgumentException>(() => ExplicitSectionTools.ValidateProfiles(sketches.Interface, id, new List<ElementItemId> { profile }));
            Throws<ArgumentException>(() => ExplicitSectionTools.ValidateProfiles(sketches.Interface, id, new List<ElementItemId> { profile, profile }));
            using (var gateway = new AutomationGateway()) {
                var sectionArgs = new JObject { ["documentId"] = "rev", ["sketch"] = AutomationValues.Json(id), ["profiles"] = new JArray(AutomationValues.Json(profile)) };
                Check(Throws<RpcException>(() => new ToolRegistry(gateway).Call(ExplicitSectionTools.Name, sectionArgs)).Code == -32010, "Explicit section bypassed confirmation");
            }
        }
    }
}
