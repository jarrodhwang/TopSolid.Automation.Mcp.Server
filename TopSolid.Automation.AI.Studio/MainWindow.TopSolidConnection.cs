using System.IO;
using TopSolid.Automation.AI.Studio.Connections;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio;

public partial class MainWindow
{
    private async Task ConnectConfiguredTopSolid(CancellationToken token)
    {
        if (session != null)
        {
            // Reconnecting can resolve an automatic target to a newly started process.
            // Keep the transcript, but never replay native identities from the previous transport.
            session.Clear(); session = null; sessionConfiguration = null;
            provider?.Dispose(); provider = null; responsePresenter.Clear();
            RecordChat("System", StudioStrings.Get("TsConnection.ContextReset"));
        }
        try
        {
            await mcp.ConnectAsync(settings.McpServerPath, settings.TopSolidConnection, settings.TopSolidGatewayToken, token);
            var license = await mcp.GetLicenseStatusAsync(token);
            if (!license.CanStart) throw new IOException(StudioStrings.Get("License.Denied"));
            startupLicenseStatus = license;
            reconnectMcpOnRefresh = false;
            lastDiscoveredTools = CloneTools(mcp.Tools);
            connectionHealth.Set("Mcp", ConnectionSeverity.Ready, "Health.McpReady");
            connectionHealth.Set("TopSolid", ConnectionSeverity.Ready, "Health.TopSolidReady");
        }
        catch
        {
            await mcp.DisconnectAsync(); startupLicenseStatus = null;
            connectionHealth.Set("TopSolid", ConnectionSeverity.Error, "TsConnection.Failed");
            throw;
        }
    }
}
