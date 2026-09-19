using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

internal static class ToolResultContext
{
    // MCP permits the same JSON in structuredContent and a text block. Send it
    // once to the model, while diagnostics retain the full transport receipt.
    internal static string Serialize(McpToolResult result)
    {
        if (result.StructuredContent == null)
        {
            // Older server tools return one JSON object inside an escaped text
            // block. Normalize only that unambiguous case for model context.
            // The transport result remains unchanged in diagnostics and the UI.
            if (result.Content.Count == 1 && result.Content[0] is JObject single && (string?)single["type"] == "text" && single["text"]?.Type == JTokenType.String)
            {
                try {
                    if (JToken.Parse((string)single["text"]!) is JObject data)
                        return JsonConvert.SerializeObject(new McpToolResult { IsError = result.IsError, StructuredContent = data, Content = new JArray() });
                }
                catch (JsonException) { }
            }
            return JsonConvert.SerializeObject(result);
        }
        var content = new JArray();
        foreach (var block in result.Content)
        {
            if (block is JObject textBlock && (string?)textBlock["type"] == "text" && (string?)textBlock["text"] is { } text)
            {
                try { if (JToken.DeepEquals(JToken.Parse(text), result.StructuredContent)) continue; }
                catch (JsonException) { /* Non-JSON explanations remain available. */ }
            }
            content.Add(block.DeepClone());
        }
        return JsonConvert.SerializeObject(new McpToolResult { IsError = result.IsError, StructuredContent = result.StructuredContent, Content = content });
    }
}
