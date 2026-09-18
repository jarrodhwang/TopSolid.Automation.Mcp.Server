using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Settings;

namespace TopSolid.Automation.Tests;

internal static class CloudServiceTests
{
    public static async Task LiveModelList(string expectedService)
    {
        var store = new SettingsStore();
        var before = File.ReadAllBytes(store.FilePath);
        var settings = store.Load();
        Check.Equal(expectedService, settings.CloudService, "Live test must use the explicitly selected saved service");
        Check.Equal(AppSettings.OpenAiProvider, settings.Provider, "Live cloud test requires saved cloud configuration");
        Check.Equal(CloudServices.Get(expectedService).BaseUrl, settings.CloudBaseUrl.TrimEnd('/'), "Live listing must use the official preset endpoint");
        using var provider = ProviderFactory.Create(settings);
        var models = await provider.ListModelsAsync(default);
        Check.True(models.Count > 0, "Live service returned no models");
        Console.WriteLine($"Live {expectedService}: {models.Count} model IDs retrieved. No chat or CAD data sent.");
        Directory.CreateDirectory("artifacts/pdm-names");
        File.WriteAllText("artifacts/pdm-names/available-models.json", JArray.FromObject(models).ToString());
        Console.WriteLine($"Saved model '{settings.CloudModel}' listed: {models.Contains(settings.CloudModel)}");
        Check.True(before.SequenceEqual(File.ReadAllBytes(store.FilePath)), "Live model listing must not rewrite user settings");
    }

