using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Automation.AI.Studio.Appearance;

namespace TopSolid.Automation.Tests;

internal static class UserQuestionTests
{
    internal static JObject SelectionInput(string kind = "project", bool multiple = false) => new()
    {
        ["question"] = "Choose a project", ["kind"] = "select", ["itemKind"] = kind, ["multiple"] = multiple,
        ["sources"] = new JArray(new JObject { ["toolCallId"] = "read-1", ["path"] = "/items" })
    };

    internal static McpToolResult Projects() => new() { StructuredContent = new JObject { ["items"] = new JArray(
        new JObject { ["pdmObjectId"] = "6daacb4a-15ce-46ba-b216-9c93e105d9b3", ["name"] = "Gear Design", ["path"] = "Projects / Production", ["revision"] = "B" },
        new JObject { ["pdmObjectId"] = "fad512f5-5bf7-4500-9b72-f4ea8669ce30", ["name"] = "Gear Design", ["path"] = "Projects / Training", ["revision"] = "A" },
        new JObject { ["pdmObjectId"] = "926a6821-b3fe-45a2-9821-33ddb5d29720", ["name"] = "Deburring", ["path"] = "Projects / Production", ["revision"] = "C" }) } };

    internal static UserQuestion ProjectQuestion(bool multiple = false)
    {
        var sources = new QuestionSources(); sources.Capture("read-1", "topsolid_list_projects", new JObject { ["limit"] = 100 }, Projects());
        return sources.Create(SelectionInput(multiple: multiple));
    }

