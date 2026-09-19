using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class SelectionRequestTests
{
    internal static async Task Run()
    {
        var cases = new[]
        {
            ("프로젝트를 선택해줘", "topsolid_list_projects", JObject.Parse("{pdmObjectId:'p1',name:'Project A'}")),
            ("문서를 선택해줘", "topsolid_list_document_summaries", JObject.Parse("{documentId:'d1',name:'Part A',extension:'.TopPrt'}")),
            ("오퍼레이션을 선택해줘", "topsolid_list_cam_operation_summaries", JObject.Parse("{operation:{element:{documentId:'d1',id:11}},operationName:'[1: Roughing]'}")),
            ("공구를 선택해줘", "topsolid_list_cam_tools", JObject.Parse("{id:1,name:'T1',toolDisplayName:'T 1 : End Mill D10'}")),
            ("스케치를 선택해줘", "topsolid_list_sketches2d", JObject.Parse("{element:{documentId:'d1',id:2},name:'Profile'}"))
        };

        foreach (var (request, tool, row) in cases)
        {
            var mcp = new FakeMcpClient { Tools = [new McpToolDefinition { Name = tool,
                Annotations = new JObject { ["readOnlyHint"] = true } }] };
            mcp.OnCall = _ => Task.FromResult(new McpToolResult { StructuredContent = new JObject
            { ["items"] = new JArray(row.DeepClone()), ["offset"] = 0, ["total"] = 1, ["hasMore"] = false } });
            using var model = new FakeAiProvider();
            var dialogCount = 0;
            var session = new ChatSession(model, mcp)
            {
                AskUserAsync = (question, _) =>
                {
                    dialogCount++;
                    Check.Equal(1, question.Choices.Count, "Selection dialog did not use the verified receipt: " + request);
                    return Task.FromResult<QuestionAnswer?>(question.Answer(selectedKeys: [question.Choices[0].Key]));
                }
            };

            var answer = await session.SendAsync(request, CancellationToken.None);
            Check.True(dialogCount == 1 && model.CompletionCount == 0 && answer.StartsWith("Selected:", StringComparison.Ordinal),
                "Explicit object selection did not open a native dialog without inference: " + request);
            Check.Equal(tool, mcp.Calls.Single().Name, "Selection dispatched an unrelated list tool: " + request);
            Check.True(session.GetConversationSnapshot().Single().Any(m => m.ToolName == QuestionSources.ToolName),
                "Selection receipt was not retained: " + request);
        }
    }
}
