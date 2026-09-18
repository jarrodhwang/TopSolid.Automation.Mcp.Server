using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.AI;

// OpenAI Chat Completions wire format. The configurable base URL includes any
// API prefix, for example https://api.openai.com/v1 or a compatible proxy /v1.
public sealed class OpenAiCompatibleProvider : IAiProvider
{
    private readonly AiHttpClient _http;
    private readonly string _model;
    private readonly bool _gemini;

    public OpenAiCompatibleProvider(string baseUrl, string apiKey, string model,
        HttpMessageHandler? handler = null, string authenticationHint = "Check the API key for this endpoint.", TimeSpan? requestTimeout = null)
    {
        if (apiKey.Any(char.IsControl)) throw new ArgumentException("The API key contains invalid control characters.");
        var endpoint = EndpointValidator.Validate(baseUrl, "Cloud base URL");
        _gemini = endpoint.Host == "generativelanguage.googleapis.com" && endpoint.AbsolutePath.TrimEnd('/') == "/v1beta/openai";
        _http = new AiHttpClient(endpoint, apiKey, handler,
            authenticationHint: authenticationHint, requestTimeout: requestTimeout);
        _model = _gemini ? NormalizeGeminiModel(model) : model.Trim();
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken)
    {
        var response = await _http.SendAsync(HttpMethod.Get, "models", null, cancellationToken).ConfigureAwait(false);
        if (response["data"] is not JArray models)
            throw new AiProviderException("The AI endpoint returned an invalid model list; expected a data array.");
        var names = ProviderJson.ModelNames(models, "id");
        return _gemini ? names.Select(NormalizeGeminiModel).Where(n => n.Length > 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() : names;
    }

    internal static string NormalizeGeminiModel(string model)
    {
        var value = model.Trim();
        return value.StartsWith("models/", StringComparison.Ordinal) ? value[7..] : value;
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
        if (tools.Count > 0)
        {
            body["tools"] = ProviderJson.Tools(tools);
            body["tool_choice"] = "auto";
        }
        var response = await _http.SendAsync(HttpMethod.Post, "chat/completions", body, cancellationToken,
            hasImageAttachments: messages.Any(message => message.Images.Count > 0)).ConfigureAwait(false);
        if (response["choices"] is not JArray { Count: > 0 } choices ||
            choices[0] is not JObject choice || choice["message"] is not JObject message)
            throw new AiProviderException("The AI endpoint returned no chat message.");
        var finishReason = ProviderJson.Text(choice["finish_reason"]);
        if (finishReason == "length")
            throw new AiProviderException("The AI response reached the model's output limit. Its tool calls were not executed; try a shorter request.");
        if (finishReason == "content_filter")
            throw new AiProviderException("The AI endpoint filtered this response. Try rephrasing the request.");

        var content = ProviderJson.Text(message["content"]);
        if (content.Length == 0) content = ProviderJson.Text(message["refusal"]);
        var calls = ProviderJson.ParseToolCalls(message["tool_calls"], ollama: false);
        if (string.IsNullOrWhiteSpace(content) && calls.Count == 0)
            throw new AiProviderException("The AI endpoint returned an empty response without any tool calls.");
        return new AiReply { Content = content, ToolCalls = calls,
            Thinking = message["reasoning_content"] == null ? null : ProviderJson.Text(message["reasoning_content"]),
            ExtraContent = message["extra_content"] is JObject extra ? (JObject)extra.DeepClone() : null };
    }

    private JObject ToMessage(AiMessage message)
    {
        ProviderJson.ValidateRole(message.Role);
        var result = new JObject { ["role"] = message.Role, ["content"] = message.Content };
        if (message.Images.Count > 0)
        {
            if (message.Role != "user") throw new ArgumentException("Only user messages may contain image attachments.");
            var content = new JArray(new JObject { ["type"] = "text", ["text"] = message.Content });
            foreach (var image in message.Images)
                content.Add(new JObject { ["type"] = "image_url", ["image_url"] = new JObject
                    { ["url"] = "data:" + image.MediaType + ";base64," + image.Base64 } });
            result["content"] = content;
        }
        if (message.Role == "assistant")
        {
            if (message.Thinking != null) result["reasoning_content"] = message.Thinking;
            if (message.ExtraContent != null) result["extra_content"] = message.ExtraContent.DeepClone();
        }
        if (message.Role == "tool")
        {
            if (string.IsNullOrWhiteSpace(message.ToolCallId))
                throw new ArgumentException("A tool result must include its original tool-call ID.");
            result["tool_call_id"] = message.ToolCallId;
        }
        if (message.ToolCalls.Count > 0)
        {
            if (message.Role != "assistant") throw new ArgumentException("Only assistant messages may contain tool calls.");
            if (string.IsNullOrEmpty(message.Content)) result["content"] = null;
            result["tool_calls"] = new JArray(message.ToolCalls.Select(ToToolCall));
        }
        return result;
    }

    private JObject ToToolCall(AiToolCall call)
    {
        var result = new JObject
            {
                ["id"] = call.Id,
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = call.Name,
                    ["arguments"] = call.RawArguments ?? call.Arguments.ToString(Formatting.None)
                }
            };
        if (call.ExtraContent != null) result["extra_content"] = call.ExtraContent.DeepClone();
        else if (_gemini && call.ClientInitiated)
            // Google's documented marker for deterministic client-created calls:
            // https://ai.google.dev/gemini-api/docs/generate-content/thought-signatures
            result["extra_content"] = new JObject { ["google"] = new JObject { ["thought_signature"] = "skip_thought_signature_validator" } };
        return result;
    }

    public void Dispose() => _http.Dispose();
}