    public static async Task Run()
    {
        var originalLanguage = StudioStrings.CurrentLanguage; StudioStrings.Apply("en");
        try
        {
            VerifyCamChoices();
            var sources = new QuestionSources(); var result = Projects();
            var textSource = new QuestionSources();
            textSource.Capture("read-1", "topsolid_list_projects", new JObject(), new McpToolResult { Content = new JArray(new JObject { ["type"] = "text", ["text"] = result.StructuredContent!.ToString() }) });
            Check.Equal(3, textSource.Create(SelectionInput()).Choices.Count, "Text-only MCP results lost question choices");
            sources.Capture("read-1", "topsolid_list_projects", new JObject { ["limit"] = 100 }, result);
            result.StructuredContent!["items"]![0]!["name"] = "Mutated after capture";
            foreach (var kind in QuestionSources.Kinds)
            {
                var question = sources.Create(SelectionInput(kind));
                Check.Equal("Gear Design", question.Choices[0].Label, "Object choice name or snapshot was changed");
                Check.True(question.Choices.All(c => !c.Label.Contains("6daacb4a") && !c.Detail.Contains("pdmObjectId")), "User cards exposed identifiers");
                Check.True(question.Choices[0].Detail != question.Choices[1].Detail, "Same-name choices lost distinguishing context");
                var answer = question.Answer(selectedKeys: [question.Choices[1].Key]);
                Check.Equal("fad512f5-5bf7-4500-9b72-f4ea8669ce30", (string?)answer.Data["selected"]![0]!["value"]!["pdmObjectId"], "Selection changed target identity");
                Reject(() => question.Answer(selectedKeys: ["invented"]));
                Reject(() => question.Answer(selectedKeys: [question.Choices[0].Key, question.Choices[1].Key]));
            }
            var multi = sources.Create(SelectionInput(multiple: true));
            Check.Equal(2, ((JArray)multi.Answer(selectedKeys: [multi.Choices[0].Key, multi.Choices[1].Key]).Data["selected"]!).Count, "Multiple selection lost a choice");
            Reject(() => new QuestionSources().Create(SelectionInput()));
            var invalid = SelectionInput(); invalid["sources"]![0]!["path"] = "/missing"; Reject(() => sources.Create(invalid));
            invalid = SelectionInput(); invalid.Remove("sources"); invalid["choices"] = new JArray("Invented project"); Reject(() => sources.Create(invalid));
            var failedSources = new QuestionSources(); failedSources.Capture("read-1", "topsolid_list_projects", new JObject(), McpToolResult.Error("failed"));
            Reject(() => failedSources.Create(SelectionInput()));
            foreach (var kind in new[] { "integer", "decimal" })
            {
                var question = new QuestionSources().Create(new JObject { ["question"] = "Enter feed", ["kind"] = kind, ["unit"] = "mm/min", ["minimum"] = 0, ["maximum"] = 500 });
                foreach (var text in new[] { "NaN", "Infinity", "-1", "501", "1,234", "" }) Reject(() => question.Answer(text, culture: CultureInfo.GetCultureInfo("en-US")));
                Check.Equal("mm/min", (string?)question.Answer("250").Data["unit"], "Input unit was lost");
                if (kind == "integer") Reject(() => question.Answer("12.5"));
                else Check.Equal(12.5m, (decimal)question.Answer("12,5", culture: CultureInfo.GetCultureInfo("de-DE")).Data["value"]!, "Locale decimal changed the number");
            }
            var color = new QuestionSources().Create(new JObject { ["question"] = "Color", ["kind"] = "color" });
            Check.Equal(128, (int)color.Answer("#ff8000").Data["value"]!["g"]!, "Color value mismatch"); Reject(() => color.Answer("red"));
            var textQuestion = new QuestionSources().Create(new JObject { ["question"] = "Name", ["kind"] = "text" });
            Check.Equal("Part A", (string?)textQuestion.Answer("Part A").Data["value"], "Text input changed"); Reject(() => textQuestion.Answer(" "));
            foreach (var key in StudioStrings.Keys.Where(k => k.StartsWith("Question.") || k == "Activity.Question"))
            { StudioStrings.Apply("en"); var en = StudioStrings.Get(key); StudioStrings.Apply("ko"); Check.True(StudioStrings.Get(key) != key && en != key, "Missing question translation: " + key); }
            StudioStrings.Apply("en");
            await RoundTrip(); await CancelStopsBatch(); await ImageRoundTrip(); await DirectAmbiguity(); await RecoveryPreservesAnswer();
            using var handler = new QuestionOllamaHandler();
            using var ollama = new OllamaProvider("http://localhost:11434", "fixture", handler);
            var ollamaSession = new ChatSession(ollama, new QuestionMcp()) { AskUserAsync = (q, _) => Task.FromResult<QuestionAnswer?>(q.Answer(selectedKeys: [q.Choices[0].Key])) };
            Check.Equal("Selection received.", await ollamaSession.SendAsync("Choose a project.", CancellationToken.None), "Ollama question wire roundtrip failed");
        }
        finally { StudioStrings.Apply(originalLanguage); }
    }

    internal static UserQuestion CamQuestion()
    {
        const string sweep = "TopSolid.Cam.NC.MillTurn.Form.DB.Sweeping.Operation.SweepingOperation";
        var rows = new JArray(
            new JObject { ["operation"] = new JObject { ["element"] = new JObject { ["documentId"] = "cam-fixture-revision", ["id"] = 10990 } },
                ["operationName"] = "[2: 볼 동시가공 커브 스위핑 (축 방향)] ", ["description"] = "[2: 볼 동시가공 커브 스위핑 (축 방향)] ",
                ["operationType"] = sweep, ["toolName"] = "Face Mill D40 A90 L3 SD41" },
            new JObject { ["operation"] = new JObject { ["element"] = new JObject { ["documentId"] = "cam-fixture-revision", ["id"] = 11154 } },
                ["operationName"] = "[3: 동시가공 스위핑 (축 방향)] ", ["operationType"] = sweep, ["toolName"] = "Face Mill D40 A45 L6 SD41" },
            new JObject { ["operationName"] = "[6: 커브따라가공]", ["operationType"] = "TopSolid.Cam.NC.MillTurn.DB.SideMilling.SideMillingOperation" });
        var sources = new QuestionSources();
        sources.Capture("read-1", "topsolid_list_cam_operation_summaries", new JObject { ["offset"] = 50 }, new McpToolResult { StructuredContent = new JObject { ["items"] = rows } });
        var input = SelectionInput("operation"); input["question"] = "가공 작업을 선택하세요.";
        return sources.Create(input);
    }

