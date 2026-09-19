using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class CamSelectionTests
{
    internal const string Request = "현재 CAM 문서의 모든 가공 작업을 선택 대화상자로 보여줘. 실제 작업 이름과 가공 종류별 아이콘을 표시하고, 문서는 변경하지 마.";
    internal static JObject Row(int number) => new()
    {
        ["operation"] = new JObject { ["element"] = new JObject { ["documentId"] = "cam-test", ["id"] = 1000 + number } },
        ["operationName"] = $"[{number}: {(number == 1 ? "환경 활성" : "스위핑")}]",
        ["operationType"] = number == 1 ? "TopSolid.Cam.NC.Kernel.DB.Annex.Operation.EnvironmentOperation" : "TopSolid.Cam.NC.MillTurn.Form.DB.Sweeping.Operation.SweepingOperation",
        ["hasTool"] = number != 1,
        ["toolName"] = number == 1 ? null : "공구 기능 2",
        ["toolDefinitionName"] = number == 1 ? null : "Face Mill D40 A90 L3 SD41",
        ["toolPocket"] = number == 1 ? null : "T 2",
        ["toolFunction"] = number == 1 ? null : "FaceMill"
    };
    private static McpToolResult Page(int offset, int total, params JObject[] rows) => new() { StructuredContent = new JObject
    {
        ["items"] = new JArray(rows), ["offset"] = offset, ["total"] = total,
        ["hasMore"] = offset + rows.Length < total, ["nextOffset"] = offset + rows.Length < total ? offset + rows.Length : null
    } };

    internal static UserQuestion Question()
    {
        var sources = new QuestionSources();
        sources.Capture("fixture", CamSelectionRequest.Tool, new JObject(), Page(0, 3, Row(1), Row(2), Row(3)));
        return sources.Create(new JObject { ["question"] = StudioStrings.Get("Cam.Select"), ["kind"] = "select", ["itemKind"] = "operation", ["multiple"] = true,
            ["sources"] = new JArray(new JObject { ["toolCallId"] = "fixture", ["path"] = "/items" }) });
    }

    internal static async Task Run()
    {
        var language = StudioStrings.CurrentLanguage;
        StudioStrings.Apply("ko");
        try
        {
            foreach (var request in new[] { Request,
                "현재 CAM 문서의 모든 가공 작업을 선택 대화상자로 보여줘. 실제 작업 이름과 가공 종류별 아이콘을 표시",
                "가공 작업을 선택하고 싶어. 각 작업 카드 오른쪽에 공구 번호와 전체 공구 사양을 공구 타입 아이콘과 함께 보여줘.",
                "현재 CAM 문서의 환경 활성 작업을 선택 대화상자로 보여줘. 아무것도 변경하지 마.",
                "2번 볼 동시가공 커브 스위핑 작업을 선택 대화상자로 보여줘. 사용 공구 정보도 표시해줘.",
                "2번과 3번 가공 작업을 선택 대화상자로 함께 보여줘. 각 작업의 사용 공구 번호, 사양과 아이콘도 표시하고 변경하지 마." })
            {
                using var model = new FakeAiProvider { Reply = (_, _, _) => throw new Exception("Explicit dialog invoked model") };
                var client = new Client(); var asked = 0;
                var session = new ChatSession(model, client) { AskUserAsync = (q, _) =>
                {
                    asked++;
                    var expected = request.Contains("환경 활성 작업") || request.StartsWith("2번 볼") ? 1 : request.StartsWith("2번과") ? 2 : 3;
                    Check.Equal(expected, q.Choices.Count, "Explicit selection scope changed");
                    Check.True(q.Choices.All(c => !c.Detail.Contains("TopSolid.Cam") && !(c.ToolText ?? "").Contains("공구 기능")), "Native implementation names leaked to cards");
                    foreach (var choice in q.Choices.Where(c => c.ToolText != null))
                        Check.Equal("T 2\nFace Mill D40 A90 L3 SD41\n페이스밀", choice.ToolText, "Tool number/name/type are not separated");
                    return Task.FromResult<QuestionAnswer?>(q.Answer(selectedKeys: [q.Choices.Last().Key]));
                } };
                var answer = await session.SendAsync(request, CancellationToken.None);
                Check.True(asked == 1 && !session.LastResponseUsedModel && answer.StartsWith("선택:"), "Explicit selection was reported without a dialog");
                Check.Equal("cam-test", (string?)client.Calls[1]["documentId"], "Later pages did not pin the document");
                Check.True(session.GetConversationSnapshot().Single().Any(m => m.Role == "tool" && m.ToolName == QuestionSources.ToolName), "Dialog receipt missing from history");
            }
            foreach (var request in new[] { "가공 작업을 선택하고 이송을 수정해줘", "가공 작업을 선택하지 마", "Explain CAM operations", "가공 작업을 선택 없이 보여줘" })
                Check.True(!CamSelectionRequest.Matches(request), "Read-only fast path swallowed another workflow");

            foreach (var mode in new[] { "error", "duplicate", "changed-document", "bad-page", "changed-total", "missing", "overflow", "cancel", "headless" })
            {
                var asked = 0;
                using var model = new FakeAiProvider { Reply = (_, _, _) => throw new Exception("Failed selection invoked model") };
                var session = new ChatSession(model, new Client { Mode = mode }) { AskUserAsync = mode == "headless" ? null : (_, _) => { asked++; return Task.FromResult<QuestionAnswer?>(null); } };
                var result = await session.SendAsync(mode == "missing" ? "99번 가공 작업을 선택 대화상자로 보여줘" :
                    mode == "overflow" ? "99999999999999999번 가공 작업을 선택 대화상자로 보여줘" : Request, CancellationToken.None);
                Check.Equal(mode == "cancel" ? 1 : 0, asked, "Incomplete data opened a dialog");
                Check.True(!result.StartsWith("선택:"), "Failed/cancelled selection claimed completion");
            }
            var question = Question();
            var selected = question.Answer(selectedKeys: [question.Choices[1].Key]);
            Check.Equal(1002, (int)selected.Data["selected"]![0]!["value"]!["operation"]!["element"]!["id"]!, "Display formatting modified execution identity");
            Check.Equal("환경 활성 스위핑 사이드 밀링 페이스밀", CamDisplay.Text("TopSolid.Cam.NC.Kernel.DB.Annex.Operation.EnvironmentOperation TopSolid.Cam.NC.MillTurn.Form.DB.Sweeping.Operation.SweepingOperation TopSolid.Cam.NC.MillTurn.DB.SideMilling.SideMillingOperation FaceMill"), "Native type labels not translated");
            await RepairFalseSuccess();
        }
        finally { StudioStrings.Apply(language); }
    }

    internal static async Task Live(string executable)
    {
        var language = StudioStrings.CurrentLanguage;
        StudioStrings.Apply("ko");
        try
        {
            await using var client = new StdioMcpClient();
            using var token = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await client.ConnectAsync(executable, token.Token);
            using var model = new OllamaProvider("http://localhost:11434", "gemma4:e4b");
            var before = await client.CallToolAsync("topsolid_get_active_document", new JObject(), token.Token);
            var opened = 0;
            var record = new JObject { ["server"] = executable, ["provider"] = "Ollama/gemma4:e4b", ["nativeWrites"] = 0 };
            var traces = new JArray();
            var session = new ChatSession(model, client) { AskUserAsync = (question, _) =>
            {
                opened++;
                Check.True(question.Choices.Count > 0, "Live selection returned no choices");
                record["choices"] = new JArray(question.Choices.Select(c => new JObject { ["label"] = c.Label, ["detail"] = c.Detail,
                    ["tool"] = c.ToolText, ["icon"] = c.IconKey, ["toolIcon"] = c.ToolIconKey }));
                Check.True(question.Choices.All(c => !c.Detail.Contains("TopSolid.Cam") && !(c.ToolText ?? "").Contains("공구 기능")), "Live cards leaked internal names");
                return Task.FromResult<QuestionAnswer?>(null);
            } };
            session.Trace += trace => traces.Add(new JObject { ["kind"] = trace.Kind, ["text"] = trace.Text });
            record["answer"] = await session.SendAsync(Request, token.Token);
            Check.Equal(1, opened, "Live operation request did not reach the dialog callback");
            Check.True(!session.LastResponseUsedModel, "Explicit UI command unnecessarily called Ollama inference");
            var after = await client.CallToolAsync("topsolid_get_active_document", new JObject(), token.Token);
            Check.True(JToken.DeepEquals(before.StructuredContent, after.StructuredContent) && JToken.DeepEquals(before.Content, after.Content), "Active CAM document changed");
            record["modelInferenceCalls"] = 0;
            record["dialogCallbackCount"] = opened;
            record["activeDocumentUnchanged"] = true;
            record["trace"] = traces;
            Directory.CreateDirectory("artifacts/cam-selection-review-20260918");
            await File.WriteAllTextAsync("artifacts/cam-selection-review-20260918/live-selection.json", record.ToString());
            Console.WriteLine($"PASS live CAM selection: {((JArray)record["choices"]!).Count} native operation cards, Ollama provider, zero inference calls, cancelled callback, zero writes.");
        }
        finally { StudioStrings.Apply(language); }
    }

    private static async Task RepairFalseSuccess()
    {
        using var model = new FakeAiProvider { Reply = (round, _, _) => Task.FromResult(round == 1
            ? FakeAiProvider.ToolReply(new AiToolCall { Id = "bad-question", Name = QuestionSources.ToolName, Arguments = new JObject {
                ["question"] = "Choose a project", ["kind"] = "select", ["itemKind"] = "project",
                ["sources"] = new JArray(new JObject { ["toolCallId"] = "missing", ["path"] = "/structuredContent/items/operation" }) } })
            : new AiReply { Content = "I have shown the selection dialog." }) };
        var session = new ChatSession(model, new FakeMcpClient()) { AskUserAsync = (_, _) => throw new Exception("Invalid source opened UI") };
        var answer = await session.SendAsync("Choose a project", CancellationToken.None);
        Check.True(!answer.Contains("shown") && model.CompletionCount == 4, "Question failure accepted false success or exceeded repair budget");
    }

    private sealed class Client : IMcpClient
    {
        public bool IsConnected => true;
        public IReadOnlyList<McpToolDefinition> Tools { get; } = [new() { Name = CamSelectionRequest.Tool, Annotations = new JObject { ["readOnlyHint"] = true } }];
        internal string Mode = "";
        internal List<JObject> Calls = [];
        public Task<McpToolResult> CallToolAsync(string name, JObject args, CancellationToken token)
        {
            Check.Equal(CamSelectionRequest.Tool, name, "Selection attempted an unrelated action");
            Calls.Add((JObject)args.DeepClone());
            if (Mode == "error") return Task.FromResult(McpToolResult.Error("Unavailable"));
            if ((int?)args["offset"] == 0) return Task.FromResult(Page(0, 3, Row(1)));
            var row = Row(Mode == "duplicate" ? 1 : 2);
            if (Mode == "changed-document") row["operation"]!["element"]!["documentId"] = "other-document";
            return Task.FromResult(Page(Mode == "bad-page" ? 0 : 1, Mode == "changed-total" ? 4 : 3, row, Row(3)));
        }
    }
}
