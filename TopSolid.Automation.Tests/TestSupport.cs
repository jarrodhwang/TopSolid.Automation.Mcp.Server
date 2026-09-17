using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class Check
{
    public static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}; received {actual}.");
    }

    public static T Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T exception) { return exception; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    public static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T exception) { return exception; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}

internal sealed record CapturedRequest(Uri Uri, string? AuthorizationScheme,
    string? AuthorizationParameter, JObject? Body, IReadOnlyDictionary<string, string> Headers);

internal sealed class ScriptedHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> responses = new();
    public List<CapturedRequest> Requests { get; } = [];

    public ScriptedHandler Json(string response, HttpStatusCode status = HttpStatusCode.OK)
    {
        responses.Enqueue((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json")
        }));
        return this;
    }

    public ScriptedHandler PendingUntilCancelled()
    {
        responses.Enqueue(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("A cancelled request should not complete.");
        });
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(new CapturedRequest(request.RequestUri!, request.Headers.Authorization?.Scheme,
            request.Headers.Authorization?.Parameter,
            request.Content == null ? null : JObject.Parse(await request.Content.ReadAsStringAsync(cancellationToken)),
            request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase)));
        if (responses.Count == 0) throw new InvalidOperationException("Unexpected provider request.");
        return await responses.Dequeue()(request, cancellationToken);
    }
}

internal sealed class FakeMcpClient : IMcpClient
{
    public const string StatusName = "topsolid_connection_status";
    public bool IsConnected { get; set; } = true;
    public IReadOnlyList<McpToolDefinition> Tools { get; set; } = [new()
    {
        Name = StatusName,
        Description = "Get the current TopSolid Automation connection state.",
        Annotations = new JObject { ["readOnlyHint"] = true },
        InputSchema = JObject.Parse("{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}")
    }];
    public List<(string Name, JObject Arguments)> Calls { get; } = [];
    public Func<CancellationToken, Task<McpToolResult>>? OnCall { get; set; }

    public async Task<McpToolResult> CallToolAsync(string name, JObject arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add((name, (JObject)arguments.DeepClone()));
        if (OnCall != null) return await OnCall(cancellationToken);
        return new McpToolResult
        {
            Content = new JArray(new JObject { ["type"] = "text", ["text"] = "{\"connected\":true}" }),
            StructuredContent = new JObject { ["connected"] = true }
        };
    }
}

internal sealed class FakeAiProvider : IAiProvider
{
    public int CompletionCount { get; private set; }
    public List<AiMessage[]> Requests { get; } = [];
    public List<string[]> SuppliedTools { get; } = [];
    public Func<int, IReadOnlyList<AiMessage>, CancellationToken, Task<AiReply>> Reply { get; set; }
        = (_, _, _) => Task.FromResult(new AiReply { Content = "Ready." });

    public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<string>>(["fake-model"]);

    public Task<AiReply> CompleteAsync(IReadOnlyList<AiMessage> messages,
        IReadOnlyList<McpToolDefinition> tools, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SuppliedTools.Add(tools.Select(t => t.Name).ToArray());
        Requests.Add(messages.Select(m => new AiMessage
        {
            Role = m.Role, Content = m.Content, ToolCallId = m.ToolCallId, ToolName = m.ToolName,
            Thinking = m.Thinking, ToolCalls = m.ToolCalls.ToArray()
        }).ToArray());
        return Reply(++CompletionCount, messages, cancellationToken);
    }

    public void Dispose() { }

    public static AiReply ToolReply(params AiToolCall[] calls) => new() { ToolCalls = calls };
    public static AiToolCall StatusCall(string id = "status-1") => new()
        { Id = id, Name = FakeMcpClient.StatusName, Arguments = new JObject() };
}
