using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;

namespace TopSolid.Automation.AI.Studio.Chat;

/// <summary>Resolve an explicit active-document preview locally; the language model
/// cannot hide an available native viewport by claiming it only supports text.</summary>
internal static class GraphicPreviewRequest
{
    internal static bool Matches(string text)
    {
        var value = text.Trim();
        var mutation = Regex.IsMatch(value, @"\b(?:delete|remove|change|modify|create|make|edit)\b|삭제|변경|수정|생성|만들|편집", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var readOnly = Regex.IsMatch(value, @"수정\s*하지|변경\s*하지|삭제\s*하지|생성\s*하지|만들지|편집\s*하지|\b(?:do\s+not|don't|without|read[- ]only)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (mutation && !readOnly) return false;
        if (Regex.IsMatch(value, @"\b(?:named|called)\b|이름이|\bproject\b|프로젝트|라이브러리", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
        if (Regex.IsMatch(value, @"공구|\btools?\b|\bcutter\b|\bT\s*\d+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
        return Regex.IsMatch(value,
            @"(?:show|open|display|view|preview).{0,80}(?:graphical|3d|geometry|shape|model|part|document)\s*(?:preview|view)?|(?:3d|graphical)?\s*(?:preview|미리보기|프리뷰).{0,80}(?:active|current|open|part|document|geometry|shape|model)|(?:현재|활성|열린)\s*(?:탑솔리드|TopSolid)?\s*(?:파트|부품|문서|형상|모델)?[^.!?\r\n]{0,40}(?:3d\s*)?(?:프리뷰|미리보기|미리\s*보여)|(?:현재|활성|열린)[^.!?\r\n]{0,80}(?:형상|모델|부품)[^.!?\r\n]{0,30}(?:3d\s*)?(?:프리뷰|미리보기|미리\s*보여)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    internal static async Task<IReadOnlyList<AiMessage>?> Run(string text, IMcpClient mcp,
        Func<JObject, CancellationToken, Task>? show, Action<ChatTrace> trace, CancellationToken token)
    {
        const string tool = "topsolid_get_active_document";
        if (!Matches(text) || show == null || !mcp.IsConnected || !mcp.Tools.Any(t => t.Name == tool && !t.RequiresConfirmation)) return null;
        var args = new JObject(); var callId = "preview-" + Guid.NewGuid().ToString("N");
        trace(new("Tool call", tool + " {}"));
        var result = await mcp.CallToolAsync(tool, args, token);
        trace(new(result.IsError ? "Tool error" : "Tool result", JsonConvert.SerializeObject(result), tool, args));
        JObject? data = result.IsError ? null : result.StructuredContent as JObject;
        if (data == null && !result.IsError && result.Content.Count == 1 && result.Content[0] is JObject block &&
            (string?)block["type"] == "text" && block["text"]?.Type == JTokenType.String)
        {
            try { data = JToken.Parse((string)block["text"]!) as JObject; }
            catch (JsonException) { }
        }
        var document = data?["document"] as JObject;
        var shown = document?["documentId"]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string?)document["documentId"]);
        if (shown) await show!(new JObject { ["documentId"] = document!["documentId"]!.DeepClone() }, token);
        return [new() { Role = "user", Content = text },
            new() { Role = "assistant", ToolCalls = [new() { Id = callId, Name = tool, Arguments = args, ClientInitiated = true }] },
            new() { Role = "tool", ToolCallId = callId, ToolName = tool, Content = ToolResultContext.Serialize(result) },
            new() { Role = "assistant", Content = StudioStrings.Get(shown ? "Preview.Shown" : "Preview.unavailable") }];
    }
}
