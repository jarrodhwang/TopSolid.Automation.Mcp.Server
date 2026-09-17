using System.Diagnostics;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class PdmFastPathTests
{
    private const string PartRequest = "can u create one part document named \"AI made this part\" in project \"AI made this project\"";
    public static async Task Run()
    {
        Check.True(PdmCreateRequest.Parse(PartRequest)?.Kind == "part", "Reported quoted part request must be recognized");
        foreach (var text in new[] { "do not " + PartRequest, PartRequest + " and delete the old one", "explain how to create project named \"Example\"", "create part named \"X\" in project \"A\" or \"B\"" })
            Check.True(PdmCreateRequest.Parse(text) == null, "Ambiguous/multi-action intent must use the normal model workflow");
        foreach (var approve in new[] { true, false })
        {
            var client = new CreationFake(); var approvals = 0;
            using var provider = NoInference();
            var session = new ChatSession(provider, client) { ConfirmChangeAsync = (proposal, _) => {
                approvals++; Check.True(proposal["confirmationToken"] == null, "Token exposed in confirmation");
                Check.Equal("project-live", (string?)proposal["arguments"]?["ownerId"], "Must resolve the live project ID");
                Check.Equal(".TopPrt", (string?)proposal["arguments"]?["extension"], "Known native part extension does not require a loaded document");
                Check.Equal(false, (bool?)proposal["arguments"]?["useDefaultTemplate"], "Plain creation must not use a default template");
                return Task.FromResult(approve);
            } };
            var answer = await session.SendAsync(PartRequest, CancellationToken.None);
            Check.Equal(1, approvals, "Normal confirmation remains required");
            Check.Equal(approve ? 1 : 0, client.Writes, "Declined fast creation must not execute");
            Check.Equal(0, provider.CompletionCount, "No model round before/after direct creation");
            Check.Equal(1, client.Reads, "Only the destination should be read; no loaded-document discovery");
            Check.True(approve ? answer.Contains("Created \"AI made this part\"") : answer.Contains("declined"), "Answer must reflect the actual receipt");
            Check.True(session.GetConversationSnapshot().Single().Any(m => m.Role == "tool"), "Keep the authoritative creation receipt in history");
        }
        foreach (var scenario in new[] { "duplicate", "partialLookup", "tamper", "partialWrite" })
        {
            var client = new CreationFake { Scenario = scenario }; var shown = 0;
            using var provider = NoInference();
            var session = new ChatSession(provider, client) { ConfirmChangeAsync = (_, _) => { shown++; return Task.FromResult(true); } };
            if (scenario is "partialLookup" or "tamper") await Check.ThrowsAsync<InvalidOperationException>(() => session.SendAsync(PartRequest, CancellationToken.None));
            else {
                var answer = await session.SendAsync(PartRequest, CancellationToken.None);
                Check.True(!answer.StartsWith("Created "), "Uncertain creation must not claim success");
            }
            Check.Equal(scenario == "partialWrite" ? 1 : 0, client.Writes, "Ambiguous/missing/changed targets cannot execute");
            Check.Equal(scenario == "partialWrite" ? 1 : 0, shown, "Invalid creation must not reach approval");
        }
        var projectClient = new CreationFake();
        using (var provider = NoInference()) {
            var session = new ChatSession(provider, projectClient); // No approval UI.
            await session.SendAsync("Create a project named \"Demo\"", CancellationToken.None);
            Check.Equal(0, projectClient.Writes, "Headless fast creation must not auto-approve");
        }
        foreach (var scenario in new[] { "", "missingType", "partialLookup", "busy" })
        {
            var contextClient = new CreationFake { UseContext = true, UsePartTool = true, Scenario = scenario };
            using var provider = NoInference();
            var previews = 0;
            var session = new ChatSession(provider, contextClient) { ConfirmChangeAsync = (proposal, _) => {
                Check.Equal("topsolid_create_part_document", (string?)proposal["toolName"], "Use the direct empty-part tool");
                Check.True(proposal["arguments"]?["templateId"] == null, "Plain part must not select a template");
                previews++; return Task.FromResult(false); } };
            if (scenario == "partialLookup") await Check.ThrowsAsync<InvalidOperationException>(() => session.SendAsync(PartRequest, CancellationToken.None));
            else await session.SendAsync(PartRequest, CancellationToken.None);
            Check.Equal(1, contextClient.Reads, "New creation context should replace two separate native lookups");
            Check.Equal(scenario is "partialLookup" or "busy" ? 0 : 1, previews, "Only incomplete/busy lookup blocks confirmation; missing loaded types must not");
            Check.Equal(0, contextClient.Writes, "Context lookup/decline changed native data");
        }
    }

    internal static FakeAiProvider NoInference() => new() { Reply = (_, _, _) => throw new InvalidOperationException("A direct PDM request unexpectedly invoked the AI model.") };

    public static async Task Live(string server)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        await using var mcp = new StdioMcpClient(); await mcp.ConnectAsync(server, deadline.Token);
        using var provider = NoInference();
        var session = new ChatSession(provider, mcp); var metrics = new JArray(); var shown = 0;
        session.ConfirmChangeAsync = (_, _) => { shown++; return Task.FromResult(false); }; // Read-only preview; never submit writes.
        foreach (var request in new[] { "List all projects and libraries name list order by alphabetically", "reverse alphabetical order", "List all projects name order by cretaion date (oldest to newest)", "list all projects and libraries", "Create a project named \"MCP timing preview only\"", PartRequest })
        {
            var watch = Stopwatch.StartNew(); var answer = await session.SendAsync(request, deadline.Token); watch.Stop();
            Check.True(!session.LastResponseUsedModel && provider.CompletionCount == 0, "Native fast path invoked inference");
            if (!request.StartsWith("Create") && request != PartRequest) Check.True(answer.Contains("(complete)"), "Live inventory incomplete: " + answer);
            else Check.True(answer.Contains("declined"), "Preview should be declined without creating anything: " + answer);
            metrics.Add(new JObject { ["request"] = request, ["seconds"] = watch.Elapsed.TotalSeconds, ["modelCalls"] = provider.CompletionCount, ["changesSubmitted"] = 0 });
            Console.WriteLine($"Direct MCP: {watch.Elapsed.TotalSeconds:F3}s — {request}");
        }
        Check.Equal(2, shown, "Both creation requests must reach explicit confirmation previews only");
        Directory.CreateDirectory("artifacts/log-review-0.5.2");
        File.WriteAllText("artifacts/log-review-0.5.2/live-timing.json", metrics.ToString());
    }

    private sealed class CreationFake : IConfirmableMcpClient
    {
        public string Scenario = "";
        public bool UseContext, UsePartTool;
        public int Writes, Reads;
        public bool IsConnected => true;
        public IReadOnlyList<McpToolDefinition> Tools => new[] { "topsolid_list_projects", "topsolid_list_document_summaries", "topsolid_create_project", "topsolid_create_document" }
            .Concat(UseContext ? new[] { "topsolid_get_document_creation_context" } : Array.Empty<string>())
            .Concat(UsePartTool ? new[] { "topsolid_create_part_document" } : Array.Empty<string>())
            .Select(name => new McpToolDefinition { Name = name, Annotations = new JObject { ["readOnlyHint"] = !name.Contains("create") } }).ToArray();
        public Task<McpToolResult> CallToolAsync(string name, JObject arguments, CancellationToken token)
        {
            Check.True(!name.Contains("create"), "Write used unconfirmed dispatch"); Reads++;
            if (name == "topsolid_get_document_creation_context") return Task.FromResult(new McpToolResult { StructuredContent = new JObject {
                ["complete"] = Scenario != "partialLookup", ["projectMatches"] = JArray.Parse("[{name:'AI Made This Project',pdmObjectId:'project-live'}]"),
                ["documentTypes"] = new JArray(), ["readiness"] = new JObject { ["canCreatePdmObjects"] = Scenario != "busy", ["activeCommandName"] = Scenario == "busy" ? "Sketch edit" : null } } });
            Check.Equal("topsolid_list_projects", name, "Direct creation must not inspect loaded documents/templates");
            var items = name == "topsolid_list_projects" ? JArray.Parse("[{name:'AI Made This Project',pdmObjectId:'project-live'}]") :
                JArray.Parse("[{type:'TopSolid.Cad.Design.DB.Documents.PartDocument',extension:'.LivePartExtension'}]");
            if (Scenario == "duplicate" && name == "topsolid_list_projects") items.Add(items[0].DeepClone());
            if (Scenario == "missingType" && name != "topsolid_list_projects") items.Clear();
            return Task.FromResult(new McpToolResult { StructuredContent = new JObject { ["items"] = items, ["total"] = items.Count, ["hasMore"] = false, ["failed"] = Scenario == "partialLookup" ? 1 : 0 } });
        }
        public Task<JObject> PrepareToolAsync(string name, JObject arguments, CancellationToken token) => Task.FromResult(new JObject {
            ["toolName"] = name, ["arguments"] = Scenario == "tamper" ? new JObject() : arguments.DeepClone(),
            ["target"] = new JObject { ["name"] = "Live selected project" }, ["confirmationToken"] = "fake-private-token" });
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject arguments, string confirmationToken, CancellationToken token)
        {
            Check.Equal("fake-private-token", confirmationToken, "Confirmation token mismatch"); Writes++;
            return Task.FromResult(Scenario == "partialWrite" ? new McpToolResult { IsError = true, StructuredContent = JObject.Parse("{message:'Setup failed; do not retry',partialChange:{created:true,pdmObjectId:'actual-created-id'}}") } :
                new McpToolResult { StructuredContent = new JObject { ["created"] = true, ["complete"] = true, ["name"] = arguments["name"]!.DeepClone(), ["pdmObjectId"] = "created-live", ["documentId"] = "returned-revision" } });
        }
    }
}
