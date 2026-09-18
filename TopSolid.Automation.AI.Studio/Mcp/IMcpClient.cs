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
}
