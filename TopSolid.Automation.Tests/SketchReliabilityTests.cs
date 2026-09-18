using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class SketchReliabilityTests
{
    internal static async Task Run()
    {
        using var oldProvider = new FakeAiProvider { Reply = (_, _, _) => Task.FromException<AiReply>(new AiProviderException("Model unavailable")) };
        var oldSession = new ChatSession(oldProvider, new FakeMcpClient());
        await Check.ThrowsAsync<AiProviderException>(() => oldSession.SendAsync("하트 스케치를 그려줘", CancellationToken.None));
        using var nextProvider = new FakeAiProvider();
        var nextSession = new ChatSession(nextProvider, new FakeMcpClient());
        nextSession.RestoreConversation(oldSession.GetConversationSnapshot());
        await nextSession.SendAsync("XY평면에 크기와 위치는 알아서 해줘", CancellationToken.None);
        Check.True(nextProvider.Requests[0].Any(m => m.Role == "user" && m.Content.Contains("하트")), "Provider change or failed response lost the requested heart");
        var portable = ModelHistory.Portable([new AiMessage { Role = "assistant", Thinking = "private-thinking", ExtraContent = JObject.Parse("{signature:'provider-secret'}"),
            ToolCalls = [new AiToolCall { Id = "old", Name = "write", Arguments = JObject.Parse("{documentId:'exact-revision'}"), ExtraContent = JObject.Parse("{thought_signature:'old-signature'}") }] },
            new AiMessage { Role = "tool", ToolName = "write", ToolCallId = "old", Content = "{nativeCreated:true,sketch:{documentId:'exact-revision',id:173}}" }]);
        Check.True(portable.All(m => m.ToolCalls.Count == 0 && m.ProviderContent == null && m.ExtraContent == null && m.Thinking == null), "Provider-specific history escaped migration");
        Check.True(portable.Any(m => m.Content.Contains("nativeCreated")) && portable.Any(m => m.Content.Contains("exact-revision")), "Migration lost action evidence or opaque IDs");
        nextSession.Clear();
        Check.Equal(0, nextSession.GetConversationSnapshot().Count, "New chat must still clear history");

        var invalid = new InvalidPreview(); using var repair = new FakeAiProvider { Reply = (round, _, _) => Task.FromResult(FakeAiProvider.ToolReply(
            new AiToolCall { Id = "invalid-" + round, Name = "topsolid_create_contour2d", Arguments = new JObject { ["attempt"] = round } })) };
        var retrySession = new ChatSession(repair, invalid) { ConfirmChangeAsync = (_, _) => throw new InvalidOperationException("Invalid geometry reached approval") };
        var error = await Check.ThrowsAsync<InvalidOperationException>(() => retrySession.SendAsync("Draw a heart", CancellationToken.None));
        Check.True(error.Message.Contains("three invalid"), "Repair loop must explain why it stopped");
        Check.Equal(3, repair.CompletionCount, "Bad geometry consumed more than two repair rounds");
        Check.Equal(3, invalid.Previews, "Preview failure budget differs from the visible stop reason");
        Check.Equal(0, invalid.Writes, "Invalid preparation executed geometry");
        var unrequestedSection = new InvalidPreview("topsolid_create_sketch_section");
        using var sectionProvider = new FakeAiProvider { Reply = (round, _, _) => Task.FromResult(round == 1
            ? FakeAiProvider.ToolReply(new AiToolCall { Id = "unrequested-section", Name = "topsolid_create_sketch_section", Arguments = new JObject() })
            : new AiReply { Content = "Use the ordinary sketch tools." }) };
        var ordinarySession = new ChatSession(sectionProvider, unrequestedSection) { ConfirmChangeAsync = (_, _) => throw new InvalidOperationException("Unrequested section reached confirmation") };
        await ordinarySession.SendAsync("Draw a rectangle sketch", CancellationToken.None);
        Check.Equal(0, unrequestedSection.Previews, "Model prepared a section without an explicit request");
        Check.Equal(0, unrequestedSection.Writes, "Model executed a section without an explicit request");
        var metrics = OllamaProvider.Metrics(JObject.Parse("{load_duration:1000000000,prompt_eval_duration:2000000000,eval_duration:500000000,total_duration:3500000000,prompt_eval_count:1200,eval_count:40}"), "gemma4:e4b");
        Check.Equal(1.0, (double)metrics["load_seconds"]!, "Ollama nanoseconds were not converted correctly");
        Check.Equal(80.0, (double)metrics["generatedTokensPerSecond"]!, "Token generation speed is wrong");
        Check.True(OllamaProvider.Metrics(new JObject(), "test")["load_seconds"] == null, "Missing metrics must not be reported as zero");
    }

    internal static void VerifyCatalog(McpToolDefinition[] catalog)
    {
        const string request = "인공지능 테스트2026 프로젝트 테스트파트에 하트 스케치를 그려줘";
        var local = new ToolExposure(catalog, request, compact: true);
        Check.True(local.Active.Any(t => t.Name == "topsolid_create_heart_sketch") && local.Active.Any(t => t.Name == "topsolid_get_modeling_context"), "Heart workflow requires avoidable discovery rounds");
        Check.True(!local.Active.Any(t => t.Name is "topsolid_create_sketches2d" or "topsolid_create_sketch_profiles"), "Local heart request includes redundant generic geometry schemas");
        Check.True(local.Active.Length <= 10, "Local initial tool budget exceeded");
        Check.True(!local.Catalog.Contains("topsolid_delete_elements"), "Local prompt retransmits full tool catalog");
        Check.True(!local.Select(JObject.Parse("{category:'Sketch2D'}")).IsError, "Local category discovery failed");
        Check.True(!local.Select(JObject.Parse("{names:['topsolid_create_sketches2d']}")).IsError && local.Active.Any(t => t.Name == "topsolid_create_sketches2d"), "Advanced associative sketch tools became inaccessible");
        var extrusion = new ToolExposure(catalog, "그 스케치를 돌출 시켜줘 100mm", compact: true);
        Check.True(extrusion.Active.Any(t => t.Name == "topsolid_extrude_sketch"), "Extrusion cannot access original sketch directly");
        Check.True(!extrusion.Active.Any(t => t.Name == "topsolid_list_sketch2d_sections"), "Extrusion still encourages unnecessary section discovery");
        foreach (var text in new[] { "Create a red cylinder", "Draw a slot sketch", "Create a sketch without sections", "Create a sketch, no sketch section", "Do not create a sketch section", "섹션은 만들지 마" }) {
            var ordinary = new ToolExposure(catalog, text);
            Check.True(!ordinary.Active.Any(t => t.Name == "topsolid_create_sketch_section") && !ordinary.Catalog.Contains("topsolid_create_sketch_section"), "Ordinary drawing exposed a section action: " + text);
            Check.True(ordinary.Select(JObject.Parse("{names:['topsolid_create_sketch_section']}")).IsError, "Model could load an unrequested section action");
        }
        foreach (var text in new[] { "Create a sketch section from these profiles", "이 스케치에 섹션을 생성해줘" }) {
            var explicitSection = new ToolExposure(catalog, text, compact: true);
            Check.True(explicitSection.Active.Any(t => t.Name == "topsolid_create_sketch_section"), "Explicit section request lost its separate tool");
        }
        var current = new ToolExposure(catalog, "Create a section\nDo not create sections", currentRequest: "Create a section");
        Check.True(current.Active.Any(t => t.Name == "topsolid_create_sketch_section"), "Old section preferences overrode an explicit current request");
        current = new ToolExposure(catalog, "Create a cylinder\nCreate a section", currentRequest: "Create a cylinder");
        Check.True(!current.Catalog.Contains("topsolid_create_sketch_section"), "Old section request leaked permission into ordinary modeling");
        var rotatedHeart = new ToolExposure(catalog, request + " 회전 0도. 섹션은 만들지 마. 저장은 하지 마.", compact: true);
        Check.True(!rotatedHeart.Active.Any(t => t.Name is "topsolid_revolve_sketch" or "topsolid_save_documents" or "topsolid_create_part_document"), "Sketch rotation/negative instructions inflated the local schema set with unwanted actions");
        Check.True(ModelInstructions.Build(local.Active, local.Catalog, true).Length < 6000, "Task instructions expanded beyond the local budget");
    }

    private sealed class InvalidPreview(string toolName = "topsolid_create_contour2d") : IConfirmableMcpClient
    {
        public int Previews, Writes;
        public bool IsConnected => true;
        public IReadOnlyList<McpToolDefinition> Tools { get; } = [new() { Name = toolName, Annotations = new JObject { ["readOnlyHint"] = false } }];
        public Task<McpToolResult> CallToolAsync(string name, JObject arguments, CancellationToken token) { Writes++; throw new InvalidOperationException("No raw writes"); }
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject arguments, string confirmationToken, CancellationToken token) { Writes++; throw new InvalidOperationException("No confirmed writes"); }
        public Task<JObject> PrepareToolAsync(string name, JObject arguments, CancellationToken token) { Previews++; throw new StdioMcpClient.McpRequestException( "Arc endpoints must lie on the same circle."); }
    }
}
