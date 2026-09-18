using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Connections;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Settings;

namespace TopSolid.Automation.AI.Studio;

public partial class MainWindow
{
    private readonly ConnectionHealth connectionHealth = new();
    private readonly DispatcherTimer connectionTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly RotateTransform refreshRotation = new();
    private ConnectionStatusWindow? connectionDialog;
    private bool refreshRunning, refreshSpinning, startupConnectionStarted, reconnectMcpOnRefresh;
    private long modelRequestStartedTicks;

    private void InitializeConnectionIndicator()
    {
        RefreshIcon.RenderTransform = refreshRotation;
        connectionHealth.Changed += UpdateConnectionIndicator;
        connectionTimer.Tick += (_, _) =>
        {
            var modelStarted = Interlocked.Read(ref modelRequestStartedTicks);
            if (modelStarted > 0) connectionHealth.Pending("AiRuntime", "Health.AiPending", new DateTimeOffset(modelStarted, TimeSpan.Zero));
            else connectionHealth.FinishPending("AiRuntime");
            connectionHealth.Tick(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(8));
            UpdateConnectionIndicator();
            if (!refreshRunning && modelStarted == 0) connectionTimer.Stop();
        };
        UpdateConnectionIndicator();
    }

    private void InvalidateAiHealth()
    {
        if (loading || refreshRunning) return;
        connectionHealth.Remove("AiRuntime");
        var missing = string.IsNullOrWhiteSpace(ModelBox.Text) || string.IsNullOrWhiteSpace(EndpointBox.Text) ||
            (visibleProvider != "Ollama" && CloudServiceBox.SelectedValue as string != "custom" && string.IsNullOrWhiteSpace(ApiKeyBox.Password));
        connectionHealth.Set("Ai", missing ? ConnectionSeverity.Error : ConnectionSeverity.Warning, missing ? "Health.AiConfiguration" : "Health.AiChanged");
    }

