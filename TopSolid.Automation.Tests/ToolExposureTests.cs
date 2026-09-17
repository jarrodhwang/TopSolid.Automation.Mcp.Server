using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class ToolExposureTests
{
    public static async Task CatalogSelectionAndDispatch()
    {
        var catalog = Enumerable.Range(0, 180).Select(i => new McpToolDefinition { Name = "read_" + i,
            Annotations = new JObject { ["readOnlyHint"] = true }, InputSchema = JObject.Parse("{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}") }).ToArray();
        var exposure = new ToolExposure(catalog);
        Check.Equal(24, exposure.Active.Length, "Initial schema budget must remain small");
        Check.True(!exposure.Active.Any(t => t.Name == "read_179"), "Test needs an initially omitted tool");
        Check.True(exposure.Catalog.Contains("read_179"), "An omitted schema must still be discoverable without a native query");
        var before = exposure.Active.Select(t => t.Name).ToArray();
        Check.True(exposure.Select(JObject.Parse("{\"names\":[\"invented\"]}")).IsError, "Selection accepted an invented tool");
        Check.True(before.SequenceEqual(exposure.Active.Select(t => t.Name)), "Failed selection changed the active set");
        Check.True(exposure.Select(JObject.Parse("{\"names\":[\"read_179\"],\"confirmed\":true}")).IsError, "Selection accepted an approval field");
        Check.True(!exposure.Select(JObject.Parse("{\"names\":[\"read_179\"]}")).IsError && exposure.Active.Any(t => t.Name == "read_179"), "Selection did not expose an existing tool");
        Check.True(exposure.Active.Length <= 96, "Selection exceeded provider schema budget");
        var client = new FakeMcpClient { Tools = catalog };
        using var provider = new FakeAiProvider { Reply = (round, _, _) => Task.FromResult(round switch {
            1 => FakeAiProvider.ToolReply(new AiToolCall { Id = "select", Name = "studio_select_tools", Arguments = JObject.Parse("{\"names\":[\"read_179\"]}") }),
            2 => FakeAiProvider.ToolReply(new AiToolCall { Id = "read", Name = "read_179", Arguments = new JObject() }),
            _ => new AiReply { Content = "Read complete." }
        }) };
        Check.Equal("Read complete.", await new ChatSession(provider, client).SendAsync("Read the last tool.", CancellationToken.None), "Selected tool loop did not finish");
        Check.Equal(1, client.Calls.Count, "Schema selection must never dispatch a native MCP call");
        Check.Equal("read_179", client.Calls[0].Name, "Wrong selected tool executed");
        Check.True(provider.SuppliedTools.All(t => t.Length <= 96), "Chat supplied an unbounded provider payload");
        Check.True(!provider.SuppliedTools[0].Contains("read_179") && provider.SuppliedTools[1].Contains("read_179"), "Selected schema was not supplied on the next request");
        Check.True(provider.Requests[0][0].Content.Contains("read_179"), "Model did not receive the full tool name catalog");

        // A model may name a discovered tool before selecting its schema. Expose
        // the schema, but do not execute its arguments until the next request.
        var unselectedClient = new FakeMcpClient { Tools = catalog };
        using var unselectedProvider = new FakeAiProvider { Reply = (round, _, _) => Task.FromResult(round < 3
            ? FakeAiProvider.ToolReply(new AiToolCall { Id = "read-" + round, Name = "read_179", Arguments = new JObject() })
            : new AiReply { Content = "Read complete." }) };
        await new ChatSession(unselectedProvider, unselectedClient).SendAsync("Read the last tool.", CancellationToken.None);
        Check.Equal(1, unselectedClient.Calls.Count, "Unselected schema call must load, not execute");
        Check.True(unselectedProvider.SuppliedTools[1].Contains("read_179"), "Existing tool access did not recover on next round");

        var workflowCatalog = catalog.Concat(new[] { "topsolid_create_part_document", "topsolid_create_document", "topsolid_get_document_creation_context", "topsolid_open_document", "topsolid_create_sketches2d", "topsolid_list_document_types", "topsolid_get_template_projects" }
            .Select(name => new McpToolDefinition { Name = name })).ToArray();
        using var retryProvider = new FakeAiProvider { Reply = (_, _, _) => Task.FromResult(new AiReply { Content = "Please provide dimensions." }) };
        var retrySession = new ChatSession(retryProvider, new FakeMcpClient { Tools = workflowCatalog });
        foreach (var text in new[] { "Create a part and circle and parabola sketches in project A", "It does not require a template", "Now try again" })
            await retrySession.SendAsync(text, CancellationToken.None);
        foreach (var required in new[] { "topsolid_create_part_document", "topsolid_create_document", "topsolid_open_document", "topsolid_create_sketches2d" })
            Check.True(retryProvider.SuppliedTools[^1].Contains(required), "Retry lost original workflow schema " + required);
        Check.True(retryProvider.Requests[^1][0].Content.Contains("useDefaultTemplate omitted or false"), "Model creation policy lost no-template default");
        var plain = new ToolExposure(workflowCatalog, "Create a plain part document without template");
        Check.True(!plain.Active.Any(t => t.Name == "topsolid_get_template_projects"), "Plain document should not prioritize template browsing");
        var template = new ToolExposure(workflowCatalog, "Create a part document using template Company Part");
        Check.True(template.Active.Any(t => t.Name == "topsolid_get_template_projects"), "Explicit template lookup must remain accessible");
    }

    internal static void VerifyCreationWorkflow(McpToolDefinition[] catalog)
    {
        foreach (var query in new[] {
            "create part file name \"Local AI Made this part\" under the project \"AI Made this project\" , and create circle sketch in it",
            "can create document (part) under \"AI Made this project\"?",
            "create part file can be execute in your tools" })
        {
            var exposure = new ToolExposure(catalog, query);
            foreach (var required in new[] { "topsolid_create_part_document", "topsolid_create_document", "topsolid_get_document_creation_context", "topsolid_open_document", "topsolid_save_document" })
                Check.True(exposure.Active.Any(t => t.Name == required), "Reported creation prompt lost " + required);
            if (query.Contains("circle")) Check.True(exposure.Active.Any(t => t.Name == "topsolid_create_circle2d"), "Compound part/sketch workflow lost sketch creation");
            Check.True(exposure.Active.Length <= 24, "Workflow dependency selection exceeded the initial budget");
        }
        foreach (var query in new[] {
            "create part 004_more_complicate_sketch and draw tree",
            "create one more sketch - star, the start",
            "draw an ellipse",
            "create part file name(\"Gemini Creates This\") under the project \"AI Made this project\" and create 2 sketches in it, 1. circle 2. parabola",
            "Draw two sketches 20 mm to the right of reference Sketch A",
            "Create a parabola on the selected sketch plane",
            "참조 스케치 기준으로 포물선 만들어" })
        {
            var exposure = new ToolExposure(catalog, query);
            foreach (var required in new[] { "topsolid_create_sketches2d", "topsolid_get_sketch2d_context", "topsolid_read_sketch2d_geometry", "topsolid_transform_sketch2d_points" })
                Check.True(exposure.Active.Any(t => t.Name == required), "Sketch workflow requires a schema-selection round for " + required);
            if (query.Contains("selected")) Check.True(exposure.Active.Any(t => t.Name == "topsolid_get_user_selection"), "Selected reference cannot be inspected in the first request");
            Check.True(exposure.Active.Length <= 24, "Sketch schemas exceeded initial budget");
        }
        foreach (var pair in new[] { ("check in the project", "topsolid_check_in_pdm_objects"), ("save all other docs", "topsolid_save_documents") })
            Check.True(new ToolExposure(catalog, pair.Item1).Active.Any(t => t.Name == pair.Item2), "Persistence requires an unnecessary schema-selection round");
    }
}
