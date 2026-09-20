using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TopSolid.Automation.AI.Studio;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.Tests;

internal static class CamDocumentBrowseTests
{
    internal static async Task Live(string executable, string projectName)
    {
        await using var client = new StdioMcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var token = timeout.Token;
        await client.ConnectAsync(executable, token);
        var before = await client.CallToolAsync("topsolid_get_active_document", new JObject(), token);
        async Task<List<JObject>> Rows(string tool, JObject scope)
        {
            var rows = new List<JObject>();
            for (var page = 0; page < 250; page++)
            {
                var args = (JObject)scope.DeepClone(); args["offset"] = rows.Count; args["limit"] = 20;
                var result = await client.CallToolAsync(tool, args, token);
                var data = PdmInventory.Data(result);
                Check.True(!result.IsError && data?["items"] is JArray, "Live list failed: " + tool);
                rows.AddRange(((JArray)data!["items"]!).OfType<JObject>());
                if ((bool?)data["hasMore"] != true) return rows;
            }
            throw new InvalidOperationException("Live fixture list exceeded bound");
        }
        var projects = await Rows("topsolid_list_projects", new JObject());
        var project = projects.Single(p => (string?)p["name"] == projectName);
        var pending = new Queue<List<JObject>>(); pending.Enqueue([project]);
        List<JObject>? path = null;
        for (var count = 0; count < 64 && pending.Count > 0 && path == null; count++)
        {
            var parents = pending.Dequeue();
            var rows = await Rows("topsolid_list_pdm_children", new JObject { ["pdmObjectId"] = parents[^1]["pdmObjectId"]!.DeepClone() });
            var document = rows.FirstOrDefault(r => (string?)r["extension"] == ".TopMillTurn" && (bool?)r["isLoaded"] == true);
            if (document != null) { path = [..parents, document]; break; }
            foreach (var folder in rows.Where(r => (string?)r["kind"] == "folder")) pending.Enqueue([..parents, folder]);
        }
        Check.True(path != null, "The project has no already-loaded CAM fixture; no document was opened.");
        var selectedDocument = path![^1]["documentId"]!.DeepClone();
        var documentBefore = await client.CallToolAsync("topsolid_get_document_info", new JObject { ["documentId"] = selectedDocument }, token);
        var step = 0; var operationCount = 0;
        using var model = new FakeAiProvider();
        var session = new ChatSession(model, client)
        {
            AskUserAsync = async (q, ct) =>
            {
                while (q.HasMore) q = await q.LoadMoreAsync!(ct);
                var id = path[step++]["pdmObjectId"];
                var choice = q.Choices.Single(c => JToken.DeepEquals(q.Answer(selectedKeys: [c.Key]).Data["selected"]![0]!["value"]!["pdmObjectId"], id));
                return q.Answer(selectedKeys: [choice.Key]);
            },
            ShowListAsync = async (q, ct) =>
            {
                while (q.HasMore) q = await q.LoadMoreAsync!(ct);
                Check.True(q.Choices.All(c => c.Kind == "operation" && JToken.DeepEquals(q.PreviewTargetFor(c.Key)?["documentId"], selectedDocument)), "Live operation document mismatch");
                operationCount = q.Choices.Count;
            },
            ConfirmChangeAsync = (_, _) => throw new InvalidOperationException("Native writes are disabled in this live read fixture.")
        };
        var answer = await session.SendAsync("show cam operation list", token);
        Check.True(operationCount > 0 && step == path.Count && model.CompletionCount == 0, "Live staged browse failed: " + answer);
        var after = await client.CallToolAsync("topsolid_get_active_document", new JObject(), token);
        var documentAfter = await client.CallToolAsync("topsolid_get_document_info", new JObject { ["documentId"] = selectedDocument }, token);
        Check.True(JToken.DeepEquals(PdmInventory.Data(before), PdmInventory.Data(after)), "Active document changed");
        Check.True(JToken.DeepEquals(PdmInventory.Data(documentBefore), PdmInventory.Data(documentAfter)), "Selected document state changed");
        var report = new JObject { ["project"] = projectName, ["document"] = path[^1]["name"]!.DeepClone(), ["operationCount"] = operationCount,
            ["dialogSteps"] = step, ["nativeWrites"] = 0, ["modelCalls"] = 0, ["activeAndDocumentStateUnchanged"] = true };
        System.IO.Directory.CreateDirectory("artifacts/cam-document-browse");
        await System.IO.File.WriteAllTextAsync("artifacts/cam-document-browse/live.json", report.ToString(), token);
        Console.WriteLine("PASS live staged CAM browse: " + report.ToString(Newtonsoft.Json.Formatting.None));
    }

