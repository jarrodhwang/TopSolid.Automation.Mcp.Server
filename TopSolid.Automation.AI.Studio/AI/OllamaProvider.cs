using System.Net.Http;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.AI;

public sealed class OllamaProvider : IAiProvider
{
    private readonly AiHttpClient _http;
    private readonly string _model;
    private readonly bool _fastGptOss;

    public OllamaProvider(string serverUrl, string model, HttpMessageHandler? handler = null, TimeSpan? requestTimeout = null, bool fastGptOss = true)
    {
        _http = new AiHttpClient(EndpointValidator.Validate(serverUrl, "Ollama server URL", allowRemoteHttp: true), null, handler, requestTimeout: requestTimeout);
        _model = model.Trim();
        _fastGptOss = fastGptOss;
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken)
    {
        var response = await _http.SendAsync(HttpMethod.Get, "api/tags", null, cancellationToken).ConfigureAwait(false);
        if (response["models"] is not JArray models)
            throw new AiProviderException("Ollama returned an invalid model list; expected a models array.");
        return ProviderJson.ModelNames(models, "name");
    }

    public async Task<AiReply> CompleteAsync(IReadOnlyList<AiMessage> messages,
        IReadOnlyList<McpToolDefinition> tools, CancellationToken cancellationToken)
    {
        ProviderJson.RequireModel(_model);
        var body = new JObject
        {
            ["model"] = _model,
            ["messages"] = new JArray(messages.Select(ToMessage)),
            ["stream"] = false
        };
        if (tools.Count > 0) body["tools"] = ProviderJson.Tools(tools);
        // Ollama documents GPT-OSS levels (low/medium/high), not think=false.
        // https://docs.ollama.com/capabilities/thinking
        var family = _model.Split('/').Last().Split(':')[0];
        if (_fastGptOss && family.Equals("gpt-oss", StringComparison.OrdinalIgnoreCase)) body["think"] = "low";
        var response = await _http.SendAsync(HttpMethod.Post, "api/chat", body, cancellationToken).ConfigureAwait(false);
        if (response["message"] is not JObject message || response["done"]?.Type != JTokenType.Boolean ||
            response.Value<bool>("done") != true)
            throw new AiProviderException("Ollama returned an incomplete chat response. A complete non-streaming response is required.");
        if (ProviderJson.Text(response["done_reason"]) == "length")
            throw new AiProviderException("The Ollama response reached the model's output limit. Its tool calls were not executed; try a shorter request.");
        var content = ProviderJson.Text(message["content"]);
        var calls = ProviderJson.ParseToolCalls(message["tool_calls"], ollama: true);
        if (string.IsNullOrWhiteSpace(content) && calls.Count == 0)
            throw new AiProviderException("Ollama returned an empty response without any tool calls. Check that the selected model supports this request.");
        return new AiReply
        {
            Content = content,
            Thinking = ProviderJson.Text(message["thinking"]),
            ToolCalls = calls
        };
    }

    private static JObject ToMessage(AiMessage message)
    {
        ProviderJson.ValidateRole(message.Role);
        var result = new JObject { ["role"] = message.Role, ["content"] = message.Content };
        if (message.Role == "assistant" && !string.IsNullOrEmpty(message.Thinking))
            result["thinking"] = message.Thinking;
        if (message.Role == "tool")
        {
            if (string.IsNullOrWhiteSpace(message.ToolName))
                throw new ArgumentException("An Ollama tool result must include its tool name.");
            result["tool_name"] = message.ToolName;
        }
        if (message.ToolCalls.Count > 0)
        {
            if (message.Role != "assistant") throw new ArgumentException("Only assistant messages may contain tool calls.");
            // Native Ollama uses an argument object and matches tool results by
            // tool_name and order. Generated IDs remain in the application trace.
            result["tool_calls"] = new JArray(message.ToolCalls.Select((call, index) => new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["index"] = index,
                    ["name"] = call.Name,
                    ["arguments"] = call.Arguments.DeepClone()
                }
            }));
        }
        return result;
    }

    public void Dispose() => _http.Dispose();
}
