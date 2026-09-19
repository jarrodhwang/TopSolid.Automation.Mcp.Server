using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

/// <summary>
/// Handles an explicit object-picker request before model inference. A picker
/// request is presentation and read-only; the returned receipt is still only a
/// target selection and never authorizes a CAD change.
/// </summary>
internal static class SelectionRequest
{
    private static readonly Regex SelectionWords = new(
        @"선택|고르|대화\s*상자|다이(?:얼|아)?로그|\b(?:select|choose|pick|picker|selection\s+dialog)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MutationWords = new(
        @"수정|변경|삭제|생성|만들|저장|실행|편집|\b(?:change|modify|delete|create|make|save|edit|execute|apply)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PickerOutcome = new(
        @"대화\s*상자|다이(?:얼|아)?로그|보여|알려|미리|열어|사용|위해|선택\s*(?:해|하고|해서|줘|주세요|하세요)|고르\s*(?:해|하고|해서|줘|주세요|세요)|\b(?:dialog|preview|show|display|open|view|use|for|to|then|and)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool RequiresDialog(string text)
    {
        if (!SelectionWords.IsMatch(text)) return false;
        if (MutationWords.IsMatch(text)) return true;
        return HasKind(text);
    }

    internal static bool Matches(string text) => SelectionWords.IsMatch(text) && HasKind(text) &&
        !MutationWords.IsMatch(text) && PickerOutcome.IsMatch(text);

    internal static async Task<IReadOnlyList<AiMessage>?> Run(string text, IMcpClient mcp,
        Func<UserQuestion, CancellationToken, Task<QuestionAnswer?>>? ask,
        Action<ChatTrace> trace, CancellationToken token)
    {
        if (!Matches(text)) return null;
        var turn = new List<AiMessage> { new() { Role = "user", Content = text } };
        IReadOnlyList<AiMessage> Finish(string key, params object[] args)
        { turn.Add(new AiMessage { Role = "assistant", Content = StudioStrings.Get(key, args) }); return turn; }

        if (!mcp.IsConnected) return null;
        var names = ToolsFor(text, mcp).ToArray();
        if (names.Length == 0) return null;
        if (ask == null) return Finish("Selection.Unavailable");

        var browser = new ListPresentation();
        foreach (var name in names)
        {
            token.ThrowIfCancellationRequested();
            var args = new JObject { ["offset"] = 0, ["limit"] = 20 };
            var id = "selection-" + Guid.NewGuid().ToString("N");
            turn.Add(new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = id, Name = name, Arguments = args, ClientInitiated = true }] });
            trace(new ChatTrace("Tool call", name + " " + args.ToString(Formatting.None), name, args));
            var result = await mcp.CallToolAsync(name, args, token);
            trace(new ChatTrace(result.IsError ? "Tool error" : "Tool result", JsonConvert.SerializeObject(result), name, args));
            turn.Add(new AiMessage { Role = "tool", ToolCallId = id, ToolName = name, Content = ToolResultContext.Serialize(result) });
            browser.Capture(name, args, result);
            if (result.IsError) return Finish("Selection.Unavailable");
        }

        if (browser.IsEmpty) return Finish("Question.EmptySelection");

        var itemKind = SingleKind(text) ?? "option";
        var multiple = Regex.IsMatch(text, @"모두|전체|여러|둘|두\s*개|\ball\b|\bboth\b|\bmultiple\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var title = StudioStrings.Get("Question.Selection");
        var input = new JObject { ["question"] = title, ["kind"] = "select", ["itemKind"] = itemKind, ["multiple"] = multiple };
        var questionId = "selection-question-" + Guid.NewGuid().ToString("N");
        var answer = await browser.AskSelectionAsync(mcp, ask, title, multiple, trace, token);
        token.ThrowIfCancellationRequested();
        turn.Add(new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = questionId, Name = QuestionSources.ToolName, Arguments = input, ClientInitiated = true }] });
        trace(new ChatTrace("Tool call", QuestionSources.ToolName + " " + input.ToString(Formatting.None), QuestionSources.ToolName, input));
        var receipt = new McpToolResult { StructuredContent = answer?.Data ?? new JObject { ["status"] = "cancelled" } };
        var receiptText = ToolResultContext.Serialize(receipt);
        turn.Add(new AiMessage { Role = "tool", ToolCallId = questionId, ToolName = QuestionSources.ToolName, Content = receiptText });
        trace(new ChatTrace("Tool result", receiptText, QuestionSources.ToolName, input));
        return answer == null ? Finish("Selection.Cancelled") : Finish("Selection.Selected", answer.Summary);
    }

    private static IEnumerable<string> ToolsFor(string text, IMcpClient mcp)
    {
        var requested = new List<string>();
        void Add(params string[] names)
        {
            foreach (var name in names)
                if (!requested.Contains(name, StringComparer.Ordinal) && mcp.Tools.Any(t => t.Name == name && !t.RequiresConfirmation)) requested.Add(name);
        }

        if (Has(text, @"프로젝트|\bprojects?\b")) Add("topsolid_list_projects");
        if (Has(text, @"라이브러리|\blibraries?\b")) Add("topsolid_list_libraries");
        if (Has(text, @"문서|도큐먼트|\bdocuments?\b|\bfiles?\b")) Add("topsolid_list_document_summaries", "topsolid_list_documents", "topsolid_list_loaded_documents");
        if (Has(text, @"오퍼레이션|가공\s*작업|작업|\boperations?\b|\bmachining\b")) Add("topsolid_list_cam_operation_summaries", "topsolid_list_cam_operations", "topsolid_list_cam_scenario");
        if (Has(text, @"공구|\btools?\b|cutter")) Add("topsolid_list_cam_tools");
        if (Has(text, @"스케치|\bsketch(?:es)?\b"))
        {
            if (Has(text, @"3\s*d|3차원")) Add("topsolid_list_sketches3d");
            else if (Has(text, @"2\s*d|2차원")) Add("topsolid_list_sketches2d");
            else Add("topsolid_list_sketches2d", "topsolid_list_sketches3d");
        }
        if (Has(text, @"형상|\bshapes?\b|\bgeometry\b")) Add("topsolid_list_shape_summaries");
        if (Has(text, @"요소|엔터티|엔티티|\belements?\b|\bentities\b")) Add("topsolid_list_named_elements");
        return requested;
    }

    private static string? SingleKind(string text)
    {
        var kinds = new[]
        {
            ("project", @"프로젝트|\bprojects?\b"), ("library", @"라이브러리|\blibraries?\b"),
            ("document", @"문서|도큐먼트|\bdocuments?\b|\bfiles?\b"), ("operation", @"오퍼레이션|가공\s*작업|\boperations?\b|\bmachining\b"),
            ("tool", @"공구|\btools?\b|cutter"), ("sketch", @"스케치|\bsketch(?:es)?\b"),
            ("shape", @"형상|\bshapes?\b|\bgeometry\b"), ("element", @"요소|엔터티|엔티티|\belements?\b|\bentities\b")
        };
        var matches = kinds.Where(k => Has(text, k.Item2)).Select(k => k.Item1).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static bool HasKind(string text) => ToolsForKeywords.Any(pattern => Has(text, pattern));
    private static readonly string[] ToolsForKeywords =
    ["프로젝트|\\bprojects?\\b", "라이브러리|\\blibraries?\\b", "문서|도큐먼트|\\bdocuments?\\b|\\bfiles?\\b",
     "오퍼레이션|가공\\s*작업|\\boperations?\\b|\\bmachining\\b", "공구|\\btools?\\b|cutter", "스케치|\\bsketch(?:es)?\\b",
     "형상|\\bshapes?\\b|\\bgeometry\\b", "요소|엔터티|엔티티|\\belements?\\b|\\bentities\\b"];

    private static bool Has(string text, string pattern) => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
