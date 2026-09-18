using Newtonsoft.Json;
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
    // Original typed intent stays separate from untrusted attachment text.
    public string? UserIntent { get; set; }
    public IReadOnlyList<AiImage> Images { get; set; } = Array.Empty<AiImage>();
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
    public string? Thinking { get; set; }
    public JArray? ProviderContent { get; set; }
    public JObject? ExtraContent { get; set; }
    public IReadOnlyList<AiToolCall> ToolCalls { get; set; } = Array.Empty<AiToolCall>();
}

/// <summary>Immutable image snapshot; diagnostic JSON contains metadata, never image bytes.</summary>
public sealed class AiImage
{
    internal AiImage(string name, string mediaType, int byteLength, string sha256, int width, int height, string base64)
    { Name = name; MediaType = mediaType; ByteLength = byteLength; Sha256 = sha256; Width = width; Height = height; Base64 = base64; }
    public string Name { get; }
    public string MediaType { get; }
    public int ByteLength { get; }
    public string Sha256 { get; }
    public int Width { get; }
    public int Height { get; }
    [JsonIgnore] internal string Base64 { get; }
}

public sealed class AiToolCall
{
    // Set only by Studio when it actually performed a deterministic context read.
    // Provider response parsing never accepts this flag from the model.
    public bool ClientInitiated { get; set; }
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
    public JObject? Metrics { get; set; }
    public string Content { get; set; } = "";
    public string? Thinking { get; set; }
    public JArray? ProviderContent { get; set; }
    public JObject? ExtraContent { get; set; }
    public IReadOnlyList<AiToolCall> ToolCalls { get; set; } = Array.Empty<AiToolCall>();
}

public sealed class AiProviderException : Exception
{
    public AiProviderException(string message, System.Net.HttpStatusCode? statusCode = null) : base(message) { StatusCode = statusCode; }
    public System.Net.HttpStatusCode? StatusCode { get; }
}
