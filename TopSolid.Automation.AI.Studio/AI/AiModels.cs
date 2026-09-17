using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.AI;

public interface IAiProvider : IDisposable
{
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken);
    Task<AiReply> CompleteAsync(IReadOnlyList<AiMessage> messages,
        IReadOnlyList<McpToolDefinition> tools, CancellationToken cancellationToken);
}

public sealed class AiMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
    public string? Thinking { get; set; }
    public JArray? ProviderContent { get; set; }
    public JObject? ExtraContent { get; set; }
    public IReadOnlyList<AiToolCall> ToolCalls { get; set; } = Array.Empty<AiToolCall>();
}

public sealed class AiToolCall
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public JObject Arguments { get; set; } = new();
    // Opaque Gemini thought signatures must survive the tool round trip.
    public JObject? ExtraContent { get; set; }

    // Keep malformed calls in the conversation so the application can return a
    // tool error to the model. Never execute a call with ArgumentsError set.
    public string? ArgumentsError { get; set; }
    public string? RawArguments { get; set; }
}

public sealed class AiReply
{
    public string Content { get; set; } = "";
    public string? Thinking { get; set; }
    public JArray? ProviderContent { get; set; }
    public JObject? ExtraContent { get; set; }
    public IReadOnlyList<AiToolCall> ToolCalls { get; set; } = Array.Empty<AiToolCall>();
}

public sealed class AiProviderException : Exception
{
    public AiProviderException(string message) : base(message) { }
}
