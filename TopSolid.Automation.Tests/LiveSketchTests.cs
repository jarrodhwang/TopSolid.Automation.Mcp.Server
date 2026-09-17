using System.Diagnostics;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class LiveSketchTests
{
    // Reads and prepared previews only. There is deliberately no confirmed-call
    // path here: this test may inspect the user's open document but never edits it.
    public static async Task Preview(string executable)
    {
        await using var client = new StdioMcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        await client.ConnectAsync(executable, timeout.Token);
        var record = new JObject { ["nativeWrites"] = 0, ["server"] = executable };
        async Task<JObject> Read(string name, JObject args)
        {
            var watch = Stopwatch.StartNew(); var result = await client.CallToolAsync(name, args, timeout.Token);
            Check.True(!result.IsError, name + ": " + result.Content);
            record[name + "Seconds"] = watch.Elapsed.TotalSeconds;
            return result.StructuredContent ?? JObject.Parse((string)result.Content[0]["text"]!);
        }
        var info = await Read("topsolid_get_document_info", new JObject());
        var doc = info["document"] as JObject ?? throw new InvalidOperationException("Live sketch preview needs an open document.");
        Check.True((string?)doc["typeFullName"] == "TopSolid.Cad.Design.DB.Documents.PartDocument", "This read-only fixture expects an open part; no document will be opened automatically.");
        var id = (string)doc["documentId"]!; record["documentBefore"] = doc.DeepClone();
        var context = await Read("topsolid_get_sketch2d_context", new JObject { ["documentId"] = id, ["documentSpace"] = "3d", ["limit"] = 100 });
        record["context"] = context;
        Check.Equal(0, (int)context["failed"]!, "Some sketch frames could not be read");
        var reference = ((JArray)context["items"]!).FirstOrDefault() as JObject ?? throw new InvalidOperationException("Live reference preview requires an existing sketch; no sketch will be created automatically.");
        var sketch = (JObject)reference["sketch"]!;
        var named = await Read("topsolid_get_sketch2d_context", new JObject { ["documentId"] = id, ["documentSpace"] = "3d", ["names"] = new JArray(reference["friendlyName"]!.DeepClone()) });
        Check.True(((JArray)named["items"]!).Any(row => JToken.DeepEquals(row["sketch"], sketch)), "Friendly-name reference lookup lost the exact sketch");
        var geometry = await Read("topsolid_read_sketch2d_geometry", new JObject { ["sketch"] = sketch.DeepClone(), ["kind"] = "segments", ["limit"] = 5 });
        record["geometry"] = geometry; Check.Equal(0, (int)geometry["failed"]!, "Native segment sampling failed");
        var circle = ((JArray)geometry["items"]!).FirstOrDefault(row => (string?)row["curveType"] == "Circle")
            ?? throw new InvalidOperationException("This reference fixture requires an existing circular segment.");
        var converted = await Read("topsolid_transform_sketch2d_points", new JObject { ["documentId"] = id, ["documentSpace"] = "3d", ["units"] = "mm",
            ["sourceSketch"] = sketch.DeepClone(), ["targetSketch"] = sketch.DeepClone(), ["points"] = JArray.Parse("[{x:20,y:30}]") });
        Check.True(Math.Abs((double)converted["points"]![0]!["targetLocal"]!["X"]! - .02) < 1e-10, "Native frame conversion or mm/metre scale failed");
        var plan = new JObject { ["documentId"] = id, ["documentSpace"] = "3d", ["units"] = "mm", ["sketches"] = new JArray(new JObject {
            ["name"] = "Preview only - never executed", ["placement"] = "reference", ["referenceSketch"] = sketch.DeepClone(),
            ["anchor"] = new JObject { ["item"] = circle["centerVertex"]!.DeepClone(), ["location"] = "vertex" },
            ["origin"] = JObject.Parse("{x:20,y:30,z:0}"), ["rotationDegrees"] = 90,
            ["profiles"] = JArray.Parse("[{kind:'circle',origin:{x:0,y:0},radius:10},{kind:'parabola',vertex:{x:0,y:0},focalLength:12.5,startParameter:-50,endParameter:50,rotationDegrees:0}]") }) };
        var timer = Stopwatch.StartNew(); var preview = await client.PrepareToolAsync("topsolid_create_sketches2d", plan, timeout.Token);
        record["previewSeconds"] = timer.Elapsed.TotalSeconds; preview.Remove("confirmationToken"); record["preview"] = preview;
        Check.True(preview["target"]?["sketches"]?[0]?["placement"]?["reference"] != null, "Reference frame was omitted from the confirmation target");
        Check.True(preview["target"]?["sketches"]?[0]?["sectionMode"]?.ToString() == "none", "Preview unexpectedly requested sections");
        Check.True(preview["target"]?["sketches"]?[0]?["placement"]?["associativeInputs"]?["xAxis"] != null, "Preview failed to resolve real associative sketch axes");
        if (circle != null)
        {
            plan["sketches"]![0]!["anchor"] = new JObject { ["item"] = circle["item"]!.DeepClone(), ["location"] = "center" };
            var anchorPreview = await client.PrepareToolAsync("topsolid_create_sketches2d", plan, timeout.Token);
            anchorPreview.Remove("confirmationToken"); record["anchorPreview"] = anchorPreview;
            Check.True(anchorPreview["target"]?["sketches"]?[0]?["placement"]?["reference"]?["anchorPointMetres"] != null, "Native circle center was not resolved for placement");
        }
        var after = await Read("topsolid_get_document_info", new JObject { ["documentId"] = id });
        Check.True(JToken.DeepEquals(doc, after["document"]), "Read-only preview changed document identity or dirty state");
        var afterContext = await Read("topsolid_get_sketch2d_context", new JObject { ["documentId"] = id, ["documentSpace"] = "3d", ["limit"] = 100 });
        Check.True(JToken.DeepEquals(context, afterContext), "Read-only preview changed native sketch inventory");
        Directory.CreateDirectory("artifacts/sketch2d-0.5.3");
        File.WriteAllText("artifacts/sketch2d-0.5.3/live-read-preview.json", record.ToString());
        Console.WriteLine($"Native sketch context, geometry, coordinate conversion and reference preview passed; {context["total"]} sketches; preview {record["previewSeconds"]} s; zero writes.");
    }

    public static async Task ModelClarification(string executable, string model)
    {
        await using var native = new StdioMcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        await native.ConnectAsync(executable, timeout.Token);
        using var provider = new OllamaProvider("http://localhost:11434", model, requestTimeout: TimeSpan.FromMinutes(4));
        var guard = new ReadOnlyClient(native); var trace = new JArray();
        var session = new ChatSession(provider, guard) { ConfirmChangeAsync = (_, _) => Task.FromResult(false) };
        session.Trace += t => { trace.Add(new JObject { ["kind"] = t.Kind, ["text"] = t.Text }); if (t.Kind is "Timing" or "Model") Console.WriteLine(t.Text); };
        var watch = Stopwatch.StartNew();
        const string request = "Create 2 sketches in the currently open part: 1. circle 2. parabola.";
        var answer = await session.SendAsync(request, timeout.Token);
        Directory.CreateDirectory("artifacts/sketch2d-0.5.3");
        File.WriteAllText("artifacts/sketch2d-0.5.3/live-model-clarification.json", new JObject { ["model"] = model, ["request"] = request, ["answer"] = answer,
            ["seconds"] = watch.Elapsed.TotalSeconds, ["attemptedChanges"] = guard.AttemptedChanges, ["nativeWrites"] = 0, ["trace"] = trace }.ToString());
        Check.Equal(0, guard.AttemptedChanges, "Model invented a geometry proposal before obtaining the missing dimensions: " + answer);
        Check.True(answer.Contains("?"), "Model did not ask for missing sketch dimensions: " + answer);
        Console.WriteLine($"Live {model} clarification in {watch.Elapsed.TotalSeconds:F2} s, zero proposed/actual writes:\n{answer}");
    }
    private sealed class ReadOnlyClient(StdioMcpClient native) : IConfirmableMcpClient
    {
        public int AttemptedChanges;
        public bool IsConnected => native.IsConnected;
        public IReadOnlyList<McpToolDefinition> Tools => native.Tools;
        public Task<McpToolResult> CallToolAsync(string name, JObject args, CancellationToken ct)
        {
            if (Tools.Any(t => t.Name == name && t.RequiresConfirmation)) throw new InvalidOperationException("Native changes are disabled in this test.");
            return native.CallToolAsync(name, args, ct);
        }
        public Task<JObject> PrepareToolAsync(string name, JObject args, CancellationToken ct) { AttemptedChanges++; throw new InvalidOperationException("Unspecified sketch dimensions: ask the user first. This fixture cannot prepare/execute changes."); }
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject args, string confirmation, CancellationToken ct) => throw new InvalidOperationException("No native write execution path.");
    }
}
