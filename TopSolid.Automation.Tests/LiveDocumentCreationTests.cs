using System.Diagnostics;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class LiveDocumentCreationTests
{
    private const string Project = "AI Made this project";
    private const string Evidence = "artifacts/document-creation-0.5.4";
    public static async Task Preview(string server)
    {
        await using var native = new StdioMcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        await native.ConnectAsync(server, timeout.Token);
        var guarded = new PreviewOnly(native); var record = new JObject { ["nativeWrites"] = 0 }; var timer = Stopwatch.StartNew();
        try
        {
            var context = Data(await guarded.CallToolAsync("topsolid_get_document_creation_context", new JObject { ["projectName"] = Project }, timeout.Token));
            record["contextSeconds"] = timer.Elapsed.TotalSeconds; record["context"] = context;
            Check.True((bool?)context["canChooseUniqueProject"] == true, "Named project lookup is incomplete/ambiguous");
            var projectId = (string)context["projectMatches"]![0]!["pdmObjectId"]!;
            var upper = Data(await guarded.CallToolAsync("topsolid_get_document_creation_context", new JObject { ["projectName"] = Project.ToUpperInvariant() }, timeout.Token));
            Check.Equal(projectId, (string?)upper["projectMatches"]?[0]?["pdmObjectId"], "Native project lookup must handle the log's case variation");
            Check.Equal(false, (bool?)context["creation"]?["useDefaultTemplate"], "Context default unexpectedly selected a template");
            Check.Equal(false, (bool?)context["creation"]?["loadedDocumentRequired"], "Context still requires a loaded part");
            Check.True((bool?)context["readiness"]?["canCreatePdmObjects"] == true, "TopSolid has an active command; live previews must wait for the user to finish it");
            var previews = new JArray(); record["previews"] = previews;
            foreach (var extension in new[] { ".TopPrt", ".TopAsm", ".Top2D", ".TopMillTurn" })
            {
                timer.Restart();
                var proposal = await guarded.PrepareToolAsync("topsolid_create_document", new JObject { ["ownerId"] = projectId, ["name"] = "Preview only - no document created", ["extension"] = extension }, timeout.Token);
                Check.Equal("empty", (string?)proposal["target"]?["creationMode"], "Preview must explicitly show empty creation");
                Check.Equal(false, (bool?)proposal["target"]?["useDefaultTemplate"], "Preview must disable the template");
                previews.Add(new JObject { ["extension"] = extension, ["seconds"] = timer.Elapsed.TotalSeconds, ["target"] = proposal["target"]!.DeepClone() });
            }
            using var provider = PdmFastPathTests.NoInference();
            var session = new ChatSession(provider, guarded) { ConfirmChangeAsync = (_, _) => Task.FromResult(false) };
            timer.Restart();
            var answer = await session.SendAsync("Create a part named \"MCP extension preview only\" in project \"AI Made this project\"", timeout.Token);
            record["directChatSeconds"] = timer.Elapsed.TotalSeconds;
            Check.True(answer.Contains("declined") && provider.CompletionCount == 0, "Direct named creation must use one lookup and a declined preview without inference");
            Console.WriteLine($"Empty-document lookup {record["contextSeconds"]} s; direct named-part preview {record["directChatSeconds"]} s; no native writes.");
        }
        finally { Directory.CreateDirectory(Evidence); File.WriteAllText(Evidence + "/live-preview.json", record.ToString()); }
    }

    public static async Task Model(string server)
    {
        var store = new SettingsStore(); var before = File.ReadAllBytes(store.FilePath); var settings = store.Load();
        Check.Equal("gemini", settings.CloudService, "This opt-in model test uses the reported saved Gemini account");
        settings.Provider = AppSettings.OpenAiProvider;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        await using var native = new StdioMcpClient(); await native.ConnectAsync(server, timeout.Token);
        var guarded = new PreviewOnly(native); using var provider = ProviderFactory.Create(settings);
        var trace = new JArray(); var previews = 0;
        var session = new ChatSession(provider, guarded) { ConfirmChangeAsync = (proposal, _) => {
            Check.True((string?)proposal["toolName"] is "topsolid_create_part_document" or "topsolid_create_document", "Model proposed an unrelated mutation");
            Check.Equal("empty", (string?)proposal["target"]?["creationMode"], "Model must propose extension-only empty creation");
            previews++; return Task.FromResult(false); } };
        session.Trace += t => trace.Add(new JObject { ["kind"] = t.Kind, ["text"] = t.Text });
        const string request = "I need you to create an empty .TopPrt part named \"MCP extension preview only\" in the project \"AI Made this project\", without a template. Do not open or save it.";
        var timer = Stopwatch.StartNew(); string? answer = null;
        try
        {
            answer = await session.SendAsync(request, timeout.Token);
            Check.Equal(1, previews, "Model should propose exactly one plain part creation, which the harness declines");
            Check.True(!guarded.Calls.Any(c => c.Contains("template") || c.Contains("loaded") || c.Contains("summaries") || c.Contains("libraries") || c.Contains("api_reference")), "Model still searched templates/loaded documents to create a plain part");
            Console.WriteLine($"Live {settings.CloudModel}: {timer.Elapsed.TotalSeconds:F2} s, {trace.Count(t => (string?)t["kind"] == "Model")} model rounds, {guarded.Calls.Count} reads, one declined preview, zero native writes.");
        }
        finally
        {
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence + "/live-model.json", new JObject { ["model"] = settings.CloudModel, ["request"] = request, ["answer"] = answer,
                ["seconds"] = timer.Elapsed.TotalSeconds, ["reads"] = new JArray(guarded.Calls), ["declinedPreviews"] = previews, ["nativeWrites"] = 0, ["trace"] = trace }.ToString());
            Check.True(before.SequenceEqual(File.ReadAllBytes(store.FilePath)), "Live test changed saved settings");
        }
    }
    private static JObject Data(McpToolResult result)
    {
        Check.True(!result.IsError, "Native read failed: " + result.Content);
        return result.StructuredContent ?? JObject.Parse((string)result.Content[0]["text"]!);
    }
    private sealed class PreviewOnly(StdioMcpClient native) : IConfirmableMcpClient
    {
        public List<string> Calls { get; } = [];
        public bool IsConnected => native.IsConnected;
        public IReadOnlyList<McpToolDefinition> Tools => native.Tools;
        public Task<McpToolResult> CallToolAsync(string name, JObject args, CancellationToken token)
        {
            if (Tools.Any(t => t.Name == name && t.RequiresConfirmation)) throw new InvalidOperationException("Live fixture is read-only; mutations are disabled.");
            Calls.Add(name); return native.CallToolAsync(name, args, token);
        }
        public Task<JObject> PrepareToolAsync(string name, JObject args, CancellationToken token) => native.PrepareToolAsync(name, args, token);
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject args, string confirmationToken, CancellationToken token) => throw new InvalidOperationException("Native writes are disabled in this fixture.");
    }
}
