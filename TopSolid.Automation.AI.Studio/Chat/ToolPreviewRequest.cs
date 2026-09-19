using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Preview;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

/// <summary>Resolves a CAM tool through the native picker before opening its definition preview.</summary>
internal static class ToolPreviewRequest
{
    internal const string Tool = "topsolid_list_cam_tools";

    internal static bool Matches(string text)
    {
        var hasTool = Regex.IsMatch(text, @"공구|\btools?\b|\bcutter\b|\bT\s*\d+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var hasPreview = Regex.IsMatch(text, @"3\s*d|프리뷰|미리보기|preview|show", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var hasSelection = Regex.IsMatch(text, @"선택|고르|대화\s*상자|다이(?:얼|아)?로그|\bselect|\bchoose|\bpick", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var hasMutation = Regex.IsMatch(text, @"수정|변경|삭제|생성|저장|\b(?:change|modify|delete|create|save|edit)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return hasTool && hasPreview && hasSelection && !hasMutation;
    }

    internal static async Task<IReadOnlyList<AiMessage>?> Run(string text, IMcpClient mcp,
        Func<UserQuestion, CancellationToken, Task<QuestionAnswer?>>? ask,
        Func<JObject, CancellationToken, Task>? show,
        Action<ChatTrace> trace, CancellationToken token)
    {
        if (!Matches(text)) return null;
        var turn = new List<AiMessage> { new() { Role = "user", Content = text } };
        IReadOnlyList<AiMessage> Finish(string key, params object[] args)
        { turn.Add(new AiMessage { Role = "assistant", Content = StudioStrings.Get(key, args) }); return turn; }

        if (ask == null || show == null || !mcp.IsConnected || !mcp.Tools.Any(t => t.Name == Tool && !t.RequiresConfirmation))
            return Finish("Preview.unavailable");

        var browser = new ListPresentation();
        var args = new JObject { ["offset"] = 0, ["limit"] = 20 };
        var id = "tool-preview-list-" + Guid.NewGuid().ToString("N");
        turn.Add(new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = id, Name = Tool, Arguments = args, ClientInitiated = true }] });
        trace(new ChatTrace("Tool call", Tool + " " + args.ToString(Formatting.None), Tool, args));
        var result = await mcp.CallToolAsync(Tool, args, token);
        trace(new ChatTrace(result.IsError ? "Tool error" : "Tool result", JsonConvert.SerializeObject(result), Tool, args));
        turn.Add(new AiMessage { Role = "tool", ToolCallId = id, ToolName = Tool, Content = ToolResultContext.Serialize(result) });
        browser.Capture(Tool, args, result);
        if (result.IsError) return Finish("Preview.unavailable");
        if (browser.IsEmpty) return Finish("Question.EmptySelection");

        var title = StudioStrings.Get("Selection.ToolPreview");
        var answer = await browser.AskSelectionAsync(mcp, ask, title, false, trace, token);
        token.ThrowIfCancellationRequested();
        var questionId = "tool-preview-question-" + Guid.NewGuid().ToString("N");
        var questionInput = new JObject { ["question"] = title, ["kind"] = "select", ["itemKind"] = "tool" };
        turn.Add(new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = questionId, Name = QuestionSources.ToolName, Arguments = questionInput, ClientInitiated = true }] });
        trace(new ChatTrace("Tool call", QuestionSources.ToolName + " " + questionInput.ToString(Formatting.None), QuestionSources.ToolName, questionInput));
        var questionReceipt = new McpToolResult { StructuredContent = answer?.Data ?? new JObject { ["status"] = "cancelled" } };
        var questionText = ToolResultContext.Serialize(questionReceipt);
        turn.Add(new AiMessage { Role = "tool", ToolCallId = questionId, ToolName = QuestionSources.ToolName, Content = questionText });
        trace(new ChatTrace("Tool result", questionText, QuestionSources.ToolName, questionInput));
        if (answer == null) return Finish("Selection.Cancelled");

        var selected = answer.Data["selected"] as JArray;
        var target = selected?.Count == 1 && selected[0] is JObject choice ? PreviewTarget.FromChoice(choice, "tool") : null;
        if (target == null) return Finish("Preview.unavailable");
        await show(target, token);
        turn.Add(new AiMessage { Role = "assistant", Content = StudioStrings.Get("Preview.Shown") });
        trace(new ChatTrace("Preview", "Opened the selected tool definition document preview."));
        return turn;
    }
}
