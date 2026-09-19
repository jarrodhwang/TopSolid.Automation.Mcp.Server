using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class NcGenerationTests
{
    internal static async Task Run()
    {
        var language = StudioStrings.CurrentLanguage;
        StudioStrings.Apply("ko");
        try
        {
            Check.True(NcGenerationRequest.Matches("NC코드를 생성해줘"), "Korean NC generation request was not recognized");
            Check.True(!NcGenerationRequest.Matches("NC 파일 목록을 보여줘"), "NC list request was incorrectly routed to generation");

            using var model = new FakeAiProvider { Reply = (_, _, _) => throw new Exception("NC workflow must not call the model") };
            var client = new Client();
            var questions = 0;
            var confirmations = 0;
            var destinations = 0;
            var session = new ChatSession(model, client)
            {
                AskUserAsync = (question, _) =>
                {
                    questions++;
                    if (questions == 1)
                    {
                        Check.Equal("operation", question.ItemKind, "Operation dialog was not first");
                        return Task.FromResult<QuestionAnswer?>(question.Answer(selectedKeys: [question.Choices[0].Key]));
                    }
                    Check.Equal("option", question.ItemKind, "Post-processor dialog did not immediately follow operation selection");
                    return Task.FromResult<QuestionAnswer?>(question.Answer(selectedKeys: [question.Choices[0].Key]));
                },
                ConfirmChangeAsync = (proposal, _) =>
                {
                    confirmations++;
                    Check.True((string?)proposal["toolName"] is NcGenerationRequest.GenerateTool or NcGenerationRequest.ExportTool,
                        "Unexpected NC confirmation");
                    return Task.FromResult(true);
                },
                ChooseNcDestinationAsync = (file, _) =>
                {
                    destinations++;
                    Check.Equal("part.nc", (string?)file["suggestedFileName"], "Generated NC filename was not carried to the save dialog");
                    return Task.FromResult<string?>("C:\\Temp\\part.nc");
                }
            };
            var answer = await session.SendAsync("NC코드를 생성해줘", CancellationToken.None);
            Check.True(answer.Contains("생성하고 저장했습니다"), "NC workflow did not report generation and export success: " + answer);
            Check.Equal(2, questions, "NC workflow did not open exactly operation and post-processor dialogs");
            Check.Equal(2, confirmations, "NC workflow did not confirm generation and export separately");
            Check.Equal(1, destinations, "NC workflow did not show one save destination dialog");
            Check.True(client.Calls.SequenceEqual(new[] { NcGenerationRequest.OperationTool, NcGenerationRequest.PostProcessorTool }),
                "Read sequence did not select operations then resolve the configured post-processor");
        }
        finally { StudioStrings.Apply(language); }
    }

    private sealed class Client : IConfirmableMcpClient
    {
        public bool IsConnected => true;
        public IReadOnlyList<McpToolDefinition> Tools { get; } =
        [
            new() { Name = NcGenerationRequest.OperationTool, Annotations = new JObject { ["readOnlyHint"] = true } },
            new() { Name = NcGenerationRequest.PostProcessorTool, Annotations = new JObject { ["readOnlyHint"] = true } },
            new() { Name = NcGenerationRequest.GenerateTool, Annotations = new JObject { ["readOnlyHint"] = false } },
            new() { Name = NcGenerationRequest.ExportTool, Annotations = new JObject { ["readOnlyHint"] = false } }
        ];
        internal List<string> Calls { get; } = [];

        public Task<McpToolResult> CallToolAsync(string name, JObject arguments, CancellationToken cancellationToken)
        {
            Calls.Add(name);
            if (name == NcGenerationRequest.OperationTool)
                return Task.FromResult(new McpToolResult { StructuredContent = new JObject
                {
                    ["items"] = new JArray(new JObject
                    {
                        ["operation"] = new JObject { ["element"] = new JObject { ["documentId"] = "cam-test", ["id"] = 42 } },
                        ["operationName"] = "[1: Drilling]", ["operationType"] = "Drilling", ["hasTool"] = false
                    }),
                    ["total"] = 1, ["offset"] = 0, ["hasMore"] = false, ["nextOffset"] = null
                } });
            if (name == NcGenerationRequest.PostProcessorTool)
                return Task.FromResult(new McpToolResult { StructuredContent = new JObject { ["value"] = "Fanuc", ["units"] = "" } });
            throw new InvalidOperationException("Unexpected direct NC call: " + name);
        }

        public Task<JObject> PrepareToolAsync(string name, JObject arguments, CancellationToken cancellationToken) =>
            Task.FromResult(new JObject { ["toolName"] = name, ["arguments"] = arguments.DeepClone(), ["target"] = new JObject { ["action"] = name }, ["confirmationToken"] = "token" });

        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject arguments, string confirmationToken, CancellationToken cancellationToken)
        {
            if (name == NcGenerationRequest.GenerateTool)
                return Task.FromResult(new McpToolResult { StructuredContent = new JObject
                {
                    ["generated"] = true, ["postProcessorId"] = "Fanuc", ["ncFiles"] = new JArray(new JObject
                    {
                        ["element"] = new JObject { ["documentId"] = "iso-test", ["id"] = 7 },
                        ["name"] = "part", ["suggestedFileName"] = "part.nc"
                    })
                } });
            if (name == NcGenerationRequest.ExportTool)
                return Task.FromResult(new McpToolResult { StructuredContent = new JObject { ["exported"] = true, ["fileName"] = arguments["fileName"] } });
            throw new InvalidOperationException("Unexpected confirmed NC call: " + name);
        }
    }
}
