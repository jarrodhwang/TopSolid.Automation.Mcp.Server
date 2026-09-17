using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Diagnostics;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio;

public partial class MainWindow : Window
{
    private readonly DiagnosticLog diagnosticLog = App.DiagnosticLog;
    private readonly SettingsStore settingsStore = new();
    private readonly StdioMcpClient mcp = new();
    private readonly SessionLog sessionLog = new();
    private IReadOnlyList<McpToolDefinition> lastDiscoveredTools = [];
    private AppSettings settings = new();
    private IAiProvider? provider;
    private ChatSession? session;
    private string? sessionConfiguration;
    private CancellationTokenSource? operation;
    private bool loading = true;
    private bool closing;
    private bool closeReady;
    private string visibleProvider = "OpenAI-compatible";
    private string modelStatus = "not tested";
    private readonly Stopwatch chatClock = new();
    private readonly DispatcherTimer elapsedTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };

    public MainWindow()
    {
        InitializeComponent();
        elapsedTimer.Tick += (_, _) => ElapsedText.Text = "Elapsed " + ChatTranscript.Elapsed(chatClock.Elapsed.TotalMilliseconds);
        settings = settingsStore.Load();
        TimeoutBox.ItemsSource = Enumerable.Range(1, 60);
        TimeoutBox.SelectedItem = settings.RequestTimeoutMinutes;
        FastGptOssBox.IsChecked = settings.OllamaFastGptOss;
        CloudServiceBox.ItemsSource = CloudServices.All;
        var configuredServer = settings.McpServerPath;
        settings.McpServerPath = AppSettings.ResolveServerPath(configuredServer, AppContext.BaseDirectory);
        visibleProvider = settings.Provider;
        ProviderBox.SelectedIndex = visibleProvider == "Ollama" ? 1 : 0;
        ShowProvider();
        ServerPathBox.Text = settings.McpServerPath;
        loading = false;
        UpdateLogSecrets();
        mcp.Diagnostic += message => RecordTrace("Server", message);
        mcp.ConnectionChanged += () =>
        {
            if (mcp.Tools.Count > 0) lastDiscoveredTools = CloneTools(mcp.Tools);
            diagnosticLog.Write("info", "mcp.connectionChanged", data: new JObject
            {
                ["connected"] = mcp.IsConnected,
                ["toolCount"] = mcp.Tools.Count,
                ["mutationInFlight"] = mcp.IsMutationInFlight
            });
            OnUi(UpdateStatus);
        };
        if (!string.IsNullOrEmpty(settingsStore.LastLoadWarning)) RecordTrace("Settings", settingsStore.LastLoadWarning);
        if (!string.Equals(configuredServer, settings.McpServerPath, StringComparison.OrdinalIgnoreCase))
            RecordTrace("Settings", "Using the MCP server bundled with this Studio version. Save settings to persist its location.");
        diagnosticLog.Write("info", "studio.windowInitialized", data: new JObject
        {
            ["settingsFilePath"] = settingsStore.FilePath,
            ["mcpServerPath"] = settings.McpServerPath
        });
        WriteConfigurationDiagnostic("configuration.loaded");
        RecordTrace("Log", "Current application-session diagnostics: " + diagnosticLog.CurrentLogFilePath +
            "; all-history Studio diagnostics: " + diagnosticLog.AllLogFilePath +
            "; MCP server all-history diagnostics: " + DiagnosticLog.GetAllHistoryLogFilePath("mcp-server"));
        UpdateStatus();
    }

    private void OnUi(Action action)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
        Dispatcher.BeginInvoke(action);
    }

    private void ShowProvider()
    {
        var local = visibleProvider == "Ollama";
        FastGptOssBox.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        CloudServiceBox.SelectedValue = settings.CloudService;
        CloudServiceBox.Visibility = CloudServiceLabel.Visibility = local ? Visibility.Collapsed : Visibility.Visible;
        EndpointBox.Text = local ? settings.OllamaServerUrl : settings.CloudBaseUrl;
        EndpointBox.IsReadOnly = !local && settings.CloudService != CloudServices.Custom;
        EndpointBox.ToolTip = EndpointBox.IsReadOnly ? "Service URL is filled automatically. Choose Custom OpenAI-compatible to use another endpoint." : null;
        ModelBox.ItemsSource = null;
        ModelBox.Text = local ? settings.OllamaModel : settings.CloudModel;
        ApiKeyBox.Password = local ? "" : settings.ApiKey;
        ApiKeyBox.IsEnabled = !local;
        ApiKeyBox.ToolTip = local ? null : CloudServices.Get(settings.CloudService).KeyHint;
    }

    private void ReadProvider()
    {
        if (visibleProvider == "Ollama")
        {
            settings.OllamaServerUrl = EndpointBox.Text.Trim();
            settings.OllamaModel = ModelBox.Text.Trim();
        }
        else
        {
            settings.CloudBaseUrl = EndpointBox.Text.Trim();
            settings.ApiKey = ApiKeyBox.Password;
            settings.CloudModel = ModelBox.Text.Trim();
        }
        settings.Provider = visibleProvider;
        settings.RequestTimeoutMinutes = TimeoutBox.SelectedItem is int minutes ? minutes : AppSettings.DefaultRequestTimeoutMinutes;
        settings.OllamaFastGptOss = FastGptOssBox.IsChecked == true;
        settings.McpServerPath = ServerPathBox.Text.Trim();
        UpdateLogSecrets();
    }

    private void Provider_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loading) return;
        ReadProvider();
        visibleProvider = ProviderBox.SelectedIndex == 1 ? "Ollama" : "OpenAI-compatible";
        loading = true;
        ShowProvider();
        loading = false;
        modelStatus = "not tested";
        UpdateStatus();
    }

    private void Endpoint_Changed(object sender, TextChangedEventArgs e)
    {
        if (loading || ApiKeyBox == null) return;
        if (visibleProvider != "Ollama") ApiKeyBox.Clear();
        modelStatus = "not tested";
        UpdateStatus();
    }

    private void CloudService_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loading || CloudServiceBox.SelectedValue is not string id) return;
        ReadProvider();
        settings.SelectCloudService(id);
        loading = true;
        ShowProvider();
        loading = false;
        modelStatus = "not tested";
        UpdateStatus();
    }

    private void ModelConfiguration_Changed(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        var configuredModel = visibleProvider == "Ollama" ? settings.OllamaModel : settings.CloudModel;
        if (ModelBox.Text.Trim() == configuredModel &&
            (visibleProvider == "Ollama" || ApiKeyBox.Password == settings.ApiKey)) return;
        modelStatus = "not tested";
        UpdateStatus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ReadProvider();
            settingsStore.Save(settings);
            WriteConfigurationDiagnostic("configuration.saved");
            RecordTrace("Settings", "Saved. API key protected for this Windows user.");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void SaveLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ReadProvider();
            var picker = new SaveFileDialog
            {
                Title = "Save TopSolid Automation diagnostic log",
                Filter = "JSON diagnostic log (*.json)|*.json",
                DefaultExt = ".json",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = $"TopSolid-AI-current-log-{DateTime.Now:yyyyMMdd-HHmmss}.json"
            };
            if (picker.ShowDialog(this) != true)
            {
                diagnosticLog.Write("info", "log.exportCancelled");
                return;
            }

            var path = Path.GetFullPath(picker.FileName);
            RecordTrace("Log", "Exporting current application-session diagnostics: " + path);

            var liveTools = mcp.Tools;
            var exportTools = liveTools.Count > 0 ? CloneTools(liveTools) : lastDiscoveredTools;
            var export = LogExportBuilder.Build(settings, settingsStore.FilePath, settingsStore.LastLoadWarning,
                modelStatus, mcp.IsConnected, mcp.IsMutationInFlight,
                exportTools,
                session?.GetConversationSnapshot() ?? [], sessionLog.Snapshot(), diagnosticLog);
            WriteUtf8FileAtomically(path, export.ToString(Formatting.Indented));
            diagnosticLog.Write("info", "log.exported", data: new JObject
            {
                ["path"] = path,
                ["bytes"] = new FileInfo(path).Length
            });
            RecordTrace("Log", "Saved diagnostic log: " + path);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Filter = "MCP server executable (*.exe)|*.exe", CheckFileExists = true };
        if (picker.ShowDialog(this) == true) ServerPathBox.Text = picker.FileName;
    }

    private async void ListModels_Click(object sender, RoutedEventArgs e) => await RunOperation(async token =>
    {
        ReadProvider();
        modelStatus = "listing models";
        UpdateStatus();
        using var candidate = ProviderFactory.Create(settings);
        var models = await candidate.ListModelsAsync(token);
        var selected = ModelBox.Text;
        ModelBox.ItemsSource = models;
        // Do not automatically pick the alphabetically first embedding/image model.
        ModelBox.Text = selected;
        modelStatus = $"{models.Count} models listed; inference not tested";
        RecordTrace("Models", modelStatus);
        _ = Dispatcher.BeginInvoke(new Action(() => { if (!closing) ModelBox.IsDropDownOpen = models.Count > 0; }));
    });

    private async void Connect_Click(object sender, RoutedEventArgs e) => await RunOperation(async token =>
    {
        if (mcp.IsConnected) { await mcp.DisconnectAsync(); return; }
        ReadProvider();
        await mcp.ConnectAsync(settings.McpServerPath, token);
        lastDiscoveredTools = CloneTools(mcp.Tools);
        diagnosticLog.Write("info", "mcp.toolsDiscovered", data: new JObject
        {
            ["toolCount"] = mcp.Tools.Count,
            ["tools"] = JArray.FromObject(mcp.Tools)
        });
        RecordTrace("MCP", "Discovered: " + string.Join(", ", mcp.Tools.Select(t => t.Name)));
    });

    private async void Status_Click(object sender, RoutedEventArgs e) => await RunOperation(async token =>
    {
        var result = await mcp.CallToolAsync("topsolid_get_status", new JObject(), token);
        RecordTrace(result.IsError ? "TopSolid error" : "TopSolid status", JsonConvert.SerializeObject(result, Formatting.Indented));
    });

    private void EnsureSession()
    {
        ReadProvider();
        // Never persist or log the signature. Changing provider starts fresh history.
        var signature = string.Join("\n", settings.Provider, settings.CloudService, settings.CloudBaseUrl, settings.CloudModel,
            settings.OllamaServerUrl, settings.OllamaModel, settings.ApiKey, settings.RequestTimeoutMinutes, settings.OllamaFastGptOss);
        if (session != null && sessionConfiguration == signature) return;
        var nextProvider = ProviderFactory.Create(settings);
        provider?.Dispose();
        provider = nextProvider;
        session = new ChatSession(provider, mcp);
        session.ConfirmChangeAsync = ConfirmChange;
        session.Trace += trace =>
        {
            RecordTrace(trace.Kind, trace.Text);
            if (trace.Kind == "CAD change") RecordChat("TopSolid result", trace.Text);
        };
        if (sessionConfiguration != null) RecordChat("System", "Configuration changed. Started a new conversation.");
        sessionConfiguration = signature;
    }

    private Task<bool> ConfirmChange(JObject proposal, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var dialog = new ChangeConfirmationWindow(proposal) { Owner = this };
        return Task.FromResult(dialog.ShowDialog() == true && !token.IsCancellationRequested);
    }

    private async void Send_Click(object sender, RoutedEventArgs e) => await SendMessage();

    private async Task SendMessage()
    {
        if (operation != null || string.IsNullOrWhiteSpace(MessageBox.Text)) return;
        var text = MessageBox.Text.Trim();
        chatClock.Restart(); ElapsedText.Text = "Elapsed 0.0 s"; elapsedTimer.Start();
        try { await RunOperation(async token =>
        {
            EnsureSession();
            RecordChat("You", text);
            MessageBox.Clear();
            try
            {
                var reply = await session!.SendAsync(text, token);
                modelStatus = session.LastResponseUsedModel ? "response received" : "not used (direct MCP)";
                RecordChat("Assistant", reply);
            }
            catch
            {
                if (string.IsNullOrEmpty(MessageBox.Text)) MessageBox.Text = text;
                modelStatus = token.IsCancellationRequested ? "cancelled" : "request failed";
                throw;
            }
        }); }
        finally {
            chatClock.Stop(); elapsedTimer.Stop();
            ElapsedText.Text = "Elapsed " + ChatTranscript.Elapsed(chatClock.Elapsed.TotalMilliseconds);
            RecordTrace("Timing", "Chat elapsed: " + ChatTranscript.Elapsed(chatClock.Elapsed.TotalMilliseconds) + " (includes confirmation time)");
        }
    }

    private async Task RunOperation(Func<CancellationToken, Task> work)
    {
        if (operation != null || closing) return;
        using var active = new CancellationTokenSource();
        operation = active;
        SetBusy(true);
        try { await work(active.Token); }
        catch (OperationCanceledException) { RecordTrace("Cancelled", "Request cancelled. An interrupted MCP session may need reconnecting."); if (chatClock.IsRunning) RecordChat("System", "Request cancelled."); }
        catch (Exception ex) { if (modelStatus == "listing models") modelStatus = "model listing failed"; ShowError(ex); }
        finally { operation = null; if (!closing) { SetBusy(false); UpdateStatus(); } }
    }

    private void SetBusy(bool busy)
    {
        ConfigurationPanel.IsEnabled = !busy;
        SendButton.IsEnabled = !busy;
        ConnectButton.IsEnabled = !busy;
        StatusButton.IsEnabled = !busy && mcp.IsConnected;
        ClearButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy && !mcp.IsMutationInFlight;
    }

    private void UpdateStatus()
    {
        StatusText.Text = $"MCP: {(mcp.IsConnected ? $"connected ({mcp.Tools.Count} tools)" : "disconnected")} · Model: {modelStatus}";
        ConnectButton.Content = mcp.IsConnected ? "Disconnect MCP" : "Connect MCP";
        StatusButton.IsEnabled = operation == null && mcp.IsConnected;
        CancelButton.IsEnabled = operation != null && !mcp.IsMutationInFlight;
        if (mcp.IsMutationInFlight) StatusText.Text += " · Applying CAD change; waiting for commit/rollback";
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        session?.Clear();
        sessionLog.Clear();
        ChatBox.Clear();
        ElapsedText.Text = "";
        TraceBox.Clear();
        diagnosticLog.Write("info", "chat.cleared");
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) { if (!mcp.IsMutationInFlight) operation?.Cancel(); }
    private async void Message_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; await SendMessage(); }
    }

    private void UpdateLogSecrets()
    {
        var currentKey = ApiKeyBox?.Password;
        diagnosticLog.SetSecrets(settings.CloudProfiles.Values.Select(profile => profile.ApiKey)
            .Concat(new[] { settings.ApiKey, currentKey ?? "" }));
    }

    private void WriteConfigurationDiagnostic(string eventName)
    {
        diagnosticLog.Write("info", eventName, data: new JObject
        {
            ["provider"] = settings.Provider,
            ["cloudService"] = settings.CloudService,
            ["cloudBaseUrl"] = settings.CloudBaseUrl,
            ["cloudModel"] = settings.CloudModel,
            ["cloudApiKeyConfigured"] = !string.IsNullOrWhiteSpace(settings.ApiKey),
            ["ollamaServerUrl"] = settings.OllamaServerUrl,
            ["ollamaModel"] = settings.OllamaModel,
            ["requestTimeoutMinutes"] = settings.RequestTimeoutMinutes,
            ["ollamaFastGptOss"] = settings.OllamaFastGptOss,
            ["mcpServerPath"] = settings.McpServerPath,
            ["settingsFilePath"] = settingsStore.FilePath
        });
    }

    private void RecordTrace(string kind, string text)
    {
        var safe = diagnosticLog.Redact(text);
        sessionLog.AddTrace(kind, safe);
        var level = kind.Contains("error", StringComparison.OrdinalIgnoreCase) ? "error" : "info";
        diagnosticLog.Write(level, "trace", data: new JObject { ["kind"] = kind, ["text"] = safe });
        OnUi(() => AppendTraceToUi(kind, safe));
    }

    private void AppendTraceToUi(string kind, string safe)
    {
        TraceBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {kind}: {safe}\n\n");
        if (TraceBox.Text.Length > 120000) TraceBox.Text = TraceBox.Text[^90000..];
        TraceBox.ScrollToEnd();
    }

    private void RecordChat(string role, string text)
    {
        var safe = diagnosticLog.Redact(text);
        double? elapsed = chatClock.IsRunning && role != "You" ? chatClock.Elapsed.TotalMilliseconds : null;
        sessionLog.AddChat(role, safe, elapsed);
        diagnosticLog.Write("info", "chat.message", data: new JObject { ["role"] = role, ["text"] = safe, ["elapsedMilliseconds"] = elapsed });
        OnUi(() => ChatBox.AppendMessage(role, safe, elapsed));
    }

    private void ShowError(Exception error)
    {
        diagnosticLog.WriteException("handled.error", error);
        RecordTrace("Error", error.Message);
        RecordChat("System", error.Message);
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (closeReady) return;
        e.Cancel = true;
        if (mcp.IsMutationInFlight)
        {
            RecordTrace("TopSolid", "A CAD change is in progress. Wait for its result before closing the application.");
            return;
        }
        if (closing) return;
        closing = true;
        operation?.Cancel();
        try { await mcp.DisposeAsync(); }
        finally { provider?.Dispose(); closeReady = true; _ = Dispatcher.BeginInvoke(new Action(Close)); }
    }

    private static IReadOnlyList<McpToolDefinition> CloneTools(IEnumerable<McpToolDefinition> tools)
        => tools.Select(tool => new McpToolDefinition
        {
            Name = tool.Name,
            Description = tool.Description,
            InputSchema = (JObject)tool.InputSchema.DeepClone(),
            Annotations = (JObject)tool.Annotations.DeepClone(),
            Metadata = (JObject)tool.Metadata.DeepClone()
        }).ToArray();

    private static void WriteUtf8FileAtomically(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory)) throw new IOException("Choose a valid destination for the diagnostic log.");
        Directory.CreateDirectory(directory);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}

