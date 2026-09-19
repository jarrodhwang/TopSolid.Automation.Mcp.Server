using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

// A read-only UI command, recognized only in the current typed request.
// A model response or an attachment can never trigger this path.
internal static class CamSelectionRequest
{
    internal const string Tool = "topsolid_list_cam_operation_summaries";
    internal static bool Matches(string text)
    {
        bool Has(string pattern) => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var withoutReadOnly = Regex.Replace(text,
            @"(?:아무것도\s*|문서는?\s*)?변경(?:하지\s*마|\s*없이)|\b(?:do not|don't)\s+(?:change|modify)\b|\bwithout\s+(?:changes|modifying)\b", "", RegexOptions.IgnoreCase);
        return Has(@"가공\s*작업|스위핑\s*작업|환경\s*활성\s*작업|\b(?:CAM|machining)\s+operations?\b") &&
            Has(@"선택|대화\s*상자|다이(?:얼|아)?로그|\b(?:select|choose|pick|dialog)\b") &&
            !Regex.IsMatch(withoutReadOnly, @"변경|수정|삭제|생성|저장|이송|피드|절삭|파라미터|매개변수|\b(?:change|modify|edit|delete|create|save|feed|parameter|cutting)\b", RegexOptions.IgnoreCase) &&
            !Has(@"선택\s*(?:하지\s*마|없이)|\b(?:do not|don't)\s+(?:select|show)|다른\s*문서|프로젝트|라이브러리|\b(?:project|library|named|called|explain)\b|설명해");
    }

