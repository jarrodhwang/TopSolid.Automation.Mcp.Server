using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.AI;

internal static class ProviderJson
{
    public static JArray Tools(IReadOnlyList<McpToolDefinition> tools) => new(tools.Select(tool =>
        new JObject
        {
            ["type"] = "function",
            ["function"] = new JObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parameters"] = tool.InputSchema.DeepClone()
            }
        }));

    public static string Text(JToken? value) => value?.Type switch
    {
        null or JTokenType.Null => "",
        JTokenType.String => value.Value<string>() ?? "",
        _ => throw new AiProviderException("The AI endpoint returned an unsupported text format.")
    };

    public static IReadOnlyList<string> ModelNames(JArray models, string nameProperty)
    {
        var names = new List<string>();
        foreach (var model in models)
        {
            if (model is not JObject item)
                throw new AiProviderException("The AI endpoint returned an invalid model-list entry.");
            var name = Text(item[nameProperty]);
            if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
        }
        return names.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<AiToolCall> ParseToolCalls(JToken? token, bool ollama)
    {
        if (token is null || token.Type == JTokenType.Null) return Array.Empty<AiToolCall>();
        if (token is not JArray calls)
            throw new AiProviderException("The AI endpoint returned an invalid tool-call list.");
        if (calls.Count > 32)
            throw new AiProviderException("The AI model requested more than 32 tools in one response. Ask for a smaller task.");

        var result = new List<AiToolCall>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tokenCall in calls)
        {
            if (tokenCall is not JObject call || call["function"] is not JObject function ||
                (call["type"] is not null && Text(call["type"]) != "function"))
                throw new AiProviderException("The AI endpoint returned an unsupported tool call. Function tools are required.");

            var id = Text(call["id"]);
            if (ollama && string.IsNullOrWhiteSpace(id)) id = "ollama_" + Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                throw new AiProviderException("The AI endpoint returned missing or duplicate tool-call IDs.");
            var name = Text(function["name"]);
            if (string.IsNullOrWhiteSpace(name))
                throw new AiProviderException("The AI endpoint returned a tool call without a function name.");

            var parsed = new AiToolCall { Id = id, Name = name, ExtraContent = call["extra_content"] is JObject extra ? (JObject)extra.DeepClone() : null };
            var arguments = function["arguments"];
            if (arguments is JObject argumentsObject)
            {
                parsed.Arguments = (JObject)argumentsObject.DeepClone();
                parsed.RawArguments = argumentsObject.ToString(Formatting.None);
            }
            else if (arguments?.Type == JTokenType.String)
            {
                parsed.RawArguments = arguments.Value<string>() ?? "";
                try
                {
                    using var reader = new JsonTextReader(new StringReader(parsed.RawArguments))
                    {
                        MaxDepth = 32,
                        DateParseHandling = DateParseHandling.None
                    };
                    parsed.Arguments = JObject.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                    });
                    if (reader.Read()) throw new JsonReaderException();
                }
                catch (JsonException)
                {
                    parsed.Arguments = new JObject();
                    parsed.ArgumentsError = "Tool arguments must be one valid JSON object with unique property names. The tool was not executed.";
                }
            }
            else
            {
                parsed.RawArguments = arguments?.ToString(Formatting.None) ?? "";
                parsed.ArgumentsError = "Tool arguments must be a JSON object. The tool was not executed.";
            }
            result.Add(parsed);
        }
        return result;
    }

    public static void RequireModel(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Choose or enter a model name before sending a message.");
    }

    public static void ValidateRole(string role)
    {
        if (role is not ("system" or "user" or "assistant" or "tool"))
            throw new ArgumentException("Unsupported chat message role.");
    }
}
