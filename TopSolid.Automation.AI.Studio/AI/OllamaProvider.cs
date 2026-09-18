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
        // Gemma 4's native Ollama manifest advertises boolean thinking support.
        // Keep other model families' options unchanged; this switch is reversible.
        if (_fastGptOss && family.Equals("gemma4", StringComparison.OrdinalIgnoreCase)) body["think"] = false;
        var response = await _http.SendAsync(HttpMethod.Post, "api/chat", body, cancellationToken,
            hasImageAttachments: messages.Any(message => message.Images.Count > 0)).ConfigureAwait(false);
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
            Metrics = Metrics(response, _model),
            ToolCalls = calls
        };
    }

    internal static JObject Metrics(JObject response, string model)
    {
        var metrics = new JObject { ["provider"] = "ollama", ["model"] = model };
        foreach (var field in new[] { "total_duration", "load_duration", "prompt_eval_duration", "eval_duration" })
            if (response[field]?.Type is JTokenType.Integer or JTokenType.Float && (double)response[field]! >= 0)
                metrics[field.Replace("_duration", "_seconds", StringComparison.Ordinal)] = (double)response[field]! / 1e9;
        foreach (var field in new[] { "prompt_eval_count", "prompt_eval_cached_count", "eval_count" })
            if (response[field]?.Type == JTokenType.Integer && (long)response[field]! >= 0) metrics[field] = response[field]!.DeepClone();
        if (metrics["eval_seconds"] != null && (double)metrics["eval_seconds"]! > 0 && metrics["eval_count"] != null)
            metrics["generatedTokensPerSecond"] = Math.Round((double)metrics["eval_count"]! / (double)metrics["eval_seconds"]!, 2);
        return metrics;
    }

    private static JObject ToMessage(AiMessage message)
    {
        ProviderJson.ValidateRole(message.Role);
        var result = new JObject { ["role"] = message.Role, ["content"] = message.Content };
        if (message.Images.Count > 0)
        {
            if (message.Role != "user") throw new ArgumentException("Only user messages may contain image attachments.");
            result["images"] = new JArray(message.Images.Select(image => image.Base64));
        }
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
