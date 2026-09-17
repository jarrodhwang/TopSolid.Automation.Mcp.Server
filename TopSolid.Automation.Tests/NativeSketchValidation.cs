using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Mcp;

namespace TopSolid.Automation.Tests;

internal static class NativeSketchValidation
{
    // Opt-in, isolated native fixture. Never run this from the default suite.
    // The caller must obtain explicit user approval for the plan before supplying its hash.
    public static async Task Run(string server, string approvedHash)
    {
        const string planPath = "docs/sketch2d-native-validation-plan.json";
        var bytes = File.ReadAllBytes(planPath); var hash = Convert.ToHexString(SHA256.HashData(bytes));
        Check.True(string.Equals(hash, approvedHash, StringComparison.OrdinalIgnoreCase), "Native validation plan hash differs from the approved plan");
        var plan = JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes));
        var name = (string)plan["newPart"]!; var project = (string)plan["project"]!;
        // Pin the fixture, rather than treating arbitrary plan-file prose as executable instructions.
        Check.Equal("Sketch2D validation 0.5.3 - 20260916", name, "Unexpected fixture name");
        Check.Equal("AI Made this project", project, "Unexpected fixture project");
        var receipts = new JArray(); string? docId = null;
        await using var client = new StdioMcpClient();
        await client.ConnectAsync(server, CancellationToken.None);
        async Task<JObject> Read(string tool, JObject args)
        {
            var r = await client.CallToolAsync(tool, args, CancellationToken.None);
            Check.True(!r.IsError, tool + ": " + r.Content);
            return r.StructuredContent ?? JObject.Parse((string)r.Content[0]["text"]!);
        }
        async Task<JObject> Write(string tool, JObject args)
        {
            if (docId != null) Check.Equal(docId, (string)args["documentId"]!, "Validation attempted to edit a different document");
            var preview = await client.PrepareToolAsync(tool, args, CancellationToken.None);
            if (preview["target"]?["affectedDocuments"] is JArray affected)
                Check.True(affected.All(d => (string?)d["documentId"] == docId), "Fixture unexpectedly synchronizes another document");
            var ticket = (string)preview["confirmationToken"]!; preview.Remove("confirmationToken");
            receipts.Add(new JObject { ["preview"] = preview }); Save();
            var r = await client.CallConfirmedToolAsync(tool, args, ticket, CancellationToken.None);
            var data = r.StructuredContent ?? JObject.Parse((string)r.Content[0]["text"]!);
            receipts.Add(new JObject { ["tool"] = tool, ["isError"] = r.IsError, ["result"] = data }); Save();
            Check.True(!r.IsError, tool + ": " + data);
            if (data["documentId"] != null) docId = (string)data["documentId"]!;
            return data;
        }
        void Save()
        {
            Directory.CreateDirectory("artifacts/sketch2d-0.5.3");
            File.WriteAllText("artifacts/sketch2d-0.5.3/native-approved-validation.json", new JObject {
                ["approvedPlanSha256"] = hash, ["fixtureDocumentId"] = docId, ["receipts"] = receipts }.ToString());
        }
        try
        {
            var context = await Read("topsolid_get_document_creation_context", new JObject { ["projectName"] = project });
            Check.True((bool?)context["complete"] == true && ((JArray)context["projectMatches"]!).Count == 1, "Fixture destination is ambiguous or incomplete");
            var owner = (string)context["projectMatches"]![0]!["pdmObjectId"]!;
            var existing = await Read("topsolid_find_pdm_documents", new JObject { ["name"] = name, ["projectId"] = owner });
            Check.True((int?)existing["total"] == 0, "Fixture part already exists or lookup is incomplete; do not overwrite or create a duplicate");
            await Write("topsolid_create_part_document", new JObject { ["ownerId"] = owner, ["name"] = name });
            await Write("topsolid_open_document", new JObject { ["documentId"] = docId });
            var batch = JObject.Parse("{documentSpace:'3d',units:'mm',sketches:[{name:'Reference sketch',placement:'xy',profiles:[{kind:'circle',origin:{x:10,y:20},radius:10},{kind:'polyline',closed:false,points:[{x:50,y:0},{x:60,y:10},{x:70,y:0}]}]},{name:'Parabola sketch',placement:'xy',profiles:[{kind:'parabola',vertex:{x:-30,y:-20},focalLength:12.5,startParameter:-20,endParameter:20,rotationDegrees:0}]}]}");
            batch["documentId"] = docId; var created = await Write("topsolid_create_sketches2d", batch);
            var reference = (JObject)created["sketches"]![0]!["sketch"]!;
            var geometry = await Read("topsolid_read_sketch2d_geometry", new JObject { ["sketch"] = reference.DeepClone(), ["kind"] = "segments", ["limit"] = 100 });
            var circle = ((JArray)geometry["items"]!).Single(r => (string?)r["curveType"] == "Circle");
            var linked = JObject.Parse("{documentSpace:'3d',units:'mm',sketches:[{name:'Linked sketch',placement:'reference',referenceMode:'associative',origin:{x:20,y:30,z:0},rotationDegrees:90,profiles:[{kind:'circle',origin:{x:0,y:0},radius:2.5}]}]}");
            linked["documentId"] = docId; linked["sketches"]![0]!["referenceSketch"] = reference.DeepClone();
            linked["sketches"]![0]!["anchor"] = new JObject { ["item"] = circle["centerVertex"]!.DeepClone(), ["location"] = "vertex" };
            await Write("topsolid_create_sketches2d", linked);
            async Task<JObject> Context() => await Read("topsolid_get_sketch2d_context", new JObject { ["documentId"] = docId, ["documentSpace"] = "3d" });
            var before = await Context();
            var currentReference = ((JArray)before["items"]!).Single(s => (string?)s["internalName"] == "Reference sketch")["sketch"]!;
            var beforeOrigin = ((JArray)before["items"]!).Single(s => (string?)s["internalName"] == "Linked sketch")["placement"]!["originMetres"]!;
            await Write("topsolid_translate_element", new JObject { ["documentId"] = docId, ["element"] = currentReference.DeepClone(), ["units"] = "mm", ["translation"] = JObject.Parse("{x:10,y:0,z:0}") });
            var after = await Context();
            var afterOrigin = ((JArray)after["items"]!).Single(s => (string?)s["internalName"] == "Linked sketch")["placement"]!["originMetres"]!;
            Check.True(Math.Abs((double)afterOrigin["X"]! - (double)beforeOrigin["X"]! - .01) < 1e-8 && Math.Abs((double)afterOrigin["Y"]! - (double)beforeOrigin["Y"]!) < 1e-8,
                "Linked sketch did not follow the reference by 10 mm");
            receipts.Add(new JObject { ["before"] = before, ["after"] = after, ["automaticFollowVerified"] = true }); Save();
            Console.WriteLine("PASS native curves and automatic reference follow; dedicated fixture remains open and unsaved: " + name);
        }
        finally { Save(); }
    }
}
