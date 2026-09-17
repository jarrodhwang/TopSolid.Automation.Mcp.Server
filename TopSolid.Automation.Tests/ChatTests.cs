using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class ChatTests
{
    public static async Task TextConversationAndClear()
    {
        using var provider = new FakeAiProvider();
        var client = new FakeMcpClient { IsConnected = false, Tools = [] };
        var session = new ChatSession(provider, client);
        Check.Equal("Ready.", await session.SendAsync("Hello.", CancellationToken.None), "Text response");
        await session.SendAsync("Remember the prior greeting.", CancellationToken.None);
        Check.True(provider.Requests[1].Any(m => m.Role == "user" && m.Content == "Hello."), "User history was lost");
        Check.True(provider.Requests[1].Any(m => m.Role == "assistant" && m.Content == "Ready."), "Assistant history was lost");
        var snapshot = session.GetConversationSnapshot();
        Check.Equal(2, snapshot.Count, "Conversation export did not retain completed turns");
        Check.True(snapshot[0].Any(m => m.Role == "assistant" && m.Content == "Ready."), "Conversation export lost assistant content");
        session.Clear();
        await session.SendAsync("New conversation.", CancellationToken.None);
        Check.True(provider.Requests[2].All(m => m.Content != "Hello."), "Clear did not reset history");
        Check.Equal(0, client.Calls.Count, "Ordinary text should not invoke MCP");
    }

    public static async Task InvalidCallsAreReturnedToModel()
    {
        using var provider = new FakeAiProvider
        {
            Reply = (round, _, _) => Task.FromResult(round == 1
                ? FakeAiProvider.ToolReply(
                    new AiToolCall { Id = "unknown", Name = "invented_cad_function", Arguments = new JObject() },
                    new AiToolCall { Id = "malformed", Name = FakeMcpClient.StatusName,
                        RawArguments = "{invalid", ArgumentsError = "Malformed JSON arguments." })
                : new AiReply { Content = "These tool requests failed." })
        };
        var client = new FakeMcpClient();
        var session = new ChatSession(provider, client);
        Check.Equal("These tool requests failed.", await session.SendAsync("Check connection.", CancellationToken.None),
            "Model should receive tool errors and complete");
        Check.Equal(0, client.Calls.Count, "Invalid calls reached MCP");
        var results = provider.Requests[1].Where(m => m.Role == "tool").ToArray();
        Check.Equal(2, results.Length, "Each invalid tool call needs its own response");
        Check.True(results.Any(m => m.ToolCallId == "unknown"), "Unknown tool call correlation was lost");
        Check.True(results.Any(m => m.ToolCallId == "malformed"), "Malformed tool call correlation was lost");
        Check.True(results.All(m => m.Content.Contains("error", StringComparison.OrdinalIgnoreCase)),
            "Invalid calls must be represented as errors");
    }

    public static async Task MultipleToolsAndToolFailure()
    {
        using var provider = new FakeAiProvider
        {
            Reply = (round, _, _) => Task.FromResult(round == 1
                ? FakeAiProvider.ToolReply(FakeAiProvider.StatusCall("first"), FakeAiProvider.StatusCall("second"))
                : new AiReply { Content = "TopSolid reported a failure." })
        };
        var client = new FakeMcpClient
        {
            OnCall = _ => Task.FromResult(McpToolResult.Error("TopSolid is unavailable in this test."))
        };
        var session = new ChatSession(provider, client);
        var traces = new List<ChatTrace>();
        session.Trace += traces.Add;
        await session.SendAsync("Check connection twice.", CancellationToken.None);
        Check.Equal(2, client.Calls.Count, "All tools in the model reply should be executed");
        var results = provider.Requests[1].Where(m => m.Role == "tool").ToArray();
        Check.Equal(2, results.Length, "Each executed tool needs a result");
        Check.Equal("first", results[0].ToolCallId, "First tool result correlation");
        Check.Equal("second", results[1].ToolCallId, "Second tool result correlation");
        Check.True(results.All(m => m.Content.Contains("TopSolid is unavailable", StringComparison.Ordinal)),
            "Actual tool failure evidence was lost");
        Check.True(traces.Any(t => t.Text.Contains(FakeMcpClient.StatusName, StringComparison.Ordinal)),
            "Tool activity should be visible in trace");
        Check.True(traces.Any(t => t.Text.Contains("TopSolid is unavailable", StringComparison.Ordinal)),
            "Tool result should be visible in trace");
    }

    public static async Task CancelDuringToolThenRecover()
    {
        var enteredTool = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var provider = new FakeAiProvider
        {
            Reply = (round, _, _) => Task.FromResult(round == 1
                ? FakeAiProvider.ToolReply(FakeAiProvider.StatusCall())
                : new AiReply { Content = "Recovered." })
        };
        var client = new FakeMcpClient
        {
            OnCall = async token =>
            {
                enteredTool.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("Cancellation should interrupt the pending tool.");
            }
        };
        var session = new ChatSession(provider, client);
        using var cancellation = new CancellationTokenSource();
        var pending = session.SendAsync("Cancel this tool call.", cancellation.Token);
        await enteredTool.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Check.ThrowsAsync<OperationCanceledException>(() => pending);
        Check.Equal(1, provider.CompletionCount, "Cancellation should not request another model response");
        Check.Equal("Recovered.", await session.SendAsync("Try again.", CancellationToken.None),
            "Cancelled conversation should accept a new turn");
        var recovery = provider.Requests[1];
        foreach (var message in recovery.Where(m => m.Role == "assistant" && m.ToolCalls.Count > 0))
            foreach (var call in message.ToolCalls)
                Check.True(recovery.Any(m => m.Role == "tool" && m.ToolCallId == call.Id),
                    "Cancellation left an unmatched tool call in provider history");
    }

    public static async Task RepeatedToolsAreBounded()
    {
        using var provider = new FakeAiProvider
        {
            Reply = (round, _, _) => Task.FromResult(FakeAiProvider.ToolReply(FakeAiProvider.StatusCall("repeat-" + round)))
        };
        var client = new FakeMcpClient();
        var session = new ChatSession(provider, client);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Check.ThrowsAsync<InvalidOperationException>(() => session.SendAsync("Repeat indefinitely.", timeout.Token));
        Check.True(provider.CompletionCount is > 0 and <= 16, "Unbounded provider/tool loop");
        Check.True(client.Calls.Count is > 0 and <= 16, "Unbounded MCP calls");
    }

    public static async Task OversizedToolBatchIsRejectedBeforeDispatch()
    {
        using var provider = new FakeAiProvider
        {
            Reply = (_, _, _) => Task.FromResult(FakeAiProvider.ToolReply(
                Enumerable.Range(1, 25).Select(index => FakeAiProvider.StatusCall("batch-" + index)).ToArray()))
        };
        var client = new FakeMcpClient();
        var session = new ChatSession(provider, client);
        var error = await Check.ThrowsAsync<InvalidOperationException>(() => session.SendAsync("Call too many tools.", CancellationToken.None));
        Check.True(error.Message.Contains("24", StringComparison.Ordinal), "Tool batch failure should explain its limit");
        Check.Equal(0, client.Calls.Count, "An oversized model tool batch must not partially execute");
    }

    public static async Task LargeToolOutputIsBounded()
    {
        using var provider = new FakeAiProvider
        {
            Reply = (round, _, _) => Task.FromResult(round == 1
                ? FakeAiProvider.ToolReply(FakeAiProvider.StatusCall())
                : new AiReply { Content = "The result was too large." })
        };
        var client = new FakeMcpClient
        {
            OnCall = _ => Task.FromResult(new McpToolResult
            {
                Content = new JArray(new JObject { ["type"] = "text", ["text"] = new string('x', 65000) })
            })
        };
        await new ChatSession(provider, client).SendAsync("Read large result.", CancellationToken.None);
        var result = provider.Requests[1].Single(m => m.Role == "tool").Content;
        Check.True(result.Length < 64000, "Excessive tool output was sent to the model");
        Check.True(JObject.Parse(result).Value<bool>("isError"), "Output size failure should be a tool error");
    }

    public static async Task DuplicateCallIdsAreRejectedBeforeDispatch()
    {
        using var provider = new FakeAiProvider
        {
            Reply = (_, _, _) => Task.FromResult(FakeAiProvider.ToolReply(
                FakeAiProvider.StatusCall("duplicate"), FakeAiProvider.StatusCall("duplicate")))
        };
        var client = new FakeMcpClient();
        var session = new ChatSession(provider, client);
        await Check.ThrowsAsync<InvalidOperationException>(() => session.SendAsync("Check twice.", CancellationToken.None));
        Check.Equal(0, client.Calls.Count, "A batch with ambiguous result IDs must not partially execute");
    }
}
