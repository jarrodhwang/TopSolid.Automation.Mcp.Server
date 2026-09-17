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
        if (result.StructuredContent == null) return JsonConvert.SerializeObject(result);
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
