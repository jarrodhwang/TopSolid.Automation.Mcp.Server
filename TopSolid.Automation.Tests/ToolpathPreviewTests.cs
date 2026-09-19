using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class ToolpathPreviewTests
{
    internal static async Task Run()
    {
        var row = JObject.Parse("{operation:{element:{documentId:'cam-doc',id:11064}},operationName:'[2: Sweeping]'}");
        var mcp = new FakeMcpClient { Tools = [new McpToolDefinition { Name = "topsolid_list_cam_operation_summaries",
            Annotations = new JObject { ["readOnlyHint"] = true } }] };
        mcp.OnCall = _ => Task.FromResult(new McpToolResult { StructuredContent = new JObject
        { ["items"] = new JArray(row.DeepClone()), ["offset"] = 0, ["total"] = 1, ["hasMore"] = false } });
        using var model = new FakeAiProvider(); var shown = new List<JObject>();
        var session = new ChatSession(model, mcp)
        { ShowGraphicPreviewAsync = (target, _) => { shown.Add(target); return Task.CompletedTask; } };
        var answer = await session.SendAsync("현재 CAM 문서의 2번 작업 툴패스를 보여줘", CancellationToken.None);
        Check.True(answer.Contains("preview", StringComparison.OrdinalIgnoreCase) && shown.Count == 1 && model.CompletionCount == 0,
            "Explicit operation toolpath was returned as a prose table or sent to the model");
        Check.Equal(11064, (int)shown[0]["operation"]!["id"]!, "Toolpath preview used the wrong operation identity");

        mcp.Calls.Clear(); shown.Clear();
        var selected = 0;
        session = new ChatSession(new FakeAiProvider(), mcp)
        {
            AskUserAsync = (question, _) =>
            {
                selected++;
                Check.Equal("operation", question.Choices[0].Kind, "Unqualified toolpath picker lost operation identity");
                return Task.FromResult<QuestionAnswer?>(question.Answer(selectedKeys: [question.Choices[0].Key]));
            },
            ShowGraphicPreviewAsync = (target, _) => { shown.Add(target); return Task.CompletedTask; }
        };
        await session.SendAsync("현재 CAM 문서의 툴패스를 보여줘", CancellationToken.None);
        Check.True(selected == 1 && shown.Count == 1 && mcp.Calls.Count == 1, "Unqualified toolpath did not open an operation picker");
        Check.True(!ToolpathPreviewRequest.Matches("현재 CAM 문서의 2번 작업 툴패스를 수정해줘"), "Toolpath preview shortcut swallowed a mutation");
    }
}
