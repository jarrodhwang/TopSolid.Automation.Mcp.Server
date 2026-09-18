using System.Diagnostics;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

internal static class ActiveDocumentContext
{
    internal const string Tool = "topsolid_get_active_document";
    // Resolve only an explicit active-document modeling request. Named/PDM
    // destinations continue through their existing exact lookup workflows.
    internal static bool Requested(string text)
    {
        bool Has(string pattern) => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return !Has(@"\b(project|library|named|called)\b|프로젝트|라이브러리")
            && Has(@"\b(?:current|active|currently)\s+(?:open(?:ed)?\s+)?(?:part|document|file)\b|현재\s*(?:열린|열려\s*있는|활성)?\s*(?:파트|부품|문서)")
            && Has(@"\b(create|make|draw|extrud\w*|revolv\w*|colou?r\w*|modify|model)\b|만들|생성|그려|돌출|회전|색상|빨간");
    }
    internal static async Task<IReadOnlyList<AiMessage>> Fetch(IMcpClient mcp, Action<ChatTrace> trace, CancellationToken token)
    {
        var timer = Stopwatch.StartNew();
        trace(new ChatTrace("Tool call", Tool + " {} (Studio context lookup for the explicitly requested active document)"));
        var receipt = await mcp.CallToolAsync(Tool, new JObject(), token);
        var data = JsonConvert.SerializeObject(receipt);
        trace(new ChatTrace(receipt.IsError ? "Tool error" : "Tool result", data));
        trace(new ChatTrace("Timing", $"Active document context: {timer.Elapsed.TotalSeconds:F2} s; no model round or confirmation"));
        var callId = "studio_context_" + Guid.NewGuid().ToString("N");
        // Record the actual client-initiated call as a normal paired tool exchange.
        // Keeping data in a tool message also prevents promoting document names
        // into system instructions and preserves the receipt in conversation logs.
        return [new AiMessage { Role = "assistant", Content = "Studio is looking up the explicitly requested active document.",
                    ToolCalls = [new AiToolCall { Id = callId, Name = Tool, ClientInitiated = true }] },
            new AiMessage { Role = "tool", ToolCallId = callId, ToolName = Tool, Content = ToolResultContext.Serialize(data.Length > 8000 ? McpToolResult.Error("Active document context exceeded the result limit.") : receipt) }];
    }
}
