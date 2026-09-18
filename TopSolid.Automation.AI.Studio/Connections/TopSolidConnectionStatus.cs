using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Connections;

internal static class TopSolidConnectionStatus
{
    public static bool IsConnected(McpToolResult result)
    {
        if (result.IsError) return false;
        // Structured status is authoritative when provided. Older servers send JSON text.
        if (result.StructuredContent != null) return Connected(result.StructuredContent);
        foreach (var block in result.Content.OfType<JObject>())
        {
            if ((string?)block["type"] != "text" || block["text"]?.Type != JTokenType.String) continue;
            try
            {
                var status = JObject.Parse((string)block["text"]!);
                if (status.ContainsKey("connected")) return Connected(status);
            }
            catch (JsonException) { /* Non-JSON diagnostic text is not connection evidence. */ }
        }
        return false;
    }

    private static bool Connected(JObject status) =>
        status["connected"]?.Type == JTokenType.Boolean && status["connected"]!.Value<bool>();
}
