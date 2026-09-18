using System.Diagnostics;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class PersistenceTests
{
    public static async Task Run()
    {
        var payload = new JObject { ["partialChange"] = new JObject { ["documentId"] = "exact-revision", ["name"] = new string('x', 5000) }, ["complete"] = false };
        var fullReceipt = new McpToolResult { IsError = true, StructuredContent = payload, Content = new JArray(
            new JObject { ["type"] = "text", ["text"] = payload.ToString() }, new JObject { ["type"] = "text", ["text"] = "Keep this separate explanation." }) };
        var compact = JObject.Parse(ToolResultContext.Serialize(fullReceipt));
        Check.True((bool)compact["isError"]! && JToken.DeepEquals(compact["structuredContent"], payload), "Compaction lost failure state or authoritative receipt data");
        Check.Equal(1, ((JArray)compact["content"]!).Count, "Model still receives duplicate structured/text JSON");
        Check.Equal(2, fullReceipt.Content.Count, "Compaction modified diagnostics source");
        const string checkIn = "check in , the project (ai made this project)";
        foreach (var text in new[] { "save all docs", "than save all other docs", "Please save all open documents." })
            Check.Equal("save", PdmPersistenceRequest.Parse(text)?.Operation, "Explicit save all request lost its batch route");
        Check.Equal("ai made this project", PdmPersistenceRequest.Parse(checkIn)?.Project, "Log check-in target not recognized");
        Check.Equal("Demo", PdmPersistenceRequest.Parse("check-in project \"Demo\"")?.Project, "Quoted check-in target lost");
        foreach (var text in new[] { "Do not save all docs", "save all loaded documents", "save all docs except this one", "save all docs and close them", "explain check in project \"X\"", "check in project \"X\" or \"Y\"", "check in project \"X\" and delete it" })
            Check.True(PdmPersistenceRequest.Parse(text) == null, "Ambiguous/compound/excluded scope must remain in the model workflow: " + text);
        foreach (var request in new[] { "than save all other docs", checkIn })
        foreach (var approve in new[] { true, false })
        {
            var fake = new PersistenceFake(); using var provider = PdmFastPathTests.NoInference(); var shown = 0;
            var session = new ChatSession(provider, fake) { ConfirmChangeAsync = (proposal, _) => {
                shown++; Check.True(proposal["confirmationToken"] == null, "Confirmation secret exposed");
                return Task.FromResult(approve);
            } };
            var traces = new List<ChatTrace>(); session.Trace += traces.Add;
            var result = await session.SendAsync(request, CancellationToken.None);
            Check.Equal(1, shown, "Batch persistence must ask for exactly one confirmation");
            Check.Equal(approve ? 1 : 0, fake.Writes, "Persistence did not honor user confirmation");
            Check.Equal(0, provider.CompletionCount, "Explicit persistence must not require model inference or follow-up");
            Check.Equal(request == checkIn ? 1 : 0, fake.Reads, "Save all must not enumerate documents through the model");
            Check.True(!approve ? result.Contains("declined") : request == checkIn ? result.Contains("CheckedIn") && result.Contains("ExclusiveModification") : result.Contains("No check-in was performed"), "Answer does not reflect exact native persistence receipt");
            Check.True(session.GetConversationSnapshot().Single().Any(m => m.Role == "tool"), "Persistence receipt lost from conversation history");
            Check.True(traces.Any(t => t.Kind == "Timing" && t.Text.Contains("user confirmation")), "Tool timing must separate human wait from execution");
        }
        foreach (var scenario in new[] { "duplicate", "partialLookup", "partialWrite", "tamper" })
        {
            var fake = new PersistenceFake { Scenario = scenario }; using var provider = PdmFastPathTests.NoInference();
            var session = new ChatSession(provider, fake) { ConfirmChangeAsync = (_, _) => Task.FromResult(true) };
            if (scenario == "tamper") await Check.ThrowsAsync<InvalidOperationException>(() => session.SendAsync(checkIn, CancellationToken.None));
            else { var answer = await session.SendAsync(checkIn, CancellationToken.None); Check.True(!answer.StartsWith("TopSolid completed"), "Uncertain/ambiguous outcome claimed check-in"); }
            Check.Equal(scenario == "partialWrite" ? 1 : 0, fake.Writes, "An unsafe target was submitted or a failed write repeated");
        }
        using (var provider = PdmFastPathTests.NoInference())
        {
            var fake = new PersistenceFake(); await new ChatSession(provider, fake).SendAsync("save all docs", CancellationToken.None);
            Check.Equal(0, fake.Writes, "Headless direct persistence bypassed confirmation");
        }
    }

    public static async Task LivePreview(string server)
    {
        const string folder = "artifacts/persistence-sketch-0.5.5";
        await using var native = new StdioMcpClient(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        await native.ConnectAsync(server, timeout.Token);
        var guarded = new PreviewOnly(native); var evidence = new JArray();
        foreach (var request in new[] { "save all docs", "check in the project (AI Made This project)" })
        {
            using var provider = PdmFastPathTests.NoInference(); var timer = Stopwatch.StartNew(); var previews = new JArray();
            var session = new ChatSession(provider, guarded) { ConfirmChangeAsync = (preview, _) => { previews.Add(preview.DeepClone()); return Task.FromResult(false); } };
            var answer = await session.SendAsync(request, timeout.Token);
            Check.True(answer.Contains("declined") && previews.Count == 1, "Native persistence preview failed: " + answer);
            evidence.Add(new JObject { ["request"] = request, ["seconds"] = timer.Elapsed.TotalSeconds, ["answer"] = answer, ["modelCalls"] = provider.CompletionCount, ["nativeWrites"] = 0, ["previews"] = previews });
            Console.WriteLine($"{request}: {timer.Elapsed.TotalSeconds:F3} s; preview declined; no native writes.");
        }
        Directory.CreateDirectory(folder); File.WriteAllText(folder + "/live-persistence-previews.json", evidence.ToString());

        var before = PdmInventory.Data(await guarded.CallToolAsync("topsolid_get_document_info", new JObject(), timeout.Token));
        var doc = (JObject?)before?["document"] ?? throw new InvalidOperationException("An open part is required for the sketch preview.");
        Check.Equal("TopSolid.Cad.Design.DB.Documents.PartDocument", (string?)doc["typeFullName"], "Sketch preview needs an already-open part; it never opens a file");
        var args = new JObject { ["documentId"] = doc["documentId"]!.DeepClone(), ["documentSpace"] = "3d", ["units"] = "mm",
            ["sketches"] = JArray.Parse("[{name:'Preview only - star',placement:'xz',units:'mm',profiles:[{kind:'star',center:{x:0,y:0},outerRadius:50,innerRadius:20,pointCount:5,rotationDegrees:90,units:'mm'}]},{name:'Preview only - ellipse',placement:'xy',profiles:[{kind:'ellipse',center:{x:10,y:20},majorRadius:60,minorRadius:40,rotationDegrees:30,tolerance:0.01}]}]") };
        var sketchTimer = Stopwatch.StartNew(); var sketchPreview = await guarded.PrepareToolAsync("topsolid_create_sketches2d", args, timeout.Token); sketchPreview.Remove("confirmationToken");
        Check.Equal(false, (bool?)sketchPreview["target"]?["sketches"]?[0]?["createsSections"], "Star preview must not create a section");
        Check.True((double?)sketchPreview["target"]?["sketches"]?[1]?["approximations"]?[0]?["maximumDeviationBoundMetres"] <= .00001, "Ellipse preview omits/exceeds approximation bound");
        var after = PdmInventory.Data(await guarded.CallToolAsync("topsolid_get_document_info", new JObject { ["documentId"] = doc["documentId"]!.DeepClone() }, timeout.Token));
        Check.True(JToken.DeepEquals(doc, after?["document"]), "Sketch preview changed the document identity or dirty state");
        File.WriteAllText(folder + "/live-sketch-preview.json", new JObject { ["seconds"] = sketchTimer.Elapsed.TotalSeconds, ["nativeWrites"] = 0, ["preview"] = sketchPreview, ["unchangedDocument"] = doc }.ToString());
        Console.WriteLine($"Native star/ellipse preview: {sketchTimer.Elapsed.TotalSeconds:F3} s; no native writes.");
    }

    public static async Task LiveSketchModel(string server)
    {
        const string folder = "artifacts/persistence-sketch-0.5.5";
        var store = new SettingsStore(); var original = File.ReadAllBytes(store.FilePath); var settings = store.Load();
        Check.Equal("gemini", settings.CloudService, "This opt-in model test uses the log's configured Gemini account"); settings.Provider = AppSettings.OpenAiProvider;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        await using var native = new StdioMcpClient(); await native.ConnectAsync(server, timeout.Token);
        var guarded = new PreviewOnly(native); using var provider = ProviderFactory.Create(settings); var trace = new JArray(); var proposals = new JArray();
        var session = new ChatSession(provider, guarded) { ConfirmChangeAsync = (preview, _) => {
            proposals.Add(preview.DeepClone());
            Check.Equal("topsolid_create_sketches2d", (string?)preview["toolName"], "Model must batch both requested sketches");
            var sketches = (JArray)preview["arguments"]!["sketches"]!;
            Check.Equal(2, sketches.Count, "Expected two sketches in one proposal");
            Check.True(sketches.Any(s => (string?)s["placement"] == "xz" && (string?)s["profiles"]?[0]?["kind"] == "star"), "Star plane or server primitive was lost");
            Check.True(sketches.Any(s => (string?)s["placement"] == "xy" && (string?)s["profiles"]?[0]?["kind"] == "ellipse"), "Ellipse must use the bounded server planner");
            return Task.FromResult(false);
        } };
        session.Trace += t => { trace.Add(new JObject { ["kind"] = t.Kind, ["text"] = t.Text }); if (t.Kind is "Timing" or "Model") Console.WriteLine(t.Text); };
        const string request = "In the currently open part, draw two new sample sketches together: a five-tip star on XZ centered at local (0,0) with outer radius 50 mm, inner radius 20 mm, first tip at 90 degrees; and an ellipse on XY centered at local (10,20), semimajor radius 60 mm, semiminor radius 40 mm, rotation 30 degrees, cubic approximation tolerance 0.01 mm. Both sketch frame origins and frame rotations are zero. No sections and no save. Use server-computed primitives.";
        var timer = Stopwatch.StartNew(); string? answer = null;
        try
        {
            answer = await session.SendAsync(request, timeout.Token);
            Check.Equal(1, proposals.Count, "Expected exactly one preview, declined by the fixture");
            Check.True(answer.Contains("declined"), "The fixture must stop without another model follow-up");
            Check.True(!trace.Any(t => (string?)t["kind"] == "Tool error" && !((string?)t["text"] ?? "").Contains("User declined this change")), "Model made malformed tool calls before the preview");
        }
        finally
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(folder + "/live-gemini-sketch-preview.json", new JObject { ["model"] = settings.CloudModel, ["seconds"] = timer.Elapsed.TotalSeconds, ["request"] = request, ["answer"] = answer, ["nativeWrites"] = 0, ["proposals"] = proposals, ["trace"] = trace }.ToString());
            Check.True(original.SequenceEqual(File.ReadAllBytes(store.FilePath)), "Live model test modified saved settings");
        }
    }

    private sealed class PersistenceFake : IConfirmableMcpClient
    {
        public string Scenario = ""; public int Reads, Writes;
        public bool IsConnected => true;
        public IReadOnlyList<McpToolDefinition> Tools { get; } = new[] { "topsolid_save_documents", "topsolid_check_in_pdm_objects", "topsolid_get_document_creation_context" }.Select(name => new McpToolDefinition {
            Name = name, Annotations = new JObject { ["readOnlyHint"] = name.Contains("context") }, Metadata = new JObject { ["topsolid/requiresConfirmation"] = !name.Contains("context") }
        }).ToArray();
        public Task<McpToolResult> CallToolAsync(string name, JObject args, CancellationToken token)
        {
            Check.Equal("topsolid_get_document_creation_context", name, "Unexpected read or unconfirmed persistence call"); Reads++;
            var rows = JArray.Parse("[{pdmObjectId:'project-live',name:'AI Made This Project'}]");
            if (Scenario == "duplicate") rows.Add(JObject.Parse("{pdmObjectId:'other-project',name:'ai made this project'}"));
            return Task.FromResult(Result(new JObject { ["complete"] = Scenario != "partialLookup", ["projectMatches"] = rows }));
        }
        public Task<JObject> PrepareToolAsync(string name, JObject args, CancellationToken token) => Task.FromResult(new JObject {
            ["toolName"] = name, ["arguments"] = Scenario == "tamper" ? new JObject() : args.DeepClone(), ["target"] = new JObject { ["count"] = 2 }, ["confirmationToken"] = "test-ticket" });
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject args, string confirmationToken, CancellationToken token)
        {
            Writes++;
            if (Scenario == "partialWrite") return Task.FromResult(new McpToolResult { IsError = true, StructuredContent = JObject.Parse("{detail:'native batch failed',partialChange:{after:[{pdmObjectId:'a',state:'CheckedIn'},{pdmObjectId:'b',state:'ExclusiveModification'}]}}") });
            var save = name == "topsolid_save_documents";
            if (save) Check.Equal("openDirty", (string?)args["scope"], "Default save must not include loaded dependencies");
            else { Check.Equal("project-live", (string?)args["pdmObjectIds"]?[0], "Check-in must use live exact PDM ID"); Check.Equal(true, (bool?)args["recursive"], "Whole-project check-in must recurse"); }
            return Task.FromResult(Result(new JObject { ["operation"] = save ? "save" : "checkIn", ["complete"] = true, ["saved"] = save, ["savedCount"] = 2, ["checkedInCount"] = 1,
                ["after"] = JArray.Parse("[{name:'A',state:'CheckedIn',isDirty:false},{name:'B',state:'ExclusiveModification',isDirty:false}]") }));
        }
        private static McpToolResult Result(JObject data) => new() { StructuredContent = data, Content = new JArray(new JObject { ["type"] = "text", ["text"] = data.ToString() }) };
    }
    private sealed class PreviewOnly(StdioMcpClient native) : IConfirmableMcpClient
    {
        public bool IsConnected => native.IsConnected;
        public IReadOnlyList<McpToolDefinition> Tools => native.Tools;
        public Task<McpToolResult> CallToolAsync(string name, JObject args, CancellationToken token)
        {
            if (Tools.Any(t => t.Name == name && t.RequiresConfirmation)) throw new InvalidOperationException("Read-only fixture rejected a native write.");
            return native.CallToolAsync(name, args, token);
        }
        public Task<JObject> PrepareToolAsync(string name, JObject args, CancellationToken token) => native.PrepareToolAsync(name, args, token);
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject args, string confirmationToken, CancellationToken token) => throw new InvalidOperationException("Native writes are disabled in this fixture.");
    }
}
