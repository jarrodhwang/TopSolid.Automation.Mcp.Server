using System.Net.Http;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.AI;

public sealed class AnthropicProvider : IAiProvider
{
    private readonly AiHttpClient http;
    private readonly string model;

    public AnthropicProvider(string baseUrl, string apiKey, string model, HttpMessageHandler? handler = null, TimeSpan? requestTimeout = null)
    {
        http = new AiHttpClient(EndpointValidator.Validate(baseUrl, "Anthropic URL"), apiKey, handler,
            anthropic: true, authenticationHint: CloudServices.Get("anthropic").KeyHint, requestTimeout: requestTimeout);
        this.model = model.Trim();
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        var path = "models?limit=100";
        for (var page = 0; page < 20; page++)
        {
            var response = await http.SendAsync(HttpMethod.Get, path, null, cancellationToken).ConfigureAwait(false);
            if (response["data"] is not JArray data) throw new AiProviderException("Anthropic returned an invalid model list.");
            names.UnionWith(ProviderJson.ModelNames(data, "id"));
            if (response.Value<bool?>("has_more") != true) return names.Order(StringComparer.Ordinal).ToArray();
            var cursor = ProviderJson.Text(response["last_id"]);
            if (string.IsNullOrWhiteSpace(cursor) || !cursors.Add(cursor))
                throw new AiProviderException("Anthropic returned an invalid model pagination cursor.");
            path = "models?limit=100&after_id=" + Uri.EscapeDataString(cursor);
        }
        throw new AiProviderException("Anthropic model listing exceeded 20 pages. No partial list was selected.");
    }

    public async Task<AiReply> CompleteAsync(IReadOnlyList<AiMessage> messages,
        IReadOnlyList<McpToolDefinition> tools, CancellationToken cancellationToken)
    {
        ProviderJson.RequireModel(model);
        var body = new JObject
        {
            ["model"] = model, ["max_tokens"] = 4096, ["stream"] = false,
            ["messages"] = ToMessages(messages)
        };
        var system = string.Join("\n\n", messages.Where(x => x.Role == "system").Select(x => x.Content));
        if (system.Length > 0) body["system"] = system;
        if (tools.Count > 0)
            body["tools"] = new JArray(tools.Select(t => new JObject
            {
                ["name"] = t.Name, ["description"] = t.Description, ["input_schema"] = t.InputSchema.DeepClone()
            }));
        var response = await http.SendAsync(HttpMethod.Post, "messages", body, cancellationToken).ConfigureAwait(false);
        if (response["content"] is not JArray content) throw new AiProviderException("Anthropic returned no message content.");
        var stopReason = ProviderJson.Text(response["stop_reason"]);
        if (stopReason is "max_tokens" or "pause_turn")
            throw new AiProviderException("Anthropic returned an incomplete response. No tool calls were executed; try a smaller task.");
        var text = new List<string>();
        var calls = new JArray();
        foreach (var block in content)
        {
            switch (ProviderJson.Text(block["type"]))
            {
                case "text": text.Add(ProviderJson.Text(block["text"])); break;
                case "tool_use":
                    calls.Add(new JObject
                    {
                        ["id"] = block["id"]?.DeepClone(), ["type"] = "function",
                        ["function"] = new JObject { ["name"] = block["name"]?.DeepClone(), ["arguments"] = block["input"]?.DeepClone() }
                    });
                    break;
                case "thinking": case "redacted_thinking": break; // Replayed verbatim; never displayed as an answer.
                default: throw new AiProviderException("Anthropic returned an unsupported content block.");
            }
        }
        var parsed = ProviderJson.ParseToolCalls(calls, ollama: false);
        var answer = string.Join("\n", text);
        if (string.IsNullOrWhiteSpace(answer) && parsed.Count == 0)
            throw new AiProviderException("Anthropic returned an empty response without tool calls.");
        return new AiReply { Content = answer, ToolCalls = parsed, ProviderContent = (JArray)content.DeepClone() };
    }

    private static JArray ToMessages(IReadOnlyList<AiMessage> messages)
    {
        var result = new JArray();
        foreach (var message in messages)
        {
            ProviderJson.ValidateRole(message.Role);
            if (message.Role == "system") continue;
            var role = message.Role == "tool" ? "user" : message.Role;
            var blocks = new JArray();
            if (message.Role == "tool")
            {
                if (string.IsNullOrWhiteSpace(message.ToolCallId)) throw new ArgumentException("A tool result requires its original ID.");
                blocks.Add(new JObject { ["type"] = "tool_result", ["tool_use_id"] = message.ToolCallId, ["content"] = message.Content });
            }
            else if (message.Role == "assistant" && message.ProviderContent != null)
                blocks = (JArray)message.ProviderContent.DeepClone();
            else
            {
                if (message.Content.Length > 0) blocks.Add(new JObject { ["type"] = "text", ["text"] = message.Content });
                foreach (var call in message.ToolCalls)
                    blocks.Add(new JObject { ["type"] = "tool_use", ["id"] = call.Id, ["name"] = call.Name, ["input"] = call.Arguments.DeepClone() });
            }
            if (blocks.Count == 0) continue;
            // Parallel tool results must be adjacent blocks in one user turn.
            if (result.Last is JObject previous && (string?)previous["role"] == role)
                foreach (var block in blocks.ToArray()) ((JArray)previous["content"]!).Add(block.DeepClone());
            else result.Add(new JObject { ["role"] = role, ["content"] = blocks });
        }
        return result;
    }

    public void Dispose() => http.Dispose();
}