    public static async Task PresetRoutesAndGemini()
    {
        foreach (var preset in CloudServices.All.Where(x => x.Id != CloudServices.Custom))
        {
            var settings = new AppSettings();
            settings.SelectCloudService(preset.Id);
            settings.ApiKey = "synthetic-" + preset.Id;
            var handler = new ScriptedHandler().Json("{\"data\":[{\"id\":\"test-model\"}]}");
            using var provider = ProviderFactory.Create(settings, handler);
            Check.Equal("test-model", (await provider.ListModelsAsync(default)).Single(), preset.Name + " discovery");
            var request = handler.Requests.Single();
            Check.Equal(preset.BaseUrl + (preset.Id == "anthropic" ? "/models?limit=100" : "/models"), request.Uri.AbsoluteUri, preset.Name + " model route");
            if (preset.Id == "anthropic")
            {
                Check.Equal(settings.ApiKey, request.Headers["x-api-key"], "Anthropic key header");
                Check.Equal("2023-06-01", request.Headers["anthropic-version"], "Anthropic version header");
                Check.True(request.AuthorizationScheme == null, "Anthropic must not use Bearer");
            }
            else Check.Equal(settings.ApiKey, request.AuthorizationParameter, preset.Name + " Bearer key");
        }
        var gemini = new AppSettings(); gemini.SelectCloudService("gemini");
        Check.Equal("https://generativelanguage.googleapis.com/v1beta/openai", gemini.CloudBaseUrl, "Gemini API prefix");
        Check.Throws<ArgumentException>(() => ProviderFactory.Create(gemini));
        gemini.ApiKey = "synthetic-gemini-key"; gemini.CloudModel = "models/test-gemini";
        using (var discovery = ProviderFactory.Create(gemini, new ScriptedHandler().Json("{\"data\":[{\"id\":\"models/test-gemini\"},{\"id\":\"test-gemini\"}]}")))
            Check.Equal("test-gemini", (await discovery.ListModelsAsync(default)).Single(), "Gemini model IDs normalize and deduplicate");
        var signature = JObject.Parse("{\"google\":{\"thought_signature\":\"opaque-test-signature\"}}");
        var call = new JObject
        {
            ["id"] = "gemini-1", ["type"] = "function", ["extra_content"] = signature.DeepClone(),
            ["function"] = new JObject { ["name"] = FakeMcpClient.StatusName, ["arguments"] = "{}" }
        };
        var firstMessage = new JObject { ["role"] = "assistant", ["content"] = "", ["tool_calls"] = new JArray(call), ["extra_content"] = signature.DeepClone() };
        var replies = new ScriptedHandler().Json(new JObject { ["choices"] = new JArray(new JObject { ["message"] = firstMessage }) }.ToString())
            .Json("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"Connected.\"}}]}");
        using (var provider = ProviderFactory.Create(gemini, replies))
        {
            var mcp = new FakeMcpClient();
            Check.Equal("Connected.", await new ChatSession(provider, mcp).SendAsync("Check TopSolid", default), "Gemini tool round trip");
            Check.Equal(1, mcp.Calls.Count, "Gemini MCP dispatch");
            var replay = ((JArray)replies.Requests[1].Body!["messages"]!).Single(x => x["tool_calls"] != null);
            Check.True(JToken.DeepEquals(signature, replay["tool_calls"]![0]!["extra_content"]), "Gemini function signature lost");
            Check.True(JToken.DeepEquals(signature, replay["extra_content"]), "Gemini message signature lost");
            Check.Equal(gemini.CloudBaseUrl + "/chat/completions", replies.Requests[0].Uri.AbsoluteUri, "Gemini chat route");
            Check.Equal("test-gemini", (string?)replies.Requests[0].Body!["model"], "Gemini request must strip models/ prefix");
        }
        using (var rejected = ProviderFactory.Create(gemini, new ScriptedHandler().Json("{}", HttpStatusCode.Unauthorized)))
        {
            var error = await Check.ThrowsAsync<AiProviderException>(() => rejected.ListModelsAsync(default));
            Check.True(error.Message.Contains("Google AI Studio") && error.Message.Contains("401"), "Gemini auth guidance");
            Check.True(!error.Message.Contains(gemini.ApiKey), "Authentication error leaked key");
        }
        foreach (var host in new[] { gemini.CloudBaseUrl, "https://example.test/v1" }) {
            var handler = new ScriptedHandler().Json("{choices:[{message:{role:'assistant',content:'Ready.'}}]}");
            using var provider = new OpenAiCompatibleProvider(host, "synthetic-key", "test", handler);
            await provider.CompleteAsync([new AiMessage { Role = "user", Content = "Use current part" },
                new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = "client-read", Name = ActiveDocumentContext.Tool, ClientInitiated = true }] },
                new AiMessage { Role = "tool", ToolCallId = "client-read", Content = "{documentId:'test'}" }], [], default);
            var marker = (string?)handler.Requests[0].Body!["messages"]![1]!["tool_calls"]![0]!["extra_content"]?["google"]?["thought_signature"];
            Check.Equal(host == gemini.CloudBaseUrl ? "skip_thought_signature_validator" : null, marker, "Client context calls need the Gemini-only documented signature marker");
        }
        gemini.CloudBaseUrl = "https://generativelanguage.googleapis.com/v1beta"; gemini.ApiKey = "test";
        Check.True(Check.Throws<ArgumentException>(() => ProviderFactory.Create(gemini)).Message.Contains("Cloud service"), "Native Gemini URL must give actionable correction");
        foreach (var errorBody in new[] { "{\"error\":{\"message\":\"This model is unavailable. key=synthetic-secret\"}}", "[{\"error\":{\"message\":\"This model is unavailable. key=synthetic-secret\"}}]" })
        {
            using var missing = new OpenAiCompatibleProvider("https://example.test/v1", "synthetic-secret", "test", new ScriptedHandler().Json(errorBody, HttpStatusCode.NotFound));
            var error = await Check.ThrowsAsync<AiProviderException>(() => missing.CompleteAsync([new AiMessage { Content = "test" }], [], default));
            Check.True(error.Message.Contains("This model is unavailable") && !error.Message.Contains("synthetic-secret"), "Provider model-unavailability detail must be shown with credentials redacted");
        }
    }

    public static async Task AnthropicToolLoopAndPagination()
    {
        var content = new JArray(
            new JObject { ["type"] = "thinking", ["thinking"] = "private", ["signature"] = "opaque" },
            new JObject { ["type"] = "tool_use", ["id"] = "claude-1", ["name"] = FakeMcpClient.StatusName, ["input"] = new JObject() },
            new JObject { ["type"] = "tool_use", ["id"] = "claude-2", ["name"] = FakeMcpClient.StatusName, ["input"] = new JObject() });
        var handler = new ScriptedHandler().Json(new JObject { ["stop_reason"] = "tool_use", ["content"] = content }.ToString())
            .Json("{\"stop_reason\":\"end_turn\",\"content\":[{\"type\":\"text\",\"text\":\"Connected.\"}]}");
        using var provider = new AnthropicProvider("https://api.anthropic.com/v1", "test-key", "test-claude", handler);
        var mcp = new FakeMcpClient();
        Check.Equal("Connected.", await new ChatSession(provider, mcp).SendAsync("Check TopSolid", default), "Claude final answer");
        Check.Equal(2, mcp.Calls.Count, "Claude parallel tool calls");
        Check.Equal("/v1/messages", handler.Requests[0].Uri.AbsolutePath, "Claude Messages API");
        var body = handler.Requests[0].Body!;
        Check.True(body["system"]?.Type == JTokenType.String, "System prompt must be top level");
        Check.True(JToken.DeepEquals(mcp.Tools[0].InputSchema, body["tools"]![0]!["input_schema"]), "Claude tool schema");
        var messages = (JArray)handler.Requests[1].Body!["messages"]!;
        Check.True(messages.All(x => (string?)x["role"] != "system"), "No system role in Claude messages");
        Check.True(JToken.DeepEquals(content, messages.Single(x => (string?)x["role"] == "assistant")["content"]), "Claude opaque blocks must replay exactly");
        var results = (JArray)messages.Last!["content"]!;
        Check.Equal(2, results.Count, "Parallel tool results share one user message");
        Check.Equal("claude-2", (string?)results[1]["tool_use_id"], "Claude tool result correlation");
        var pages = new ScriptedHandler().Json("{\"data\":[{\"id\":\"z\"}],\"has_more\":true,\"last_id\":\"z&x\"}")
            .Json("{\"data\":[{\"id\":\"a\"},{\"id\":\"z\"}],\"has_more\":false}");
        using var discovery = new AnthropicProvider("https://api.anthropic.com/v1", "test-key", "", pages);
        Check.Equal("a,z", string.Join(',', await discovery.ListModelsAsync(default)), "Claude all model pages");
        Check.True(pages.Requests[1].Uri.Query.Contains("after_id=z%26x"), "Pagination cursor must be encoded");
        var truncated = new ScriptedHandler().Json(new JObject { ["stop_reason"] = "max_tokens", ["content"] = content }.ToString());
        using var partial = new AnthropicProvider("https://api.anthropic.com/v1", "test-key", "test", truncated);
        var noCalls = new FakeMcpClient();
        await Check.ThrowsAsync<AiProviderException>(() => new ChatSession(partial, noCalls).SendAsync("Check", default));
        Check.Equal(0, noCalls.Calls.Count, "Incomplete Claude response must not execute tools");
    }

    public static Task ProfilePersistence()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TopSolid.Automation.Tests", Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(directory);
        try
        {
            var settings = new AppSettings { ApiKey = "test-openai-private", CloudModel = "openai-test" };
            settings.SelectCloudService("gemini");
            Check.Equal("", settings.ApiKey, "OpenAI key must not reach Gemini");
            Check.Equal("", settings.CloudModel, "Model belongs to its service");
            settings.ApiKey = "test-gemini-private"; settings.CloudModel = "gemini-test";
            settings.SelectCloudService("anthropic");
            settings.ApiKey = "test-claude-private"; settings.CloudModel = "claude-test";
            store.Save(settings);
            var persisted = File.ReadAllText(store.FilePath);
            foreach (var secret in new[] { "test-openai-private", "test-gemini-private", "test-claude-private" })
            {
                Check.True(!persisted.Contains(secret), "Profile keys must be encrypted");
                Check.True(!JsonConvert.SerializeObject(settings).Contains(secret), "Generic serialization must not expose profile keys");
            }
            var loaded = store.Load();
            Check.Equal("anthropic", loaded.CloudService, "Selected service persisted");
            loaded.SelectCloudService("gemini");
            Check.Equal("test-gemini-private", loaded.ApiKey, "Gemini key restored");
            Check.Equal("gemini-test", loaded.CloudModel, "Gemini model restored");
            loaded.SelectCloudService("openai");
            Check.Equal("test-openai-private", loaded.ApiKey, "OpenAI key restored");
            // Existing v1 files without a service field infer the service from the endpoint.
            var legacy = JObject.Parse(persisted); legacy.Remove("cloudService"); legacy.Remove("cloudProfiles");
            File.WriteAllText(store.FilePath, legacy.ToString());
            Check.Equal("anthropic", store.Load().CloudService, "Legacy endpoint service inference");
            var tampered = JObject.Parse(persisted);
            tampered["cloudProfiles"]!["gemini"]!["baseUrl"] = "https://example.test/v1";
            File.WriteAllText(store.FilePath, tampered.ToString());
            var invalid = store.Load(); invalid.SelectCloudService("gemini");
            Check.Equal("", invalid.ApiKey, "Profile endpoint tampering must clear the key");
            // A native Gemini v1beta endpoint must migrate without discarding its
            // existing Google key, while keeping the original file unchanged.
            store.Save(new AppSettings { CloudBaseUrl = "https://generativelanguage.googleapis.com/v1beta", CloudModel = "models/test-gemini", ApiKey = "test-google-private" });
            var nativeFile = File.ReadAllText(store.FilePath);
            var migrated = store.Load();
            Check.Equal("gemini", migrated.CloudService, "Native Gemini migration selects preset");
            Check.Equal(CloudServices.Get("gemini").BaseUrl, migrated.CloudBaseUrl, "Native Gemini route migrated");
            Check.Equal("test-google-private", migrated.ApiKey, "Same-origin Gemini migration preserves protected key");
            Check.Equal("test-gemini", migrated.CloudModel, "Native model prefix removed");
            Check.Equal(nativeFile, File.ReadAllText(store.FilePath), "Migration waits for user Save");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
        return Task.CompletedTask;
    }
}
