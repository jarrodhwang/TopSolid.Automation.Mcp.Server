using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class ConversationLogRegressionTests
{
    private const string InventoryRequest = "Let me select a machining operation, then show all its cutting-condition parameters, current values, units, allowed choices and editability.";
    private const string FeedRequest = "Let me select an operation and an editable feed parameter. Ask for the new value, then show the change for approval.";
    private static JObject Element(int id = 7) => new() { ["documentId"] = "cam-revision", ["id"] = id };
    private static JObject Scope() => new() { ["element"] = Element(), ["category"] = "CuttingConditions" };
    private static AiToolCall Call(string name, JObject args, string id = "read") => new() { Id = id, Name = name, Arguments = args };
    private static JObject Feed(string name = "ToothFeedrate@CuttingConditions", bool editable = true) => new()
    {
        ["name"] = name, ["displayName"] = name.StartsWith("Tooth") ? "날 이송속도" : "접근 이송속도",
        ["parameter"] = new JObject { ["element"] = Element(5), ["name"] = name },
        ["valueType"] = "Real", ["unitType"] = "ToothFeedRate", ["realValueSI"] = .0007,
        ["displayValue"] = "0.7mm/tooth", ["editSupported"] = editable, ["readOnly"] = !editable
    };
    private static McpToolResult Page(int offset, int total, params JObject[] rows) => new() { StructuredContent = new JObject {
        ["items"] = new JArray(rows), ["offset"] = offset, ["total"] = total, ["returned"] = rows.Length,
        ["hasMore"] = offset + rows.Length < total, ["nextOffset"] = offset + rows.Length < total ? offset + rows.Length : null,
        ["operation"] = Element(), ["operationName"] = "[6: 커브따라가공]"
    } };

    internal static async Task Run()
    {
        await CompleteInventory();
        await PartialInventory();
        foreach (var approve in new[] { false, true }) await ChooseParameter(approve);
        await ActiveTarget();
        HistoryAndPagingBoundaries();
    }

    private static async Task CompleteInventory()
    {
        var client = new Client { Read = (_, args) => (int?)args["offset"] == 1 ? Page(1, 2, Feed("EntryFeed@CuttingConditions")) : Page(0, 2, Feed()) };
        using var model = new FakeAiProvider { Reply = (round, _, _) => Task.FromResult(FakeAiProvider.ToolReply(
            Call(CamWorkflow.ListTool, round == 1 ? new JObject { ["element"] = Element() } : Scope(), "list-" + round))) };
        var session = new ChatSession(model, client);
        var answer = await session.SendAsync(InventoryRequest, CancellationToken.None);
        Check.Equal(2, client.Reads.Count, "An unfiltered call executed or a continuation was missed");
        Check.Equal(1, (int)client.Reads[1].Args["offset"]!, "Pagination ignored the real nextOffset");
        Check.True(client.Reads.All(c => (string?)c.Args["category"] == "CuttingConditions"), "An unrelated CAM parameter category was read");
        Check.True(answer.Contains("날 이송속도") && answer.Contains("접근 이송속도") && answer.Contains("2/2") && answer.Contains("ToothFeedRate"), "Complete parameter table omitted values/units/rows");
        Check.Equal(2, model.CompletionCount, "The completed inventory was retransmitted for model summarization");
        Check.True(session.GetConversationSnapshot().Single().Any(m => m.ToolCalls.Any(c => c.ClientInitiated)), "Automatic page receipt was not retained");
        Check.Equal(0, client.Prepares, "Read-only inventory prepared a change");
    }

    private static async Task PartialInventory()
    {
        var client = new Client { Read = (_, args) => (int?)args["offset"] == 1 ? McpToolResult.Error("Page unavailable") : Page(0, 2, Feed()) };
        using var model = new FakeAiProvider { Reply = (_, _, _) => Task.FromResult(FakeAiProvider.ToolReply(Call(CamWorkflow.ListTool, Scope()))) };
        var answer = await new ChatSession(model, client).SendAsync(InventoryRequest, CancellationToken.None);
        Check.True(answer.Contains("Incomplete") && answer.Contains("1/2"), "A failed page was presented as a complete list");
        Check.Equal(2, client.Reads.Count, "A failed continuation was automatically retried");
    }

    private static async Task ChooseParameter(bool approve)
    {
        var client = new Client { Read = (_, _) => Page(0, 3, Feed(), Feed("EntryFeed@CuttingConditions"), Feed("DrivenFeed@CuttingConditions", false)) };
        var shown = new List<string>();
        using var model = new FakeAiProvider { Reply = (round, _, _) => Task.FromResult(round switch
        {
            1 => FakeAiProvider.ToolReply(Call(CamWorkflow.ListTool, new JObject { ["element"] = Element(), ["nameContains"] = "Feed" })),
            2 => FakeAiProvider.ToolReply(Call(QuestionSources.ToolName, new JObject { ["question"] = "Enter feed", ["kind"] = "decimal", ["unit"] = "mm/tooth" }, "premature")),
            3 => FakeAiProvider.ToolReply(Call(QuestionSources.ToolName, new JObject { ["question"] = "Select feed", ["kind"] = "select", ["itemKind"] = "camParameter",
                ["sources"] = new JArray(new JObject { ["toolCallId"] = "read", ["path"] = "/items", ["editableOnly"] = true }) }, "select")),
            4 => FakeAiProvider.ToolReply(Call(QuestionSources.ToolName, new JObject { ["question"] = "Enter feed", ["kind"] = "decimal", ["unit"] = "mm/tooth", ["maximum"] = 10000 }, "unverified-bound")),
            5 => FakeAiProvider.ToolReply(Call(QuestionSources.ToolName, new JObject { ["question"] = "Enter feed", ["kind"] = "decimal", ["unit"] = "mm/tooth" }, "input")),
            6 => FakeAiProvider.ToolReply(Call("topsolid_set_cam_parameter_value", new JObject { ["element"] = Element(), ["documentId"] = "cam-revision",
                ["name"] = "EntryFeed@CuttingConditions", ["valueType"] = "Real", ["unitType"] = "ToothFeedRate", ["realValueSI"] = .002 }, "write")),
            _ => new AiReply { Content = "The selected feed was changed." }
        }) };
        var session = new ChatSession(model, client) {
            AskUserAsync = (question, _) => {
                shown.Add(question.Kind);
                if (question.Kind == "select") {
                    Check.Equal(2, question.Choices.Count, "Uneditable parameter reached the picker");
                    return Task.FromResult<QuestionAnswer?>(question.Answer(selectedKeys: [question.Choices[1].Key]));
                }
                Check.True(question.Minimum == null && question.Maximum == null, "Model invented a CAM range");
                return Task.FromResult<QuestionAnswer?>(question.Answer("2"));
            },
            ConfirmChangeAsync = (proposal, _) => {
                Check.Equal("EntryFeed@CuttingConditions", (string?)proposal["arguments"]?["name"], "The model changed the selected parameter");
                return Task.FromResult(approve);
            }
        };
        var answer = await session.SendAsync(FeedRequest, CancellationToken.None);
        Check.True(shown.SequenceEqual(new[] { "select", "decimal" }), "A premature or unverified value dialog was shown");
        Check.Equal(1, client.Prepares, "Expected one exact proposal");
        Check.Equal(approve ? 1 : 0, client.Writes, "Approval boundary changed");
        if (approve) Check.True(answer.Contains("not saved") && answer.Contains("recalculation") && answer.Contains("NC code was not generated"), "Success hid persistence/CAM status");
    }

    private static async Task ActiveTarget()
    {
        var client = new Client { Read = (_, _) => new McpToolResult { StructuredContent = JObject.Parse("{document:{documentId:'part-revision',typeFullName:'TopSolid.Cad.Design.DB.Documents.PartDocument'},hasActiveDocument:true}") } };
        using var model = new FakeAiProvider { Reply = (round, _, _) => Task.FromResult(round < 3
            ? FakeAiProvider.ToolReply(Call("topsolid_create_cylinder", new JObject { ["documentId"] = round == 1 ? "part-revison" : "part-revision", ["diameter"] = 40, ["height"] = 80 }, "cylinder-" + round))
            : new AiReply { Content = "Created." }) };
        var session = new ChatSession(model, client) { ConfirmChangeAsync = (_, _) => Task.FromResult(true) };
        await session.SendAsync("Create a cylinder in the active part.", CancellationToken.None);
        Check.Equal(1, client.Prepares, "Mistyped document ID reached preview");
        Check.Equal(1, client.Writes, "Mistyped document ID reached execution");
        Check.True(model.Requests[0].Any(m => m.Content.Contains("PartDocument")), "Active context omitted document type");
    }

    private static void HistoryAndPagingBoundaries()
    {
        var legacyData = Page(0, 1, Feed()).StructuredContent!;
        var legacy = new McpToolResult { Content = new JArray(new JObject { ["type"] = "text", ["text"] = legacyData.ToString(Formatting.None) }) };
        var originalWire = JsonConvert.SerializeObject(legacy);
        var normalized = ToolResultContext.Serialize(legacy);
        Check.True(normalized.Length < originalWire.Length && JToken.DeepEquals(legacyData, JsonConvert.DeserializeObject<McpToolResult>(normalized)!.StructuredContent), "Text JSON normalization changed parameter data or did not reduce escaping");
        Check.Equal(originalWire, JsonConvert.SerializeObject(legacy), "Model compaction modified the original transport receipt");
        var list = Page(0, 1, Feed()); ((JObject)list.StructuredContent!["items"]![0]!)["metadata"] = new string('x', 12000);
        var history = new AiMessage[] {
            new() { Role = "user", Content = FeedRequest },
            new() { Role = "assistant", ToolCalls = [Call(CamWorkflow.ListTool, Scope())] },
            new() { Role = "tool", ToolName = CamWorkflow.ListTool, ToolCallId = "read", Content = ToolResultContext.Serialize(list) },
            new() { Role = "tool", ToolName = "topsolid_set_cam_parameter_value", ToolCallId = "write", Content = "verified-write-receipt" }
        };
        var compact = ModelHistory.ForTurn(history).ToArray();
        Check.True(compact.Sum(m => m.Content.Length) < 2000 && history[2].Content.Length > 12000, "Compaction lost raw history or failed to reduce payload");
        Check.Equal("verified-write-receipt", compact[^1].Content, "Compaction removed a write receipt");
        var workflow = new CamWorkflow(InventoryRequest, "en");
        var bad = Page(0, 2, Feed()); bad.StructuredContent!["nextOffset"] = 0;
        workflow.Capture(CamWorkflow.ListTool, Scope(), bad);
        Check.True(workflow.NextPage() == null && workflow.Render().Contains("Incomplete"), "Non-advancing page can loop or claim completeness");
        Check.True(!new CamWorkflow("Read all cutting-condition library documents", "en").AllCuttingConditions, "Library request was silently redirected");
        Check.True(!ToolExposure.NeedsWorkflowHistory("“Let me select a machining operation, then show all its cutting conditions.”"), "Independent CAM request inherited old modeling schemas");
        Check.True(ToolExposure.NeedsWorkflowHistory("Make that cylinder blue instead") && ToolExposure.NeedsWorkflowHistory("XY평면에 크기와 위치는 알아서 해줘"), "A correction lost its original workflow schemas");
        using (var model = new FakeAiProvider()) {
            var session = new ChatSession(model, new FakeMcpClient());
            session.RestoreConversation([new[] { new AiMessage { Role = "assistant", Content = new string('x', 110000) }, new AiMessage { Role = "tool", Content = "latest-write-receipt" } }]);
            Check.True(session.GetConversationSnapshot().Single()[^1].Content.Contains("latest-write-receipt"), "An oversized turn erased its own latest action receipt");
        }
        var choice = new CamWorkflow(FeedRequest, "en");
        Check.True(choice.Validate(Call("topsolid_set_cam_parameter_value", new JObject())) != null, "A skipped parameter picker could reach prepare");
        var guard = new ActiveModelingTarget(true);
        guard.Capture(ActiveDocumentContext.Tool, new McpToolResult { StructuredContent = JObject.Parse("{document:{documentId:'cam',typeFullName:'TopSolid.Cam.NC.MillTurn.DB.Documents.MillTurnDocument'}}") });
        Check.True(guard.Validate(Call("topsolid_create_cylinder", new JObject { ["documentId"] = "cam" }), true)!.Contains("not a part"), "CAM document accepted as active part");
    }

    private sealed class Client : IConfirmableMcpClient
    {
        public bool IsConnected => true;
        public int Prepares, Writes;
        public List<(string Name, JObject Args)> Reads { get; } = [];
        public Func<string, JObject, McpToolResult> Read { get; init; } = (_, _) => throw new InvalidOperationException("Unexpected read");
        public IReadOnlyList<McpToolDefinition> Tools { get; } = new[] { CamWorkflow.ListTool, ActiveDocumentContext.Tool, "topsolid_set_cam_parameter_value", "topsolid_create_cylinder" }
            .Select(name => new McpToolDefinition { Name = name, Annotations = new JObject { ["readOnlyHint"] = name is CamWorkflow.ListTool or ActiveDocumentContext.Tool } }).ToArray();
        public Task<McpToolResult> CallToolAsync(string name, JObject arguments, CancellationToken token) {
            token.ThrowIfCancellationRequested(); Reads.Add((name, (JObject)arguments.DeepClone())); return Task.FromResult(Read(name, arguments));
        }
        public Task<JObject> PrepareToolAsync(string name, JObject arguments, CancellationToken token) {
            Prepares++; return Task.FromResult(new JObject { ["toolName"] = name, ["arguments"] = arguments.DeepClone(), ["target"] = new JObject { ["name"] = "Verified target" }, ["confirmationToken"] = "test-token" });
        }
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject arguments, string confirmationToken, CancellationToken token) {
            Writes++; return Task.FromResult(new McpToolResult { StructuredContent = new JObject { ["documentId"] = arguments["documentId"]!.DeepClone(), ["saved"] = false,
                ["recalculationMayBeRequired"] = name == "topsolid_set_cam_parameter_value", ["ncGenerated"] = false } });
        }
    }
}
