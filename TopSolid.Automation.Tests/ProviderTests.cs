using System.Net;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;

namespace TopSolid.Automation.Tests;

internal static class ProviderTests
{
    private static readonly AiMessage[] Greeting = [new() { Role = "user", Content = "Hello." }];

    public static async Task OpenAiToolLoop()
    {
        var handler = new ScriptedHandler()
            .Json(OpenAiResponse("", new JArray(OpenAiCall("cloud-1", FakeMcpClient.StatusName, "{}"))))
            .Json(OpenAiResponse("Yes, TopSolid reports that it is connected."));
        using var provider = new OpenAiCompatibleProvider("https://example.test/custom/v1", "test-key", "test-model", handler);
        var mcp = new FakeMcpClient();
        var chat = new ChatSession(provider, mcp);
        Check.Equal("Yes, TopSolid reports that it is connected.",
            await chat.SendAsync("Am I connected to TopSolid?", CancellationToken.None), "Cloud tool-loop final response");
        Check.Equal(1, mcp.Calls.Count, "Cloud tool dispatch count");
        Check.Equal(2, handler.Requests.Count, "Cloud provider should receive tool results in a second request");
        var first = handler.Requests[0];
        Check.Equal("https://example.test/custom/v1/chat/completions", first.Uri.AbsoluteUri, "Cloud base path preservation");
        Check.Equal("Bearer", first.AuthorizationScheme, "Cloud authentication scheme");
        Check.Equal("test-key", first.AuthorizationParameter, "Configured cloud key");
        Check.Equal("test-model", (string?)first.Body!["model"], "Configured cloud model");
        Check.Equal(false, (bool?)first.Body["stream"], "Cloud non-streaming request");
        Check.Equal(FakeMcpClient.StatusName, (string?)first.Body["tools"]?[0]?["function"]?["name"], "Discovered tool name");
        Check.True(JToken.DeepEquals(mcp.Tools[0].InputSchema, first.Body["tools"]?[0]?["function"]?["parameters"]),
            "Discovered input schema should reach the model unchanged");
        var replay = (JArray)handler.Requests[1].Body!["messages"]!;
        var callMessage = replay.Single(m => m["tool_calls"] is JArray);
        Check.Equal("cloud-1", (string?)callMessage["tool_calls"]?[0]?["id"], "Replayed OpenAI call ID");
        Check.Equal(JTokenType.String, callMessage["tool_calls"]![0]!["function"]!["arguments"]!.Type,
            "OpenAI requires string arguments");
        var resultMessage = replay.Single(m => (string?)m["role"] == "tool");
        Check.Equal("cloud-1", (string?)resultMessage["tool_call_id"], "OpenAI tool response correlation");
        Check.True(((string?)resultMessage["content"])?.Contains("connected", StringComparison.Ordinal) == true,
            "Actual TopSolid result did not reach the cloud model");
    }

    public static async Task OllamaToolLoop()
    {
        var handler = new ScriptedHandler()
            .Json(OllamaResponse("", new JArray(new JObject
            {
                ["function"] = new JObject { ["name"] = FakeMcpClient.StatusName, ["arguments"] = new JObject() }
            }), "Use the available connection tool."))
            .Json(OllamaResponse("TopSolid is connected."));
        using var provider = new OllamaProvider("http://192.0.2.10:11434", "local-tool-model", handler);
        var mcp = new FakeMcpClient();
        var chat = new ChatSession(provider, mcp);
        Check.Equal("TopSolid is connected.", await chat.SendAsync("Am I connected?", CancellationToken.None),
            "Ollama tool-loop final response");
        Check.Equal(1, mcp.Calls.Count, "Ollama tool dispatch count");
        Check.Equal(2, handler.Requests.Count, "Ollama should receive tool result in a second request");
        var first = handler.Requests[0];
        Check.Equal("http://192.0.2.10:11434/api/chat", first.Uri.AbsoluteUri, "Configured Ollama server URL");
        Check.True(first.AuthorizationScheme == null, "Cloud credentials must not be sent to Ollama");
        Check.Equal("local-tool-model", (string?)first.Body!["model"], "Configured Ollama model");
        Check.Equal(false, (bool?)first.Body["stream"], "Ollama non-streaming request");
        Check.True(JToken.DeepEquals(mcp.Tools[0].InputSchema, first.Body["tools"]?[0]?["function"]?["parameters"]),
            "Discovered schema did not reach Ollama");
        var replay = (JArray)handler.Requests[1].Body!["messages"]!;
        var callMessage = replay.Single(m => m["tool_calls"] is JArray);
        Check.Equal(JTokenType.Object, callMessage["tool_calls"]![0]!["function"]!["arguments"]!.Type,
            "Ollama requires object arguments");
        Check.Equal("Use the available connection tool.", (string?)callMessage["thinking"], "Ollama thinking replay");
        var resultMessage = replay.Single(m => (string?)m["role"] == "tool");
        Check.Equal(FakeMcpClient.StatusName, (string?)resultMessage["tool_name"], "Ollama tool response correlation");
        Check.True(((string?)resultMessage["content"])?.Contains("connected", StringComparison.Ordinal) == true,
            "Actual TopSolid result did not reach Ollama");
    }

