using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Mcp;

public interface IMcpClient
{
    bool IsConnected { get; }
    IReadOnlyList<McpToolDefinition> Tools { get; }
    Task<McpToolResult> CallToolAsync(string name, JObject arguments, CancellationToken cancellationToken);
}

public interface IConfirmableMcpClient : IMcpClient
{
    Task<JObject> PrepareToolAsync(string name, JObject arguments, CancellationToken cancellationToken);
    Task<McpToolResult> CallConfirmedToolAsync(string name, JObject arguments, string confirmationToken, CancellationToken cancellationToken);
}

/// <summary>Local display data; never included in model tools, conversation history or approval authority.</summary>
public interface IGraphicPreviewClient
{
    Task<JObject> GetGraphicPreviewAsync(JObject target, CancellationToken cancellationToken);
    async Task<GraphicPreviewData> GetGraphicPreviewDataAsync(JObject target, CancellationToken token)
    {
        var metadata = await GetGraphicPreviewAsync(target, token);
        var data = (string?)metadata["data"];
        if (data != null && data.Length > GraphicPreviewQuality.MaximumRpcLineCharacters) throw new System.IO.InvalidDataException("Oversized inline preview.");
        return new GraphicPreviewData(metadata, data == null ? null : Convert.FromBase64String(data));
    }
}

public sealed record GraphicPreviewData(JObject Metadata, byte[]? Bytes);
public interface IToolpathPreviewClient
{
    Task<JObject> GetToolpathPreviewAsync(JObject operation, CancellationToken token);
}
