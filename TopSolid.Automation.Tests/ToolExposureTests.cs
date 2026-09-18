using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class ToolExposureTests
{
    public static async Task CatalogSelectionAndDispatch()
    {
        foreach (var query in new[] { "In the current opened part create a cylinder", "현재 열린 파트에 원기둥을 만들어줘", "Color the active document" })
            Check.True(ActiveDocumentContext.Requested(query), "Explicit active-document modeling context was missed");
        foreach (var query in new[] { "Create a cylinder in project A", "Create a part named current part", "Hello", "Describe the current document" })
            Check.True(!ActiveDocumentContext.Requested(query), "Unrelated or named-destination request triggered active-document prefetch");
        var activeClient = new FakeMcpClient { Tools = [new McpToolDefinition { Name = ActiveDocumentContext.Tool, Annotations = new JObject { ["readOnlyHint"] = true } }],
            OnCall = _ => Task.FromResult(new McpToolResult { StructuredContent = JObject.Parse("{hasActiveDocument:true,document:{documentId:'prefetched-revision',name:'Part'}}") }) };
        using var activeProvider = new FakeAiProvider { Reply = (_, messages, _) => {
            Check.True(messages.Any(m => m.Role == "tool" && m.Content.Contains("prefetched-revision")), "Actual context did not reach the first model request as tool data");
            Check.True(!messages[0].Content.Contains("prefetched-revision"), "Context data must not enter system instructions");
            return Task.FromResult(new AiReply { Content = "Inputs ready." }); } };
        await new ChatSession(activeProvider, activeClient).SendAsync("In the active part create a cylinder", CancellationToken.None);
        Check.Equal(1, activeClient.Calls.Count, "Context must use exactly one MCP read"); Check.Equal(1, activeProvider.CompletionCount, "Context should not require a model round");
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
    internal static void VerifyParameterWorkflows(McpToolDefinition[] catalog)
    {
        foreach (var pair in new[] {
            ("Create Real and Color parameters in the active part", "topsolid_create_parameters"),
            ("Change the parameter Diameter to 20 mm", "topsolid_set_parameter_values"),
            ("Create a parameter formula that follows another parameter", "topsolid_create_parameter_expressions"),
            ("Edit a parameter formula", "topsolid_set_parameter_expressions"),
            ("파라미터 수식 변경", "topsolid_set_parameter_expressions"),
            ("List enumeration parameter choices", "topsolid_get_parameter_choices"),
            ("Create an entity folder", "topsolid_create_entity_folders"),
            ("Move the entities into their folder", "topsolid_move_entities"),
            ("Read entity properties", "topsolid_read_element_properties"),
            ("Change the entity color", "topsolid_update_elements"),
            ("Delete the parameter", "topsolid_delete_elements") })
        {
            foreach (var compact in new[] { true, false }) {
                var exposure = new ToolExposure(catalog, pair.Item1, compact);
                Check.True(exposure.Active.Any(t => t.Name == pair.Item2), "Entity/parameter workflow needs an avoidable schema-selection round: " + pair.Item1 + "/" + compact);
                Check.True(exposure.Active.Length <= (compact ? 10 : 24), "Entity/parameter schemas exceeded the initial budget");
                if (pair.Item1 == "Create an entity folder") Check.True(!exposure.Active.Any(t => t.Name == "topsolid_create_folder"), "Entity folder was routed to PDM creation");
            }
        }
        var local = new ToolExposure(catalog, "Create a linked parameter formula", true);
        var chars = local.Active.Sum(t => t.Description.Length + t.InputSchema.ToString(Newtonsoft.Json.Formatting.None).Length);
        Check.True(chars < 17000, "Parameter workflow flooded the local model with schemas");
        var instructions = ModelInstructions.Build(local.Active, local.Catalog, true);
        Check.True(instructions.Contains("child parameter handle") && instructions.Contains("native keys") && instructions.Contains("not every parameter"), "Parameter model instructions lost native identity/scope concepts");
    }
    internal static void VerifyCamWorkflows(McpToolDefinition[] catalog)
    {
        foreach (var query in new[] { "Show cutting conditions", "절삭조건을 가져와봐", "Show all operation CAM parameters",
            "Change the second operation feedrate to 4 m/min", "두 번째 가공의 피드값을 4m/min으로 바꿔줘", "Read the spindle RPM" })
        foreach (var compact in new[] { false, true }) {
            var exposure = new ToolExposure(catalog, query, compact);
            foreach (var name in new[] { "topsolid_list_cam_operation_summaries", "topsolid_list_cam_parameters", "topsolid_get_cam_parameter_value" })
                Check.True(exposure.Active.Any(t => t.Name == name), "CAM request omitted operation parameter discovery: " + query + "/" + name);
            if (query.Contains("Change") || query.Contains("바꿔"))
                Check.True(exposure.Active.Any(t => t.Name == "topsolid_set_cam_parameter_value"), "CAM edit selected design parameter tools");
            Check.True(!exposure.Active.Any(t => t.Name.Contains("cutting_conditions")), "Ordinary cutting conditions were routed to library documents");
            Check.True(exposure.Active.Length <= (compact ? 10 : 24), "CAM schema budget exceeded");
        }
        foreach (var query in new[] { "Read the cutting conditions document", "절삭 조건 라이브러리 조회" }) {
            var exposure = new ToolExposure(catalog, query, true);
            Check.True(exposure.Active.Any(t => t.Name == "topsolid_get_cutting_conditions_document"), "Explicit library-document request became inaccessible");
        }
        var tools = new ToolExposure(catalog, "Edit CAM feedrate", true);
        var instructions = ModelInstructions.Build(tools.Active, tools.Catalog, true);
        Check.True(instructions.Contains("INSIDE each machining operation") && instructions.Contains("editSupported") &&
            instructions.Contains("4/60 m/s") && instructions.Contains("hasMore=false"), "CAM instructions lost operation scope, editing limits, units or complete pagination");
        Check.True(instructions.Contains("Resolve Element/ElementEx"), "User mode must request friendly identity resolution");
    }
    internal static void VerifyModelingWorkflows(McpToolDefinition[] catalog)
    {
        foreach (var pair in new[] {
            ("in current opened part file, create cylinder (100mm diameter, 350mm height) with color (Red)", "topsolid_create_cylinder"),
            ("그리고 실린더를 extrude -> revolvoed 로 생성해줘", "topsolid_create_cylinder"),
            ("이제 해당 원기둥에 빨간색을 적용해줘", "topsolid_set_entity_colors"),
            ("Make this surface blue", "topsolid_set_entity_colors"),
            ("Color the faces red", "topsolid_color_shape_faces"),
            ("Draw a 3D sketch with a circle and arcs", "topsolid_create_sketch3d_curves"),
            ("Draw a slot 100mm long and 20mm wide", "topsolid_create_sketches2d"),
            ("Make a fillet and boolean union", "topsolid_get_modeling_guide"),
            ("Extrude the sketch parametrically using parameter Height", "topsolid_extrude_sketch") })
        foreach (var compact in new[] { false, true }) {
            var exposure = new ToolExposure(catalog, pair.Item1, compact);
            Check.True(exposure.Active.Any(t => t.Name == pair.Item2), "Reported modeling workflow needs an avoidable discovery round: " + pair.Item1 + "/" + compact);
            Check.True(exposure.Active.Length <= (compact ? 10 : 24), "Modeling schemas exceed the initial budget");
            if (compact) Check.True(exposure.Active.Sum(t => t.InputSchema.ToString(Newtonsoft.Json.Formatting.None).Length+t.Description.Length) < 17000, "Modeling schemas exceed local payload budget");
        }
        var cylinder = new ToolExposure(catalog, "Create a red cylinder", true);
        Check.True(!cylinder.Active.Any(t => t.Name == "topsolid_create_extruded_rectangle" || t.Name == "topsolid_create_parameters"), "Ordinary cylinder creation prioritizes the box or Color parameter failure path");
        var instructions = ModelInstructions.Build(cylinder.Active, cylinder.Catalog, true);
        Check.True(instructions.Contains("Use absolute-value modeling by default") && instructions.Contains("A Color parameter does not color geometry") && instructions.Contains("NEVER extruded_rectangle"), "Reported modeling semantics missing from model instructions");
        foreach (var query in new[] { "Create a cylinder with absolute values, no parameters", "원기둥 만들어줘. 파라미터 생성 및 저장은 하지 마." }) {
            var literal = new ToolExposure(catalog, query, true);
            Check.True(!literal.Active.Any(t => t.Name.Contains("parameter")), "Explicitly declining parameters must not prioritize parameter tools");
        }
        Check.True(instructions.Contains("Never ask the user to supply that ID"), "Active-document query should not become a request for an internal ID");
        Check.True(!new ToolExposure(catalog, "Create a red cylinder by revolve", true).Active.Any(t => t.Name == "topsolid_revolve_sketch"), "The cylinder workflow should not flood local context with generic sketch/profile workflows");
    }
}