    internal static async Task<IReadOnlyList<AiMessage>?> Run(string text, IMcpClient mcp,
        Func<UserQuestion, CancellationToken, Task<QuestionAnswer?>>? ask, Action<ChatTrace> trace, CancellationToken token)
    {
        if (!Matches(text)) return null;
        var turn = new List<AiMessage> { new() { Role = "user", Content = text } };
        IReadOnlyList<AiMessage> Finish(string key, params object[] args)
        { turn.Add(new AiMessage { Role = "assistant", Content = StudioStrings.Get(key, args) }); return turn; }
        if (ask == null || !mcp.IsConnected || !mcp.Tools.Any(t => t.Name == Tool && !t.RequiresConfirmation))
            return Finish("Cam.Unavailable");

        var sources = new QuestionSources();
        var references = new JArray();
        var rows = new List<(JObject Row, string Source, int Index)>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        string? document = null;
        int? total = null;
        var offset = 0;
        for (var page = 0; page < 24; page++)
        {
            token.ThrowIfCancellationRequested();
            var args = new JObject { ["offset"] = offset, ["limit"] = 20 };
            if (document != null) args["documentId"] = document;
            var id = "cam-select-" + Guid.NewGuid().ToString("N");
            turn.Add(new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = id, Name = Tool, Arguments = args, ClientInitiated = true }] });
            trace(new ChatTrace("Tool call", Tool + " " + args.ToString(Formatting.None)));
            var timer = System.Diagnostics.Stopwatch.StartNew();
            McpToolResult result;
            try { result = await mcp.CallToolAsync(Tool, args, token); }
            finally { trace(new ChatTrace("Timing", $"Tool {Tool}: {timer.Elapsed.TotalSeconds:F2} s; user confirmation: 0.00 s")); }
            var content = ToolResultContext.Serialize(result);
            trace(new ChatTrace(result.IsError ? "Tool error" : "Tool result", JsonConvert.SerializeObject(result), Tool, (JObject)args.DeepClone()));
            turn.Add(new AiMessage { Role = "tool", ToolCallId = id, ToolName = Tool, Content = content });
            var data = PdmInventory.Data(result);
            if (result.IsError) return Finish("Cam.Unavailable");
            if (content.Length > 64000 || data?["items"] is not JArray items || data["total"]?.Type != JTokenType.Integer ||
                data["offset"]?.Type != JTokenType.Integer || (int?)data["offset"] != offset || data["hasMore"]?.Type != JTokenType.Boolean)
                return Finish("Cam.Incomplete");
            var count = (int)data["total"]!;
            var next = offset + items.Count;
            var more = (bool)data["hasMore"]!;
            if (count < 0 || total.HasValue && count != total || next > count || more != (next < count) ||
                more && (items.Count == 0 || data["nextOffset"]?.Type != JTokenType.Integer || (int?)data["nextOffset"] != next))
                return Finish("Cam.Incomplete");
            total = count;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] is not JObject row || row["operation"] is not JObject identity) return Finish("Cam.Incomplete");
                var handle = identity["element"] as JObject ?? identity;
                var rowDocument = (string?)handle["documentId"];
                if (string.IsNullOrWhiteSpace(rowDocument) || handle["id"] == null ||
                    document != null && document != rowDocument || !identities.Add(identity.ToString(Formatting.None))) return Finish("Cam.Incomplete");
                document = rowDocument;
                rows.Add((row, id, i));
            }
            sources.Capture(id, Tool, args, result);
            references.Add(new JObject { ["toolCallId"] = id, ["path"] = "/items" });
            if (!more) break;
            if (page == 23) return Finish("Cam.Incomplete");
            offset = next;
        }

        // Numbers are matched to the native displayed operation number, never an element ID.
        var numbers = new HashSet<int>();
        foreach (Match number in Regex.Matches(text, @"(?<!\d)(\d+)\s*번"))
        {
            if (!int.TryParse(number.Groups[1].Value, out var value)) return Finish("Cam.NoMatch");
            numbers.Add(value);
        }
        var type = Regex.IsMatch(text, @"환경\s*활성|\benvironment\b", RegexOptions.IgnoreCase) ? "TopSolid.Cam.NC.Kernel.DB.Annex.Operation.EnvironmentOperation" :
            Regex.IsMatch(text, @"스위핑|\bsweeping\b", RegexOptions.IgnoreCase) ? "TopSolid.Cam.NC.MillTurn.Form.DB.Sweeping.Operation.SweepingOperation" :
            Regex.IsMatch(text, @"사이드\s*밀링|\bside\s*milling\b", RegexOptions.IgnoreCase) ? "TopSolid.Cam.NC.MillTurn.DB.SideMilling.SideMillingOperation" : null;
        if (numbers.Count > 0 || type != null)
        {
            var matched = rows.Where(r =>
            {
                var match = Regex.Match((string?)r.Row["operationName"] ?? "", @"^\s*\[?(\d+)\s*:");
                return (numbers.Count == 0 || match.Success && int.TryParse(match.Groups[1].Value, out var value) && numbers.Contains(value)) &&
                    (type == null || (string?)r.Row["operationType"] == type);
            }).ToArray();
            if (matched.Length == 0 || numbers.Count > 0 && matched.Length != numbers.Count) return Finish("Cam.NoMatch");
            if (matched.Length > 24) return Finish("Cam.Incomplete");
            references = new JArray(matched.Select(r => new JObject { ["toolCallId"] = r.Source, ["path"] = "/items/" + r.Index }));
        }
        if (rows.Count == 0) return Finish("Cam.NoMatch");
        var input = new JObject { ["question"] = StudioStrings.Get("Cam.Select"), ["kind"] = "select", ["itemKind"] = "operation",
            ["multiple"] = true, ["sources"] = references };
        var question = sources.Create(input);
        var questionId = "cam-question-" + Guid.NewGuid().ToString("N");
        turn.Add(new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = questionId, Name = QuestionSources.ToolName, Arguments = input, ClientInitiated = true }] });
        trace(new ChatTrace("Tool call", QuestionSources.ToolName + " " + input.ToString(Formatting.None)));
        var answer = await ask(question, token);
        token.ThrowIfCancellationRequested();
        var receipt = new McpToolResult { StructuredContent = answer?.Data ?? new JObject { ["status"] = "cancelled" } };
        var receiptText = ToolResultContext.Serialize(receipt);
        turn.Add(new AiMessage { Role = "tool", ToolCallId = questionId, ToolName = QuestionSources.ToolName, Content = receiptText });
        trace(new ChatTrace("Tool result", receiptText));
        return answer == null ? Finish("Cam.Cancelled") : Finish("Cam.Selected", answer.Summary);
    }
}
