using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class PdmNamesTests
{
    // Explicit opt-in: the user requested these live names through their configured
    // Gemini service. Only the two PDM inspection tools can dispatch in this harness.
    public static async Task Live(string server, string? modelOverride = null, bool ollama = false)
    {
        var store = new SettingsStore();
        var settingsBefore = File.ReadAllBytes(store.FilePath);
        var settings = store.Load();
        Check.Equal("gemini", settings.CloudService, "This live test is for the reported Gemini configuration");
        if (modelOverride != null) settings.CloudModel = modelOverride;
        if (ollama) settings.Provider = AppSettings.OllamaProvider;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await using var mcp = new StdioMcpClient();
        await mcp.ConnectAsync(Path.GetFullPath(server), timeout.Token);
        var expected = new Dictionary<string, JArray>();
        foreach (var category in new[] { "projects", "libraries" })
        {
            var items = new JArray();
            for (var offset = 0; ; offset += 100)
            {
                var result = await mcp.CallToolAsync("topsolid_list_" + category, new JObject { ["offset"] = offset, ["limit"] = 100 }, timeout.Token);
                Check.True(!result.IsError, "Live PDM name query failed");
                var page = JObject.Parse((string)result.Content[0]!["text"]!);
                foreach (var item in (JArray)page["items"]!)
                {
                    Check.True(item["name"]?.Type == JTokenType.String && !string.IsNullOrEmpty((string?)item["pdmObjectId"]), "PDM page must include names and IDs");
                    items.Add(item.DeepClone());
                }
                if (!(bool)page["hasMore"]!)
                {
                    Check.Equal((int)page["total"]!, items.Count, "All PDM pages collected");
                    break;
                }
                Check.True(offset < 10000, "Unexpected PDM page count");
            }
            Check.Equal(items.Count, items.Select(x => (string?)x["pdmObjectId"]).Distinct().Count(), "Pagination duplicated PDM objects");
            expected[category] = items;
        }
        Directory.CreateDirectory("artifacts/pdm-names");
        File.WriteAllText("artifacts/pdm-names/native-results.json", JObject.FromObject(expected).ToString());
        File.WriteAllText("artifacts/pdm-names/ALL-PROJECTS-AND-LIBRARIES.txt", "TopSolid PDM projects and libraries\n\n" +
            string.Join("\n\n", expected.Select(group => group.Key.ToUpperInvariant() + " (" + group.Value.Count + ")\n" +
                string.Join("\n", group.Value.Select(x => (string)x["name"]!).Order(StringComparer.Ordinal)))));
        File.WriteAllText("artifacts/pdm-names/ALL-PROJECTS-AND-LIBRARIES.md", "# TopSolid PDM projects and libraries\n\nRead from the live TopSolid Automation API.\n\n" +
            string.Join("\n\n", expected.Select(group => "## " + group.Key + " (" + group.Value.Count + ")\n\n" +
                string.Join("\n", group.Value.Select(x => (string)x["name"]!).Order(StringComparer.Ordinal).Select(name => "- " + name.Replace("\r", " ").Replace("\n", " "))))));
        Console.WriteLine($"Native PDM names: {expected["projects"].Count} projects, {expected["libraries"].Count} libraries; every page verified.");
        var guarded = new NameToolsOnly(mcp);
        var diagnosticHandler = new ErrorDiagnosticHandler(ollama ? "" : settings.ApiKey);
        using var provider = ProviderFactory.Create(settings, diagnosticHandler);
        var session = new ChatSession(provider, guarded);
        var trace = new List<string>();
        var providerLabel = ollama ? "ollama" : "gemini";
        session.Trace += entry => trace.Add(entry.Kind + ": " + entry.Text);
        try
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var answer = await session.SendAsync("List of all projects and libraries name", timeout.Token);
            timer.Stop();
            var measuredModel = ollama ? settings.OllamaModel : settings.CloudModel;
            File.WriteAllText("artifacts/pdm-names/" + (ollama ? "ollama" : "gemini") + "-timing.json", new JObject {
                ["model"] = measuredModel, ["elapsedSeconds"] = timer.Elapsed.TotalSeconds,
                ["modelRequests"] = trace.Count(x => x.StartsWith("Model:")), ["mcpCalls"] = guarded.Calls.Count,
                ["projects"] = expected["projects"].Count, ["libraries"] = expected["libraries"].Count,
                ["http"] = new JArray(diagnosticHandler.Measurements) }.ToString());
            Console.WriteLine($"Measured {measuredModel}: {timer.Elapsed.TotalSeconds:F2}s, {trace.Count(x => x.StartsWith("Model:"))} model requests, {guarded.Calls.Count} MCP calls.");
            File.WriteAllText("artifacts/pdm-names/" + providerLabel + "-answer.md", answer);
            var missing = expected.Values.SelectMany(x => x).Select(x => (string)x["name"]!).Where(name => !answer.Contains(name, StringComparison.Ordinal)).ToArray();
            Check.Equal(0, missing.Length, "Gemini answer omitted native names: " + string.Join(", ", missing));
            Check.True(guarded.Calls.Contains("topsolid_list_projects") && guarded.Calls.Contains("topsolid_list_libraries"), "Model must query both live lists");
            Console.WriteLine($"Live {measuredModel}: all {expected.Values.Sum(x => x.Count)} friendly names present; {guarded.Calls.Count} MCP calls.");
        }
        finally
        {
            File.WriteAllLines("artifacts/pdm-names/" + providerLabel + "-trace.txt", trace);
            Check.True(settingsBefore.SequenceEqual(File.ReadAllBytes(store.FilePath)), "Live test must preserve user settings");
        }
    }

    private sealed class NameToolsOnly(StdioMcpClient inner) : IMcpClient
    {
        public bool IsConnected => inner.IsConnected;
        public IReadOnlyList<McpToolDefinition> Tools => inner.Tools;
        public List<string> Calls { get; } = [];
        public Task<McpToolResult> CallToolAsync(string name, JObject arguments, CancellationToken cancellationToken)
        {
            if (name is not ("topsolid_list_projects" or "topsolid_list_libraries"))
                throw new InvalidOperationException("This read-only fixture allows only project/library name lists.");
            Calls.Add(name);
            return inner.CallToolAsync(name, arguments, cancellationToken);
        }
    }

    private sealed class ErrorDiagnosticHandler(string key) : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false })
    {
        public List<JObject> Measurements { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var requestBody = request.Content == null ? "" : await request.Content.ReadAsStringAsync(token);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await base.SendAsync(request, token);
            var responseText = await response.Content.ReadAsStringAsync(token);
            stopwatch.Stop();
            JObject? parsed = null;
            try { parsed = JObject.Parse(responseText); } catch (Newtonsoft.Json.JsonException) { }
            Measurements.Add(new JObject { ["requestBytes"] = System.Text.Encoding.UTF8.GetByteCount(requestBody),
                ["elapsedSeconds"] = stopwatch.Elapsed.TotalSeconds, ["status"] = (int)response.StatusCode,
                ["usage"] = parsed?["usage"]?.DeepClone(), ["promptEvalCount"] = parsed?["prompt_eval_count"]?.DeepClone(),
                ["evalCount"] = parsed?["eval_count"]?.DeepClone(), ["loadDurationNanoseconds"] = parsed?["load_duration"]?.DeepClone() });
            if (!response.IsSuccessStatusCode)
            {
                var body = await request.Content!.ReadAsStringAsync(token);
                var error = await response.Content.ReadAsStringAsync(token);
                if (key.Length > 0) error = error.Replace(key, "[redacted]", StringComparison.Ordinal).Replace(Uri.EscapeDataString(key), "[redacted]", StringComparison.Ordinal);
                File.WriteAllText("artifacts/pdm-names/provider-error.json", new JObject
                {
                    ["url"] = request.RequestUri!.GetLeftPart(UriPartial.Path), ["model"] = JObject.Parse(body)["model"],
                    ["status"] = (int)response.StatusCode, ["contentType"] = response.Content.Headers.ContentType?.MediaType,
                    ["body"] = error[..Math.Min(8192, error.Length)]
                }.ToString());
            }
            return response;
        }
    }
}
