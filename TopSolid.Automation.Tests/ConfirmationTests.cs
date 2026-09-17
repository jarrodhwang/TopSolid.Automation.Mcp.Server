using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class ConfirmationTests
{
    public static async Task ApprovalAndDenial()
    {
        foreach (var approve in new[] { true, false })
        {
            var client = new ConfirmableFake();
            using var provider = ChangeProvider();
            var shown = 0; var trace = new List<ChatTrace>();
            var session = new ChatSession(provider, client) { ConfirmChangeAsync = (proposal, _) =>
            {
                shown++;
                Check.True(proposal["confirmationToken"] == null, "Authorization token leaked to the visible proposal");
                Check.Equal("Exact target", (string?)proposal["target"]?["name"], "Target document missing from confirmation");
                return Task.FromResult(approve);
            } };
            session.Trace += trace.Add;
            await session.SendAsync("Add a rectangle.", CancellationToken.None);
            Check.Equal(1, shown, "Confirmation count");
            Check.Equal(approve ? 1 : 0, client.ConfirmedCalls, "A declined change reached MCP");
            Check.Equal(0, client.UnconfirmedCalls, "A write used the unconfirmed call path");
            Check.True(trace.All(t => !t.Text.Contains(ConfirmableFake.Token, StringComparison.Ordinal)), "Confirmation token leaked into model trace");
            Check.True(provider.Requests.SelectMany(r => r).All(m => !m.Content.Contains(ConfirmableFake.Token, StringComparison.Ordinal)), "Confirmation token sent to AI provider");
        }
        using (var provider = ChangeProvider())
        {
            var client = new ConfirmableFake();
            await new ChatSession(provider, client).SendAsync("No approval UI attached.", CancellationToken.None);
            Check.Equal(0, client.ConfirmedCalls, "Headless client auto-approved");
        }
        using (var provider = ChangeProvider())
        {
            var client = new ConfirmableFake { TamperPreview = true }; var shown = false;
            var session = new ChatSession(provider, client) { ConfirmChangeAsync = (_, _) => { shown = true; return Task.FromResult(true); } };
            await Check.ThrowsAsync<InvalidOperationException>(() => session.SendAsync("Reject changed proposal.", CancellationToken.None));
            Check.True(!shown && client.ConfirmedCalls == 0, "Tampered preview reached approval or execution");
        }
    }
    public static async Task DeclinedChangesAreNotRepeated()
    {
        var client = new ConfirmableFake(); var prompts = 0;
        using var provider = new FakeAiProvider { Reply = (round, _, _) => Task.FromResult(round <= 2
            ? FakeAiProvider.ToolReply(ChangeCall("call-" + round)) : new AiReply { Content = "Stopped." }) };
        var session = new ChatSession(provider, client) { ConfirmChangeAsync = (_, _) => { prompts++; return Task.FromResult(false); } };
        await session.SendAsync("One proposal only.", CancellationToken.None);
        Check.Equal(1, prompts, "Model repeated a declined confirmation dialog");
        Check.Equal(0, client.ConfirmedCalls, "Declined change executed");
        Check.Equal(1, provider.CompletionCount, "A declined confirmation must not wait for another model round");
        using var batchProvider = new FakeAiProvider { Reply = (_, _, _) => Task.FromResult(FakeAiProvider.ToolReply(ChangeCall("first"), ChangeCall("second"))) };
        var batch = new ChatSession(batchProvider, client) { ConfirmChangeAsync = (_, _) => Task.FromResult(false) };
        var answer = await batch.SendAsync("Propose two rectangles.", CancellationToken.None);
        Check.True(answer.Contains("declined"), "Decline must be rendered from the real decision");
        Check.Equal(2, batch.GetConversationSnapshot().Single().Count(m => m.Role == "tool"), "Decline must retain a receipt for each pending model call");
        Check.Equal(0, client.ConfirmedCalls, "Later calls executed after a decline");
    }
    public static async Task ChangeReceiptSurvivesFailedFollowUp()
    {
        var client = new ConfirmableFake();
        using var provider = new FakeAiProvider { Reply = (round, _, _) => round == 2
            ? Task.FromException<AiReply>(new IOException("Model disconnected after commit"))
            : Task.FromResult(round == 1 ? FakeAiProvider.ToolReply(ChangeCall("write-1")) : new AiReply { Content = "Prior change remembered." }) };
        var session = new ChatSession(provider, client) { ConfirmChangeAsync = (_, _) => Task.FromResult(true) };
        await Check.ThrowsAsync<IOException>(() => session.SendAsync("Create it.", CancellationToken.None));
        await session.SendAsync("What happened?", CancellationToken.None);
        Check.True(provider.Requests.Last().Any(m => m.Role == "tool" && m.Content.Contains("nativeCreated", StringComparison.Ordinal)), "Committed result was forgotten after model failure");
        Check.Equal(1, client.ConfirmedCalls, "Change was automatically retried");
    }
    private static FakeAiProvider ChangeProvider() => new() { Reply = (round, _, _) => Task.FromResult(round == 1
        ? FakeAiProvider.ToolReply(ChangeCall("model-change")) : new AiReply { Content = "Result received." }) };
    private static AiToolCall ChangeCall(string id) => new() { Id = id, Name = "topsolid_create_rectangle2d", Arguments = new JObject { ["documentId"] = "exact-revision", ["width"] = 20, ["height"] = 10, ["placement"] = "2d" } };

    private sealed class ConfirmableFake : IConfirmableMcpClient
    {
        public const string Token = "test-only-confirmation-secret";
        public bool IsConnected => true;
        public bool TamperPreview { get; set; }
        public int UnconfirmedCalls { get; private set; }
        public int ConfirmedCalls { get; private set; }
        public IReadOnlyList<McpToolDefinition> Tools { get; } = [new() { Name = "topsolid_create_rectangle2d", Annotations = new JObject { ["readOnlyHint"] = false }, InputSchema = new JObject { ["type"] = "object" } }];
        public Task<JObject> PrepareToolAsync(string name, JObject arguments, CancellationToken cancellationToken)
        {
            var args = (JObject)arguments.DeepClone(); if (TamperPreview) args["documentId"] = "wrong-document";
            return Task.FromResult(new JObject { ["confirmationToken"] = Token, ["toolName"] = name, ["arguments"] = args,
                ["target"] = new JObject { ["documentId"] = "exact-revision", ["name"] = "Exact target" } });
        }
        public Task<McpToolResult> CallToolAsync(string name, JObject arguments, CancellationToken cancellationToken)
        { UnconfirmedCalls++; return Task.FromResult(McpToolResult.Error("Unconfirmed write attempted")); }
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject arguments, string confirmationToken, CancellationToken cancellationToken)
        {
            Check.Equal(Token, confirmationToken, "Wrong out-of-band confirmation token"); ConfirmedCalls++;
            return Task.FromResult(new McpToolResult { Content = new JArray(new JObject { ["type"] = "text", ["text"] = "{\"nativeCreated\":true,\"saved\":false}" }) });
        }
    }
}
