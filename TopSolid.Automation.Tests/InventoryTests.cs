using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.Mcp.Contracts;
using System.Net;

namespace TopSolid.Automation.Tests;
internal static class InventoryTests
{
    public static async Task Run()
    {
        var inventory = new PdmInventory("List of all project's names");
        foreach (var (tool, count) in new[] { ("topsolid_list_projects", 50), ("topsolid_list_libraries", 66) })
        {
            var page = new JObject { ["offset"] = 0, ["total"] = count, ["items"] = new JArray(Enumerable.Range(0, count).Select(i => new JObject { ["pdmObjectId"] = i.ToString(), ["name"] = "Duplicate <name>" })) };
            inventory.Capture(tool, new McpToolResult { Content = new JArray(new JObject { ["type"] = "text", ["text"] = page.ToString() }) });
        }
        var rendered = inventory.Render()!;
        Check.Equal(116, rendered.Split("Duplicate <name>").Length - 1, "Every row including duplicate names is preserved");
        Check.True(rendered.Contains("50 of 50 (complete)") && rendered.Contains("66 of 66 (complete)"), "Authoritative counts");
        var partial = new PdmInventory("list all projects");
        partial.Capture("topsolid_list_projects", new McpToolResult { StructuredContent = JObject.Parse("{total:3,offset:1,items:[{name:'one',pdmObjectId:'1'}]}") });
        Check.True(partial.Render()!.Contains("partial"), "Missing pages are not complete");
        var mcp = new FakeMcpClient { Tools = [new McpToolDefinition { Name = "topsolid_list_projects", Annotations = new JObject { ["readOnlyHint"] = true } }] };
        mcp.OnCall = _ => Task.FromResult(new McpToolResult { StructuredContent = new JObject {
            ["total"] = 2, ["offset"] = mcp.Calls.Count - 1, ["hasMore"] = mcp.Calls.Count == 1,
            ["items"] = new JArray(new JObject { ["name"] = "Same name", ["pdmObjectId"] = mcp.Calls.Count.ToString() }) } });
        var provider = new FakeAiProvider { Reply = (round, _, _) => Task.FromResult(round == 1 ?
            FakeAiProvider.ToolReply(new AiToolCall { Id = "list", Name = "topsolid_list_projects", Arguments = new JObject() }) : new AiReply { Content = "Only one project exists." }) };
        var session = new ChatSession(provider, mcp);
        var answer = await session.SendAsync("list all projects", CancellationToken.None);
        Check.True(answer.Contains("2 of 2 (complete)") && !answer.Contains("Only one"), "Final answer replaces lossy model summary and completes pagination");
        Check.Equal(2, mcp.Calls.Count, "One batch per page");
        Check.Equal(0, provider.CompletionCount, "Simple inventory must not wait for model inference");
        Check.True(!session.LastResponseUsedModel, "Direct MCP must not claim model inference");
        Check.Equal(2, PdmListRequest.Tools("List of all projects and libraries name").Length, "Reported request is optimized");
        var reportedKorean = PdmListRequest.Parse("현재 프로젝트와 라이브러리를 생성일 오래된 순서로 모두 보여줘.");
        Check.Equal(2, reportedKorean?.Tools.Length ?? 0, "Reported Korean PDM request resolves both categories");
        Check.Equal("oldestFirst", reportedKorean?.Order, "Reported Korean PDM request preserves oldest-first ordering");
        Check.True(reportedKorean?.Dates == true, "Reported Korean PDM request includes creation dates");
        var koreanClient = new FakeMcpClient { Tools = new[] { "topsolid_list_projects", "topsolid_list_libraries" }.Select(name => new McpToolDefinition
        {
            Name = name, Annotations = new JObject { ["readOnlyHint"] = true }, InputSchema = JObject.Parse("{properties:{orderBy:{type:'string'}}}")
        }).ToArray() };
        koreanClient.OnCall = token => Task.FromResult(new McpToolResult { StructuredContent = new JObject
        {
            ["sortApplied"] = true, ["total"] = 1, ["offset"] = 0, ["hasMore"] = false,
            ["items"] = new JArray(new JObject { ["name"] = koreanClient.Calls.Last().Name.EndsWith("projects") ? "Old Project" : "Old Library", ["pdmObjectId"] = Guid.NewGuid().ToString(), ["creationDate"] = "2009-01-01" })
        } });
        using (var koreanModel = new FakeAiProvider())
        {
            var shown = 0;
            var koreanSession = new ChatSession(koreanModel, koreanClient) { ShowListAsync = (question, _) =>
            { shown++; Check.True(question.IsBrowse && question.HasMore == false && question.Choices.Count == 2, "Korean PDM request did not produce a combined browse dialog"); return Task.CompletedTask; } };
            var koreanAnswer = await koreanSession.SendAsync("현재 프로젝트와 라이브러리를 생성일 오래된 순서로 모두 보여줘.", CancellationToken.None);
            Check.True(shown == 1 && koreanAnswer.Contains("dialog", StringComparison.OrdinalIgnoreCase) && koreanModel.CompletionCount == 0, "Reported Korean PDM request used raw text or model inference");
            Check.True(koreanClient.Calls.All(call => (string?)call.Arguments["orderBy"] == "oldestFirst" && (bool?)call.Arguments["includeCreationDates"] == true), "Korean chronological ordering was not sent to both MCP list tools");
        }
        foreach (var query in new[] { "list projects created yesterday", "list all projects and delete them", "compare all libraries", "do not list projects", "list projects sort by name oldest first" })
            Check.Equal(0, PdmListRequest.Tools(query).Length, "Complex intent must retain normal workflow: " + query);
        Check.Equal(answer, session.GetConversationSnapshot().Last().Last().Content, "History preserves authoritative answer");
        mcp.Tools[0].InputSchema = JObject.Parse("{properties:{orderBy:{type:'string'}}}");
        mcp.OnCall = _ => Task.FromResult(new McpToolResult { StructuredContent = JObject.Parse("{sortApplied:false,reason:'creationDateServiceUnavailable',message:'Creation dates unavailable; no chronological list was produced.'}") });
        var sorted = await session.SendAsync("order by old one first", CancellationToken.None);
        Check.Equal(0, provider.CompletionCount, "Sort follow-up must not trigger another model inference");
        Check.True(!sorted.Contains("Same name") && sorted.Length < 200, "Unavailable sort returns concise explanation without repeating names");
        Check.Equal("oldestFirst", (string?)mcp.Calls.Last().Arguments["orderBy"], "Old one first maps to explicit server ordering");
        mcp.OnCall = _ => Task.FromResult(new McpToolResult { StructuredContent = JObject.Parse("{sortApplied:true,total:1,offset:0,hasMore:false,items:[{name:'Old record',pdmObjectId:'b',creationDate:'2009-11-05'}]}") });
        sorted = await session.SendAsync("oldest first", CancellationToken.None);
        Check.True(sorted.Contains("Old record") && sorted.Contains("2009-11-05"), "Available dates render verified sorted receipt");
        Check.Equal(0, provider.CompletionCount, "Successful sort remains deterministic");
        var firstTurn = new ChatSession(provider, mcp);
        sorted = await firstTurn.SendAsync("List all projects name order by cretaion date (oldest to newest)", CancellationToken.None);
        Check.True(sorted.Contains("Old record") && sorted.Contains("2009-11-05"), "Reported standalone sorted request uses direct MCP");
        Check.Equal(0, provider.CompletionCount, "Sorted list must not send 96 schemas or wait for any model");
        Check.Equal("newestFirst", PdmListRequest.Parse("list projects by creation date newest to oldest")?.Order, "Reverse chronological direction");
        Check.True(PdmListRequest.Parse("list all projects with creation date")?.Dates == true, "Unsorted dates are requested explicitly");
        foreach (var query in new[] { "List all projects and libraries name list order by alphabetically", "list projects order by name ascending", "list projects sorted A-Z" })
        {
            Check.Equal("nameAscending", PdmListRequest.Parse(query)?.Order, "Alphabetical intent must bypass inference: " + query);
            Check.True(PdmListRequest.Parse(query)?.Dates == false, "Name sorting must not query dates");
        }
        Check.Equal("nameDescending", PdmListRequest.Parse("list all projects reverse alphabetically")?.Order, "Reverse alphabetic direction");
        using (var noModel = PdmFastPathTests.NoInference())
        {
            var namesClient = new FakeMcpClient { Tools = new[] { "topsolid_list_projects", "topsolid_list_libraries" }.Select(n => new McpToolDefinition {
                Name = n, Annotations = new JObject { ["readOnlyHint"] = true }, InputSchema = JObject.Parse("{properties:{orderBy:{type:'string'}}}") }).ToArray() };
            namesClient.OnCall = _ => {
                var call = namesClient.Calls.Last();
                Check.Equal("nameAscending", (string?)call.Arguments["orderBy"], "Every page must retain alphabetical order");
                Check.True(call.Arguments["includeCreationDates"] == null, "Alphabetical list queried creation dates");
                var offset = (int)call.Arguments["offset"]!;
                var items = new JArray(Enumerable.Range(offset, Math.Min(100, 119 - offset)).Select(i => new JObject { ["name"] = "Duplicate <이름>", ["pdmObjectId"] = call.Name + i }));
                return Task.FromResult(new McpToolResult { StructuredContent = new JObject { ["sortApplied"] = true, ["total"] = 119,
                    ["offset"] = offset, ["hasMore"] = offset == 0, ["nextOffset"] = offset + items.Count, ["items"] = items } });
            };
            var namesSession = new ChatSession(noModel, namesClient);
            var namesAnswer = await namesSession.SendAsync("List all projects and libraries name list order by alphabetically", CancellationToken.None);
            Check.Equal(4, namesClient.Calls.Count, "Only four native pages for both complete categories");
            Check.Equal(238, namesAnswer.Split("Duplicate <이름>").Length - 1, "Duplicate friendly names must all survive");
            Check.Equal(2, namesAnswer.Split("119 of 119 (complete)").Length - 1, "Both inventories must be complete");
            var recorded = namesSession.GetConversationSnapshot().Single();
            Check.True(!string.Concat(ModelHistory.ForTurn(recorded).Select(m => m.Content)).Contains("Duplicate <이름>"), "Later model requests repeat huge inventory payloads");
            Check.True(recorded.Last().Content.Contains("Duplicate <이름>"), "Compaction altered the diagnostic transcript");
        }
        var handler = new ScriptedHandler().Json("{}", HttpStatusCode.ServiceUnavailable).Json("{}");
        using var http = new AiHttpClient(new Uri("https://example.test/"), null, handler);
        await http.SendAsync(HttpMethod.Post, "chat", new JObject(), CancellationToken.None);
        Check.Equal(2, handler.Requests.Count, "Temporary provider failure retries HTTP only");
    }
}
