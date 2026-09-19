using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

/// <summary>Resolves an operation from a live CAM list before opening the read-only toolpath preview.</summary>
internal static class ToolpathPreviewRequest
{
    private const string PrimaryTool = "topsolid_list_cam_operation_summaries";
    private static readonly Regex Number = new(
        @"(?<!\d)(?:([0-9]+)\s*번|(?:operation|op)\s*#?\s*([0-9]+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool Matches(string text)
    {
        var hasPath = Regex.IsMatch(text, @"툴\s*패스|tool\s*path|toolpath", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var hasOperation = Regex.IsMatch(text, @"CAM|가공|작업|오퍼레이션|operation|machining", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var mutation = Regex.IsMatch(text, @"수정|변경|삭제|생성|저장|편집|\b(?:change|modify|delete|create|save|edit|apply)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return hasPath && hasOperation && !mutation;
    }

    internal static async Task<IReadOnlyList<AiMessage>?> Run(string text, IMcpClient mcp,
        Func<UserQuestion, CancellationToken, Task<QuestionAnswer?>>? ask,
        Func<JObject, CancellationToken, Task>? show, Action<ChatTrace> trace, CancellationToken token)
    {
        if (!Matches(text)) return null;
        var turn = new List<AiMessage> { new() { Role = "user", Content = text } };
        IReadOnlyList<AiMessage> Finish(string key, params object[] args)
        { turn.Add(new AiMessage { Role = "assistant", Content = StudioStrings.Get(key, args) }); return turn; }

        if (show == null || !mcp.IsConnected) return Finish("Preview.unavailable");
        var tool = mcp.Tools.FirstOrDefault(t => t.Name == PrimaryTool && !t.RequiresConfirmation)?.Name ??
            mcp.Tools.FirstOrDefault(t => t.Name == "topsolid_list_cam_operations" && !t.RequiresConfirmation)?.Name;
        if (tool == null) return Finish("Preview.unavailable");

        var browser = new ListPresentation();
        var requestedNumber = Number.Match(text);
        var number = requestedNumber.Success && int.TryParse(requestedNumber.Groups[1].Success ? requestedNumber.Groups[1].Value : requestedNumber.Groups[2].Value, out var parsed)
            ? parsed : (int?)null;
        var matches = new List<(JObject Row, JObject Arguments)>();
        var offset = 0;
        for (var page = 0; page < 24; page++)
        {
            token.ThrowIfCancellationRequested();
            var args = new JObject { ["offset"] = offset, ["limit"] = 100 };
            var id = "toolpath-list-" + Guid.NewGuid().ToString("N");
            turn.Add(new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = id, Name = tool, Arguments = args, ClientInitiated = true }] });
            trace(new ChatTrace("Tool call", tool + " " + args.ToString(Formatting.None), tool, args));
            var result = await mcp.CallToolAsync(tool, args, token);
            trace(new ChatTrace(result.IsError ? "Tool error" : "Tool result", JsonConvert.SerializeObject(result), tool, args));
            turn.Add(new AiMessage { Role = "tool", ToolCallId = id, ToolName = tool, Content = ToolResultContext.Serialize(result) });
            if (result.IsError) return Finish("Preview.unavailable");
            browser.Capture(tool, args, result);
            if (browser.IsEmpty) return Finish("Question.EmptySelection");
            var data = PdmInventory.Data(result);
            if (data?["items"] is not JArray items || (int?)data["offset"] != offset) return Finish("Preview.unavailable");
            if (number.HasValue)
            {
                foreach (var item in items.OfType<JObject>())
                {
                    var name = (string?)item["operationName"] ?? (string?)item["name"] ?? "";
                    var operationNumber = Regex.Match(name, @"^\s*\[?([0-9]+)\s*:", RegexOptions.CultureInvariant);
                    if (operationNumber.Success && int.TryParse(operationNumber.Groups[1].Value, out var value) && value == number.Value)
                        matches.Add((item, (JObject)args.DeepClone()));
                }
            }
            var more = (bool?)data["hasMore"] == true;
            if (!more || number == null || matches.Count > 1) break;
            var next = (int?)data["nextOffset"] ?? offset + items.Count;
            if (next <= offset || items.Count == 0) return Finish("Preview.unavailable");
            offset = next;
        }

        JObject? target;
        if (number.HasValue)
        {
            if (matches.Count != 1) return Finish("Cam.NoMatch");
            target = Preview.PreviewTarget.FromChoice(new JObject { ["sourceTool"] = tool,
                ["sourceArguments"] = matches[0].Arguments.DeepClone(), ["value"] = matches[0].Row.DeepClone() }, "operation");
        }
        else
        {
            if (ask == null) return Finish("Question.Unavailable");
            var title = StudioStrings.Get("Cam.Select");
            var answer = await browser.AskSelectionAsync(mcp, ask, title, false, trace, token);
            token.ThrowIfCancellationRequested();
            var questionId = "toolpath-question-" + Guid.NewGuid().ToString("N");
            var questionInput = new JObject { ["question"] = title, ["kind"] = "select", ["itemKind"] = "operation" };
            turn.Add(new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = questionId, Name = QuestionSources.ToolName, Arguments = questionInput, ClientInitiated = true }] });
            trace(new ChatTrace("Tool call", QuestionSources.ToolName + " " + questionInput.ToString(Formatting.None), QuestionSources.ToolName, questionInput));
            var receipt = new McpToolResult { StructuredContent = answer?.Data ?? new JObject { ["status"] = "cancelled" } };
            var receiptText = ToolResultContext.Serialize(receipt);
            turn.Add(new AiMessage { Role = "tool", ToolCallId = questionId, ToolName = QuestionSources.ToolName, Content = receiptText });
            trace(new ChatTrace("Tool result", receiptText, QuestionSources.ToolName, questionInput));
            if (answer == null) return Finish("Cam.Cancelled");
            var selected = answer.Data["selected"] as JArray;
            target = selected?.Count == 1 && selected[0] is JObject choice ? Preview.PreviewTarget.FromChoice(choice, "operation") : null;
        }

        if (target == null) return Finish("Preview.unavailable");
        await show(target, token);
        turn.Add(new AiMessage { Role = "assistant", Content = StudioStrings.Get("Preview.Shown") });
        trace(new ChatTrace("Preview", "Opened the selected CAM operation toolpath preview."));
        return turn;
    }
}