    public static async Task ModelDiscovery()
    {
        var cloudHandler = new ScriptedHandler().Json("{\"data\":[{\"id\":\"z\"},{\"id\":\"a\"},{\"id\":\"z\"}]}");
        using var cloud = new OpenAiCompatibleProvider("https://example.test/v1", "test-key", "", cloudHandler);
        Check.Equal("a,z", string.Join(',', await cloud.ListModelsAsync(CancellationToken.None)), "Cloud model discovery");
        Check.Equal("/v1/models", cloudHandler.Requests[0].Uri.AbsolutePath, "Cloud models path");
        var localHandler = new ScriptedHandler().Json("{\"models\":[{\"name\":\"z:latest\"},{\"name\":\"a:latest\"}]}");
        using var local = new OllamaProvider("http://localhost:11434", "", localHandler);
        Check.Equal("a:latest,z:latest", string.Join(',', await local.ListModelsAsync(CancellationToken.None)), "Ollama model discovery");
        Check.Equal("/api/tags", localHandler.Requests[0].Uri.AbsolutePath, "Ollama models path");
        Check.True(localHandler.Requests[0].AuthorizationScheme == null, "Model discovery sent cloud credentials to Ollama");
    }

    public static async Task MalformedArgumentsNeverExecute()
    {
        var handler = new ScriptedHandler()
            .Json(OpenAiResponse("", new JArray(OpenAiCall("malformed", FakeMcpClient.StatusName, "{\"x\":1,\"x\":2}"))))
            .Json(OpenAiResponse("I could not run the malformed request."));
        using var provider = new OpenAiCompatibleProvider("https://example.test/v1", "", "test-model", handler);
        var mcp = new FakeMcpClient();
        var session = new ChatSession(provider, mcp);
        await session.SendAsync("Check status.", CancellationToken.None);
        Check.Equal(0, mcp.Calls.Count, "Duplicate JSON argument properties reached MCP");
        var replay = (JArray)handler.Requests[1].Body!["messages"]!;
        var result = replay.Single(m => (string?)m["role"] == "tool");
        Check.True(((string?)result["content"])?.Contains("error", StringComparison.OrdinalIgnoreCase) == true,
            "Malformed tool arguments were not reported to model");
        Check.Equal("{\"x\":1,\"x\":2}", (string?)replay.Single(m => m["tool_calls"] is JArray)["tool_calls"]?[0]?["function"]?["arguments"],
            "Original malformed argument text should be preserved for model correction");
    }

    public static async Task ProviderErrorsAndCancellation()
    {
        const string secret = "test-key-never-display";
        var unauthorizedHandler = new ScriptedHandler().Json("{\"error\":\"" + secret + "\"}", HttpStatusCode.Unauthorized);
        using (var unauthorized = new OpenAiCompatibleProvider("https://example.test/v1", secret, "test", unauthorizedHandler))
        {
            var error = await Check.ThrowsAsync<AiProviderException>(() => unauthorized.CompleteAsync(Greeting, [], CancellationToken.None));
            Check.True(error.Message.Contains("401", StringComparison.Ordinal), "Authentication failure should be understandable");
            Check.True(!error.ToString().Contains(secret, StringComparison.Ordinal), "Raw provider error leaked credentials");
        }
        var redirectHandler = new ScriptedHandler().Json("{}", HttpStatusCode.TemporaryRedirect);
        using (var redirect = new OpenAiCompatibleProvider("https://example.test/v1", secret, "test", redirectHandler))
        {
            var error = await Check.ThrowsAsync<AiProviderException>(() => redirect.CompleteAsync(Greeting, [], CancellationToken.None));
            Check.True(error.Message.Contains("redirect", StringComparison.OrdinalIgnoreCase), "Redirects should be rejected visibly");
            Check.Equal(1, redirectHandler.Requests.Count, "A provider redirect should not be followed");
        }
        using (var incomplete = new OllamaProvider("http://localhost:11434", "test",
                   new ScriptedHandler().Json("{\"done\":false,\"message\":{\"content\":\"partial\"}}")))
            await Check.ThrowsAsync<AiProviderException>(() => incomplete.CompleteAsync(Greeting, [], CancellationToken.None));
        using (var truncated = new OpenAiCompatibleProvider("https://example.test/v1", "", "test",
                   new ScriptedHandler().Json(OpenAiResponse("", new JArray(OpenAiCall("partial", FakeMcpClient.StatusName, "{}")), "length"))))
            await Check.ThrowsAsync<AiProviderException>(() => truncated.CompleteAsync(Greeting, [], CancellationToken.None));
        using (var duplicateIds = new OpenAiCompatibleProvider("https://example.test/v1", "", "test",
                   new ScriptedHandler().Json(OpenAiResponse("", new JArray(
                       OpenAiCall("duplicate", FakeMcpClient.StatusName, "{}"), OpenAiCall("duplicate", FakeMcpClient.StatusName, "{}"))))))
            await Check.ThrowsAsync<AiProviderException>(() => duplicateIds.CompleteAsync(Greeting, [], CancellationToken.None));
        using (var pending = new OllamaProvider("http://localhost:11434", "test", new ScriptedHandler().PendingUntilCancelled()))
        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
            await Check.ThrowsAsync<OperationCanceledException>(() => pending.CompleteAsync(Greeting, [], cancellation.Token));
    }

    private static JObject OpenAiCall(string id, string name, string arguments) => new()
    {
        ["id"] = id, ["type"] = "function",
        ["function"] = new JObject { ["name"] = name, ["arguments"] = arguments }
    };

    private static string OpenAiResponse(string content, JArray? calls = null, string finishReason = "stop") => new JObject
    {
        ["choices"] = new JArray(new JObject
        {
            ["finish_reason"] = finishReason,
            ["message"] = new JObject { ["role"] = "assistant", ["content"] = content, ["tool_calls"] = calls }
        })
    }.ToString();

    private static string OllamaResponse(string content, JArray? calls = null, string? thinking = null) => new JObject
    {
        ["done"] = true,
        ["message"] = new JObject { ["role"] = "assistant", ["content"] = content, ["thinking"] = thinking, ["tool_calls"] = calls }
    }.ToString();
}