    private static void VerifyCamChoices()
    {
        var question = CamQuestion();
        Check.Equal("[2: 볼 동시가공 커브 스위핑 (축 방향)]", question.Choices[0].Label, "CAM summary name and native number must survive pagination");
        Check.Equal("[3: 동시가공 스위핑 (축 방향)]", question.Choices[1].Label, "Different sweeping names must stay distinct");
        Check.True(!question.Choices[0].Detail.Contains("볼 동시") && question.Choices[0].Detail.Contains("Face Mill"), "Details must keep tooling context without repeating the title");
        Check.Equal(11154, (int)question.Answer(selectedKeys: [question.Choices[1].Key]).Data["selected"]![0]!["value"]!["operation"]!["element"]!["id"]!, "Friendly labels changed the selected operation");
        Check.True(question.Choices[0].IconKey != "operation" && question.Choices[0].IconKey != question.Choices[2].IconKey, "Sweeping and side milling need their actual native icons");
        foreach (var type in new[] { "TopSolid.Cam.NC.MillTurn.Form.DB.Roughing.RoughingOperation", "TopSolid.Cam.NC.MillTurn.FiveAxis.DB.Contour.ContourOperation", "TopSolid.Cam.NC.MillTurn.DB.PointToPoint.Operations.HoleOperation" })
            Check.True(TopSolidIcons.OperationKey(new JObject { ["operationType"] = type }) != "operation", "Missing native operation icon: " + type);
        Check.Equal("operation", TopSolidIcons.OperationKey(new JObject { ["operationType"] = "UnknownOperation", ["name"] = "5 axis roughing" }), "Operation names must never guess machining type");
        foreach (var sample in new[] { ("Tool", "cam-tool"), ("CuttingConditions|Tool", "cam-cutting-conditions"), ("Geometry", "cam-geometry"), ("First|Strategy", "cam-strategy"), ("Global|Comments", "cam-comment"), ("MultiAxis", "cam-multi-axis"), ("ToFiveAxisPrimitive", "cam-multi-axis"), ("Properties", "cam-properties"), ("UnknownCategory", "parameter") })
        {
            var parameter = new JObject { ["name"] = "Value@" + sample.Item1, ["displayName"] = "Native parameter", ["categories"] = new JArray(sample.Item1.Split('|')) };
            var sources = new QuestionSources(); sources.Capture("read-1", "topsolid_list_cam_parameters", new JObject(), new McpToolResult { StructuredContent = new JObject { ["items"] = new JArray(parameter) } });
            Check.Equal(sample.Item2, sources.Create(SelectionInput("camParameter")).Choices[0].IconKey, "CAM pane icon mismatch: " + sample.Item1);
        }
    }

    private static async Task RoundTrip()
    {
        var client = new QuestionMcp(); var prompts = 0; var approvals = 0;
        using var provider = new FakeAiProvider { Reply = (round, messages, _) =>
        {
            if (round == 1) return Task.FromResult(FakeAiProvider.ToolReply(new AiToolCall { Id = "read-1", Name = "topsolid_list_projects", Arguments = new JObject() }));
            if (round == 2) return Task.FromResult(FakeAiProvider.ToolReply(new AiToolCall { Id = "q1", Name = QuestionSources.ToolName, Arguments = SelectionInput() }, Write("premature")));
            if (round == 3)
            {
                var receipt = JObject.Parse(messages.Single(m => m.Role == "tool" && m.ToolCallId == "q1").Content);
                Check.Equal("fad512f5-5bf7-4500-9b72-f4ea8669ce30", (string?)receipt["structuredContent"]!["selected"]![0]!["value"]!["pdmObjectId"], "Real selected row did not reach model");
                Check.Equal(0, client.Writes, "Same-batch change ran before using the answer");
                return Task.FromResult(FakeAiProvider.ToolReply(Write("later")));
            }
            return Task.FromResult(new AiReply { Content = "Updated selected project." });
        } };
        var session = new ChatSession(provider, client)
        {
            AskUserAsync = (q, _) => { prompts++; return Task.FromResult<QuestionAnswer?>(q.Answer(selectedKeys: [q.Choices[1].Key])); },
            ConfirmChangeAsync = (_, _) => { approvals++; return Task.FromResult(true); }
        };
        await session.SendAsync("Choose a project to modify.", CancellationToken.None);
        Check.Equal(1, prompts, "Question was not shown once"); Check.Equal(1, approvals, "Selection bypassed separate change approval"); Check.Equal(1, client.Writes, "Confirmed change did not execute");
        Check.True(provider.SuppliedTools.All(t => t.Contains(QuestionSources.ToolName)), "Question tool disappeared across schema rounds");
        Check.True(provider.Requests.All(m => m[0].Content.Contains("CALL studio_ask_user")), "Model was not instructed to ask in dialog");
    }

