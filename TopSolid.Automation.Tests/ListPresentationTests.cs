using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class ListPresentationTests
{
    internal static async Task Run()
    {
        foreach (var text in new[] { "현재 가공 도큐먼트의 오퍼레이션 리스트 보여줘", "가공 오퍼레이션 목록", "list machining operations" })
            Check.True(ListPresentation.DirectTools(text).SequenceEqual(new[] { CamSelectionRequest.Tool }), "Ordinary operation list still requires a dialog keyword: " + text);
        foreach (var text in new[] { "프로젝트 A의 파라미터 리스트", "오퍼레이션 2번 변경하고 목록 보여줘", "delete all operations and list them" })
            Check.True(ListPresentation.DirectTools(text).Length == 0, "Qualified or mutating intent was replaced by an unqualified browse");
        var mcp = new FakeMcpClient { Tools = [new() { Name = CamSelectionRequest.Tool, Annotations = new JObject { ["readOnlyHint"] = true },
            InputSchema = JObject.Parse("{type:'object',properties:{documentId:{type:'string'}}}") }] };
        mcp.OnCall = _ => Task.FromResult(new McpToolResult { StructuredContent = Page(mcp.Calls.Count - 1) });
        using var model = new FakeAiProvider();
        var shown = 0;
        var session = new ChatSession(model, mcp) { ShowListAsync = async (q, token) =>
        {
            shown++; Check.True(q.IsBrowse && !q.Multiple && q.HasMore && q.Choices.Count == 1, "List is not a paged read-only browser");
            Check.Equal("op-doc", (string)q.DocumentPreview!["documentId"]!, "Default CAM model is missing");
            Check.Equal(11, (int)q.PreviewTargetFor(q.Choices[0].Key)!["operation"]!["id"]!, "Clicked operation identity was discarded");
            var next = await q.LoadMoreAsync!(token);
            Check.True(!next.HasMore && next.Choices.Count == 2, "Next page was lost");
            Check.Equal("op-doc", (string)mcp.Calls[1].Arguments["documentId"]!, "Paging followed a different active document");
        } };
        var result = await session.SendAsync("현재 가공 도큐먼트의 오퍼레이션 리스트 보여줘", CancellationToken.None);
        Check.True(shown == 1 && model.CompletionCount == 0 && !result.Contains("cutting-a"), "List was restated by a model or not displayed");
        Check.True(!result.Contains("cancel", StringComparison.OrdinalIgnoreCase), "Closing browse was recorded as cancellation");

        // Filtered requests still let the model resolve the scope, but presentation is deterministic.
        mcp.Calls.Clear(); mcp.OnCall = _ => Task.FromResult(new McpToolResult { StructuredContent = Page(0, more: false) });
        model.Reply = (round, _, _) => Task.FromResult(round == 1 ? FakeAiProvider.ToolReply(new AiToolCall { Id = "queried", Name = CamSelectionRequest.Tool,
            Arguments = new JObject { ["documentId"] = "op-doc", ["offset"] = 0 } }) : new AiReply { Content = "text list that should never be displayed" });
        session = new ChatSession(model, mcp) { ShowListAsync = (q, _) => { shown++; Check.True(q.IsBrowse, "Fallback did not browse"); return Task.CompletedTask; } };
        result = await session.SendAsync("블레이드 문서의 가공 오퍼레이션 목록", CancellationToken.None);
        Check.True(shown == 2 && !result.Contains("text list"), "Tool-backed list was returned as prose");
        var source = new ListPresentation(); source.Capture(CamSelectionRequest.Tool, new JObject(), McpToolResult.Error("failed"));
        Check.True(!await source.ShowAsync(mcp, (_, _) => throw new Exception("Failed read opened UI"), _ => { }, CancellationToken.None), "Failed result was displayable");
        source.Capture(CamSelectionRequest.Tool, new JObject(), new McpToolResult { StructuredContent = new JObject { ["items"] = new JArray(), ["total"] = 0, ["offset"] = 0, ["hasMore"] = false } });
        Check.True(source.IsEmpty, "Empty list was not classified as empty");
        Check.True(!await source.ShowAsync(mcp, (_, _) => throw new Exception("Empty list opened UI"), _ => { }, CancellationToken.None), "Empty list opened a blank dialog");
    }
    internal static JObject Page(int offset, bool more = true) => new() { ["items"] = new JArray(new JObject {
        ["operation"] = new JObject { ["element"] = new JObject { ["documentId"] = "op-doc", ["id"] = 11 + offset } },
        ["operationName"] = offset == 0 ? "cutting-a" : "cutting-b", ["operationType"] = "TopSolid.Cam.NC.MillTurn.DB.SideMilling.SideMillingOperation" }),
        ["total"] = more ? 2 : 1, ["offset"] = offset, ["hasMore"] = more && offset == 0, ["nextOffset"] = more && offset == 0 ? 1 : JValue.CreateNull() };
}