    private void UpdateConnectionIndicator()
    {
        if (RefreshIcon == null || closing) return;
        var severity = connectionHealth.Severity;
        RefreshIcon.Source = TopSolidIcons.Get(severity == ConnectionSeverity.Error ? "refresh-error" : severity == ConnectionSeverity.Warning ? "refresh-warning" : "refresh");
        var spin = (refreshRunning || Interlocked.Read(ref modelRequestStartedTicks) > 0) && severity <= ConnectionSeverity.Pending;
        if (spin != refreshSpinning)
        {
            refreshSpinning = spin;
            refreshRotation.BeginAnimation(RotateTransform.AngleProperty, spin ? new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.15))
                { RepeatBehavior = RepeatBehavior.Forever } : null);
            if (!spin) refreshRotation.Angle = 0;
        }
        var label = StudioStrings.Get(severity switch
        {
            ConnectionSeverity.Error => "Health.Error", ConnectionSeverity.Warning => "Health.Warning",
            ConnectionSeverity.Pending => "Health.Checking", _ => "Common.RefreshComplete"
        });
        RefreshButton.ToolTip = label;
        AutomationProperties.SetName(RefreshButton, label);
        AutomationProperties.SetHelpText(RefreshButton, string.Join("\n", connectionHealth.Conditions.Select(c => StudioStrings.Get(c.MessageKey))));
        RefreshButton.IsEnabled = !closing;
        connectionDialog?.Update(connectionHealth, operation == null && !refreshRunning && !mcp.IsMutationInFlight);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (closing || connectionDialog != null) return;
        // Keep license details reachable even when every connection is healthy.
        {
            var dialog = new ConnectionStatusWindow(startupLicenseStatus) { Owner = this };
            connectionDialog = dialog;
            dialog.Update(connectionHealth, operation == null && !refreshRunning && !mcp.IsMutationInFlight);
            bool retry;
            try { retry = dialog.ShowDialog() == true; }
            finally { connectionDialog = null; }
            if (!retry) return;
        }
        await RefreshConnectionsAsync();
    }

    private async Task RefreshConnectionsAsync()
    {
        if (operation != null || closing || refreshRunning) return;
        refreshRunning = true;
        foreach (var component in new[] { "Mcp", "TopSolid", "Ai" })
        {
            connectionHealth.Remove(component);
            if (component == "TopSolid") connectionHealth.Set(component, ConnectionSeverity.Pending, "Health.TopSolidWaiting");
            else connectionHealth.Pending(component, "Health." + component + "Checking");
        }
        connectionTimer.Start(); UpdateConnectionIndicator();
        try
        {
            await RunOperation(async token =>
            {
                ReadProvider(); themeFollower?.Refresh();
                // Independent checks: one service must not block the other's diagnosis.
                await Task.WhenAll(CheckMcpAndTopSolid(token), CheckAiConnection(token));
                if (closing) return;
                SettingsFeedback.Text = StudioStrings.Get(connectionHealth.HasProblems ? "Common.RefreshIncomplete" : "Common.RefreshComplete");
                RecordTrace("Refresh", SettingsFeedback.Text);
                RefreshDeveloper();
            });
        }
        finally
        {
            refreshRunning = false;
            connectionHealth.CancelPending();
            if (Interlocked.Read(ref modelRequestStartedTicks) == 0) connectionTimer.Stop();
            UpdateConnectionIndicator();
        }
    }

    private async Task CheckMcpAndTopSolid(CancellationToken token)
    {
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            deadline.CancelAfter(TimeSpan.FromSeconds(20));
            try
            {
                if (reconnectMcpOnRefresh && mcp.IsConnected) await mcp.DisconnectAsync();
                if (!mcp.IsConnected) await mcp.ConnectAsync(settings.McpServerPath, deadline.Token);
                reconnectMcpOnRefresh = false;
                lastDiscoveredTools = CloneTools(mcp.Tools);
                connectionHealth.Set("Mcp", ConnectionSeverity.Ready, "Health.McpReady");
                connectionHealth.Pending("TopSolid", "Health.TopSolidChecking");
            }
            catch (Exception error)
            {
                if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                RecordTrace("Refresh error", error.Message);
                connectionHealth.Set("Mcp", ConnectionSeverity.Error, "Health.McpUnavailable");
                connectionHealth.Set("TopSolid", ConnectionSeverity.Warning, "Health.TopSolidBlocked");
                return;
            }
        }
        using var hostDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        hostDeadline.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var result = await mcp.CallToolAsync("topsolid_get_status", new JObject(), hostDeadline.Token);
                RecordTrace(result.IsError ? "TopSolid error" : "TopSolid status", JsonConvert.SerializeObject(result, Formatting.Indented));
                if (TopSolidConnectionStatus.IsConnected(result))
                {
                    connectionHealth.Set("TopSolid", ConnectionSeverity.Ready, "Health.TopSolidReady");
                    return;
                }
                var availability = await Task.Run(TopSolidInstallation.Inspect, hostDeadline.Token);
                hostDeadline.Token.ThrowIfCancellationRequested();
                connectionHealth.Set("TopSolid", availability == TopSolidAvailability.NotFound ? ConnectionSeverity.Error : ConnectionSeverity.Warning,
                    availability switch
                    {
                        TopSolidAvailability.NotFound => "Health.TopSolidNotFound", TopSolidAvailability.NotRunning => "Health.TopSolidNotRunning",
                        TopSolidAvailability.NotResponding => "Health.TopSolidNotResponding", _ => "Health.TopSolidStarting"
                    });
                if (availability is not (TopSolidAvailability.Starting or TopSolidAvailability.NotResponding) || attempt == 2) return;
                await Task.Delay(TimeSpan.FromSeconds(2), hostDeadline.Token);
            }
        }
        catch (Exception error)
        {
            if (token.IsCancellationRequested) throw new OperationCanceledException(token);
            reconnectMcpOnRefresh = true;
            RecordTrace("Refresh error", error.Message);
            connectionHealth.Set("TopSolid", ConnectionSeverity.Warning, "Health.TopSolidNotResponding");
            if (!mcp.IsConnected) connectionHealth.Set("Mcp", ConnectionSeverity.Error, "Health.McpUnavailable");
        }
    }

    private async Task CheckAiConnection(CancellationToken token)
    {
        var selected = settings.Provider == AppSettings.OllamaProvider ? settings.OllamaModel : settings.CloudModel;
        if (string.IsNullOrWhiteSpace(selected) || (settings.Provider != AppSettings.OllamaProvider && settings.CloudService != "custom" && string.IsNullOrWhiteSpace(settings.ApiKey)))
        {
            modelStatus = "not configured";
            connectionHealth.Set("Ai", ConnectionSeverity.Error, "Health.AiConfiguration");
            return;
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var models = await RefreshModelList(deadline.Token, openSelector: false);
            var available = models.Contains(selected, StringComparer.Ordinal) ||
                (settings.Provider == AppSettings.OllamaProvider && !selected.Contains(':') && models.Contains(selected + ":latest", StringComparer.Ordinal));
            connectionHealth.Set("Ai", available ? ConnectionSeverity.Ready : ConnectionSeverity.Warning, available ? "Health.AiReady" : "Health.AiNotListed");
            // Discovery cannot prove that an earlier inference quota error has recovered.
        }
        catch (Exception error)
        {
            if (token.IsCancellationRequested) throw new OperationCanceledException(token);
            modelStatus = "model listing failed";
            RecordTrace("Refresh error", error.Message);
            var failure = ConnectionHealth.AiFailure(error);
            if (error is AiProviderException { StatusCode: System.Net.HttpStatusCode.NotFound })
                failure = (ConnectionSeverity.Warning, "Health.AiCatalogUnavailable");
            connectionHealth.Set("Ai", failure.Severity, failure.Key);
        }
    }

    private async Task<IReadOnlyList<string>> RefreshModelList(CancellationToken token, bool openSelector)
    {
        modelStatus = "listing models";
        UpdateStatus();
        using var candidate = ProviderFactory.Create(settings);
        var models = await candidate.ListModelsAsync(token);
        token.ThrowIfCancellationRequested();
        var selected = ModelBox.Text;
        ModelBox.ItemsSource = models;
        ModelBox.Text = selected;
        modelStatus = $"{models.Count} models listed; inference not tested";
        RecordTrace("Models", modelStatus);
        SettingsFeedback.Text = LocalizedModelStatus();
        if (openSelector)
            _ = Dispatcher.BeginInvoke(new Action(() => { if (!closing) SettingsModelBox.IsDropDownOpen = models.Count > 0; }));
        return models;
    }

    private string LocalizedModelStatus()
    {
        const string suffix = " models listed; inference not tested";
        if (modelStatus.EndsWith(suffix, StringComparison.Ordinal) && int.TryParse(modelStatus[..^suffix.Length], out var count))
            return string.Format(StudioStrings.Text("{0} models listed; inference not tested"), count);
        return StudioStrings.Text(modelStatus);
    }
}