    private static async Task CancelStopsBatch()
    {
        var client = new QuestionMcp();
        using var provider = new FakeAiProvider { Reply = (_, _, _) => Task.FromResult(FakeAiProvider.ToolReply(
            new AiToolCall { Id = "q1", Name = QuestionSources.ToolName, Arguments = new JObject { ["question"] = "Name?", ["kind"] = "text" } }, Write("later"))) };
        var session = new ChatSession(provider, client) { AskUserAsync = (_, _) => Task.FromResult<QuestionAnswer?>(null), ConfirmChangeAsync = (_, _) => throw new Exception("Unexpected approval") };
        var result = await session.SendAsync("Enter the missing data.", CancellationToken.None);
        Check.Equal(1, provider.CompletionCount, "Cancel queried the model again"); Check.Equal(0, client.Writes, "Cancel ran pending changes");
        Check.True(result.Contains("cancelled", StringComparison.OrdinalIgnoreCase), "Cancelled result was misrepresented");
        Check.Equal(2, session.GetConversationSnapshot().Single().Count(m => m.Role == "tool"), "Cancel left unmatched tool calls in history");
    }

    private static async Task ImageRoundTrip()
    {
        var image = await ChatAttachments.LoadAsync(Path.GetFullPath("TopSolid.Automation.AI.Studio/Assets/TopSolid/project.png"), CancellationToken.None);
        using var provider = new FakeAiProvider { Reply = (round, messages, _) =>
        {
            if (round == 1) return Task.FromResult(FakeAiProvider.ToolReply(new AiToolCall { Id = "qimage", Name = QuestionSources.ToolName, Arguments = new JObject { ["question"] = "Reference image", ["kind"] = "image" } }));
            Check.True(messages.Last().Role == "user" && messages.Last().Images.Count == 1, "Image did not reach a provider-supported user part");
            Check.True(messages[0].Content.Contains(ChatAttachments.ModelBoundary), "Question image lost its untrusted-data boundary");
            Check.True(!messages.Any(m => m.Content.Contains(image.Image!.Base64)), "Image bytes leaked into text logs or receipts");
            return Task.FromResult(new AiReply { Content = "Image received." });
        } };
        await new ChatSession(provider, new FakeMcpClient()) { AskUserAsync = (q, _) => Task.FromResult<QuestionAnswer?>(q.Answer(image: image)) }.SendAsync("Use a reference image.", CancellationToken.None);
    }

    private static async Task DirectAmbiguity()
    {
        var client = new QuestionMcp();
        var plan = await new PdmCreateRequest("part", "New Part", "Gear Design").Resolve(client, _ => { }, CancellationToken.None,
            (q, _) => Task.FromResult<QuestionAnswer?>(q.Answer(selectedKeys: [q.Choices[1].Key])));
        Check.Equal("fad512f5-5bf7-4500-9b72-f4ea8669ce30", (string?)plan.Call!.Arguments["ownerId"], "Direct creation ambiguity bypassed selected destination");
    }