    internal static async Task Ui(Window owner, Action<Window, string> render)
    {
        var previous = StudioStrings.CurrentLanguage;
        StudioStrings.Apply("ko");
        try
        {
            var client = new Client("loaded");
            var step = 0;
            async Task Render(UserQuestion question)
            {
                var window = new QuestionWindow(question) { Owner = owner, ShowInTaskbar = false, ShowActivated = false,
                    WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
                try
                {
                    window.Show();
                    await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                    Check.Equal(question.CanGoBack ? Visibility.Visible : Visibility.Collapsed, ((Button)window.FindName("PreviousStep")).Visibility, "Back visibility is wrong");
                    Check.Equal(question.IsBrowse ? Visibility.Collapsed : Visibility.Visible, ((Button)window.FindName("ContinueQuestion")).Visibility, "Wrong selection/browse mode");
                    if (question.HasNavigationContext && question.Context.Length > 0)
                        Check.Equal(question.Context, ((TextBlock)window.FindName("QuestionContext")).Text, "Document path missing in UI");
                    render(window, "cam-document-step-" + ++step + ".png");
                }
                finally { window.Close(); }
            }
            var result = await CamDocumentBrowseRequest.Run("show cam operation list", client, async (question, token) =>
            {
                while (question.HasMore) question = await question.LoadMoreAsync!(token);
                await Render(question);
                return question.Answer(selectedKeys: [question.Choices.Last().Key]);
            }, async (question, token) =>
            {
                while (question.HasMore) question = await question.LoadMoreAsync!(token);
                await Render(question);
            }, null, _ => { }, CancellationToken.None);
            Check.Equal(4, step, "Project, folder, document and operations did not all render: " + result?[^1].Content);
        }
        finally { StudioStrings.Apply(previous); }
    }

    internal static async Task Run()
    {
        foreach (var text in new[] { "show cam operation list", "just cam operation list", "list operations", "가공 도큐먼트의 오퍼레이션을 리스트를 볼래, 가공 도큐먼트 먼저 선택하고",
                     "아니 project --> 그 안의 도큐먼트 리스트를 먼저 보여줘서 가공 도큐먼트를 먼저 선택하게 해줘야지" })
            Check.True(CamDocumentBrowseRequest.Matches(text), "Logged request missed staged browsing: " + text);
        foreach (var text in new[] { "show current cam operations", "change cam operation feed", "preview cam operations", "explain project documents" })
            Check.True(!CamDocumentBrowseRequest.Matches(text), "Unrelated request captured: " + text);

        foreach (var mode in new[] { "loaded", "open", "decline", "tampered", "foreign", "cancel", "back", "empty", "bad-page", "forged" })
        {
            var client = new Client(mode);
            using var model = new FakeAiProvider();
            var stage = 0; var shown = 0; var confirmations = 0;
            var session = new ChatSession(model, client)
            {
                AskUserAsync = async (question, token) =>
                {
                    stage++;
                    Check.True(!client.Calls.Any(c => c.Name == CamSelectionRequest.Tool), "Operations read before document selection");
                    if (stage == 1)
                    {
                        Check.True(question.Choices.All(c => c.Kind == "project"), "Mixed project picker");
                        if (mode == "cancel") return null;
                        Check.True(question.HasMore && question.LoadMoreAsync != null, "Project paging missing");
                        question = await question.LoadMoreAsync!(token);
                        return question.Answer(selectedKeys: [question.Choices.Last().Key]);
                    }
                    Check.True(question.CanGoBack, "Document step cannot return to parent");
                    Check.True(question.Choices.All(c => c.Kind == "document"), "Project/operation mixed into document picker");
                    if (mode == "empty")
                    {
                        Check.Equal(0, question.Choices.Count, "Non-CAM document leaked into CAM picker");
                        Check.True(question.Context.Contains("No matching"), "Empty folder guidance missing");
                        return null;
                    }
                    if (mode == "back" && stage == 3) return UserQuestion.Back();
                    if (stage == 2 || mode == "back" && stage == 4)
                    {
                        Check.Equal("Project B", question.Context, "Wrong selected project context");
                        Check.Equal("CAM folder", question.Choices.Single().Label, "Wrong project's documents");
                    }
                    else
                    {
                        Check.True(question.Context.Contains("Project B → CAM folder"), "Folder path lost");
                        Check.Equal("Chosen CAM", question.Choices.Single().Label, "Filtered paging lost the CAM document");
                        if (mode == "forged") return new QuestionAnswer(JObject.Parse("{status:'answered',selected:[{value:{documentId:'other'}}]}"), "forged");
                    }
                    return question.Answer(selectedKeys: [question.Choices.Single().Key]);
                },
                ConfirmChangeAsync = (proposal, _) =>
                {
                    confirmations++;
                    Check.True(proposal["confirmationToken"] == null, "Token exposed to approval UI");
                    Check.Equal("doc-selected-17", (string?)proposal["arguments"]?["documentId"], "Wrong open target");
                    return Task.FromResult(mode != "decline");
                },
                ShowListAsync = async (question, token) =>
                {
                    shown++;
                    Check.True(question.IsBrowse && question.Choices.All(c => c.Kind == "operation"), "Mixed operation list");
                    Check.Equal(2, question.Total, "Partial operation list hid the native total");
                    Check.True(question.Context.EndsWith("Chosen CAM"), "Operation document context lost");
                    Check.True(question.HasMore, "Operation continuation missing");
                    question = await question.LoadMoreAsync!(token);
                    Check.Equal(2, question.Choices.Count, "Operation continuation lost rows");
                    Check.True(!question.HasMore, "Operation list never completes");
                }
            };
            var errors = new List<string>();
            session.Trace += t => { if (t.Kind == "Tool error") errors.Add(t.Text); };
            await session.SendAsync("가공 도큐먼트의 오퍼레이션을 리스트를 볼래, 가공 도큐먼트 먼저 선택하고", CancellationToken.None);
            var succeeds = mode is "loaded" or "open" or "back";
            Check.Equal(succeeds ? 1 : 0, shown, "Unexpected operations dialog: " + mode + " " + string.Join("; ", errors));
            Check.Equal(mode == "open" ? 1 : 0, client.Opened, "Unexpected native open: " + mode);
            Check.Equal(mode is "open" or "decline" ? 1 : 0, confirmations, "Unexpected approval: " + mode);
            Check.Equal(0, model.CompletionCount, "Staged browse fell through to model: " + mode);
        }
    }

    private sealed class Client(string mode) : IConfirmableMcpClient
    {
        public bool IsConnected => true;
        public List<(string Name, JObject Args)> Calls { get; } = [];
        public int Opened { get; private set; }
        public IReadOnlyList<McpToolDefinition> Tools { get; } = new[] { "topsolid_list_projects", "topsolid_list_pdm_children", CamSelectionRequest.Tool, "topsolid_open_document" }
            .Select(name => new McpToolDefinition { Name = name, Annotations = new JObject { ["readOnlyHint"] = name != "topsolid_open_document" } }).ToArray();
        public Task<McpToolResult> CallToolAsync(string name, JObject args, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Calls.Add((name, (JObject)args.DeepClone()));
            var offset = (int)args["offset"]!;
            if (name == "topsolid_list_projects")
                return Page(offset, 2, new JObject { ["pdmObjectId"] = offset == 0 ? "p1" : "p2", ["name"] = offset == 0 ? "Project A" : "Project B" });
            if (name == "topsolid_list_pdm_children")
            {
                var parent = (string?)args["pdmObjectId"];
                Check.True(parent is "p2" or "folder-id-42", "Unselected project queried");
                if (mode == "empty") return Page(0, 1, JObject.Parse("{pdmObjectId:'part',kind:'document',extension:'.TopPrt',name:'Part'}"));
                if (parent == "p2") return Page(0, 1, JObject.Parse("{pdmObjectId:'folder-id-42',kind:'folder',name:'CAM folder'}"));
                if (offset == 0) return Page(mode == "bad-page" ? 4 : 0, 2, JObject.Parse("{pdmObjectId:'part',kind:'document',extension:'.TopPrt',name:'Part'}"));
                return Page(1, 2, new JObject { ["pdmObjectId"] = "cam-pdm", ["documentId"] = "doc-selected-17", ["name"] = "Chosen CAM", ["kind"] = "document",
                    ["extension"] = ".TopMillTurn", ["isLoaded"] = mode is not ("open" or "decline" or "tampered") });
            }
            Check.Equal(CamSelectionRequest.Tool, name, "Unrelated tool queried");
            Check.Equal(mode == "open" ? "opened-revision" : "doc-selected-17", (string?)args["documentId"], "Selected document ID not pinned");
            return Page(offset, 2, new JObject { ["operationName"] = "[" + (offset + 1) + ": Roughing]", ["operation"] = new JObject
                { ["element"] = new JObject { ["documentId"] = mode == "foreign" ? "active-but-wrong" : args["documentId"]!.DeepClone(), ["id"] = offset + 10 } } });
        }
        public Task<JObject> PrepareToolAsync(string name, JObject args, CancellationToken token) => Task.FromResult(new JObject
        {
            ["toolName"] = name, ["arguments"] = mode == "tampered" ? new JObject { ["documentId"] = "wrong" } : args.DeepClone(),
            ["target"] = new JObject { ["documentId"] = "doc-selected-17" }, ["confirmationToken"] = "ticket"
        });
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject args, string ticket, CancellationToken token)
        {
            Check.Equal("topsolid_open_document", name, "Unexpected write"); Check.Equal("ticket", ticket, "Wrong ticket");
            Check.Equal("doc-selected-17", (string?)args["documentId"], "Wrong open ID"); Opened++;
            return Task.FromResult(new McpToolResult { StructuredContent = JObject.Parse("{opened:true,originalDocumentId:'doc-selected-17',documentId:'opened-revision'}") });
        }
        private static Task<McpToolResult> Page(int offset, int total, JObject row) => Task.FromResult(new McpToolResult { StructuredContent = new JObject
        { ["items"] = new JArray(row), ["offset"] = offset, ["total"] = total, ["hasMore"] = offset + 1 < total, ["nextOffset"] = offset + 1 < total ? offset + 1 : null } });
    }
}