    private static async Task RecoveryPreservesAnswer()
    {
        using var provider = new FakeAiProvider { Reply = (round, _, _) => round == 1
            ? Task.FromResult(FakeAiProvider.ToolReply(new AiToolCall { Id = "name-question", Name = QuestionSources.ToolName, Arguments = new JObject { ["question"] = "Part name?", ["kind"] = "text" } }))
            : Task.FromException<AiReply>(new IOException("Provider disconnected after user answered")) };
        var session = new ChatSession(provider, new FakeMcpClient()) { AskUserAsync = (q, _) => Task.FromResult<QuestionAnswer?>(q.Answer("Preserve this name")) };
        await Check.ThrowsAsync<IOException>(() => session.SendAsync("Create a named part.", CancellationToken.None));
        Check.True(session.GetConversationSnapshot().Single().Any(m => m.Role == "tool" && m.Content.Contains("Preserve this name")), "Provider failure discarded the user's answer");
    }

    private static AiToolCall Write(string id) => new() { Id = id, Name = "topsolid_test_change", Arguments = new JObject { ["ownerId"] = "fad512f5-5bf7-4500-9b72-f4ea8669ce30" } };
    private static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Invalid question/input was accepted."); }

    private sealed class QuestionOllamaHandler : HttpMessageHandler
    {
        private int round;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var input = JObject.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            JObject message;
            if (++round == 1) message = Call("topsolid_list_projects", new JObject());
            else if (round == 2)
            {
                var toolMessage = ((JArray)input["messages"]!).Last(m => (string?)m["role"] == "tool");
                var receipt = JObject.Parse((string)toolMessage["content"]!);
                var source = (string?)receipt["studioSourceId"];
                Check.True(!string.IsNullOrEmpty(source), "Ollama has no visible source reference for question choices");
                var question = SelectionInput(); question["sources"]![0]!["toolCallId"] = source;
                message = Call(QuestionSources.ToolName, question);
            }
            else
            {
                var toolMessage = ((JArray)input["messages"]!).Last(m => (string?)m["role"] == "tool");
                Check.True(((string)toolMessage["content"]!).Contains("6daacb4a-15ce-46ba-b216-9c93e105d9b3"), "Ollama did not receive the chosen native identity");
                message = new JObject { ["role"] = "assistant", ["content"] = "Selection received." };
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new JObject { ["done"] = true, ["message"] = message }.ToString(), Encoding.UTF8, "application/json") };
        }
        private static JObject Call(string name, JObject arguments) => new() { ["role"] = "assistant", ["content"] = "",
            ["tool_calls"] = new JArray(new JObject { ["function"] = new JObject { ["name"] = name, ["arguments"] = arguments } }) };
    }

    private sealed class QuestionMcp : IConfirmableMcpClient
    {
        public bool IsConnected => true;
        public int Writes { get; private set; }
        public IReadOnlyList<McpToolDefinition> Tools { get; } = new[] { "topsolid_list_projects", "topsolid_get_document_creation_context", "topsolid_create_part_document", "topsolid_test_change" }
            .Select(name => new McpToolDefinition { Name = name, Annotations = new JObject { ["readOnlyHint"] = name.Contains("list") || name.Contains("context") }, InputSchema = new JObject { ["type"] = "object" } }).ToArray();
        public Task<McpToolResult> CallToolAsync(string name, JObject args, CancellationToken token)
        {
            var result = Projects(); result.StructuredContent!["complete"] = true; result.StructuredContent["projectMatches"] = result.StructuredContent["items"]!.DeepClone();
            return Task.FromResult(result);
        }
        public Task<JObject> PrepareToolAsync(string name, JObject args, CancellationToken token) => Task.FromResult(new JObject
        { ["toolName"] = name, ["arguments"] = args.DeepClone(), ["target"] = new JObject { ["name"] = "Gear Design" }, ["confirmationToken"] = "fixture" });
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject args, string confirmationToken, CancellationToken token)
        { Writes++; return Task.FromResult(new McpToolResult { StructuredContent = new JObject { ["changed"] = true } }); }
    }
}
