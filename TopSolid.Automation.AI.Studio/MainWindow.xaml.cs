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
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Connections;
using TopSolid.Automation.AI.Studio.Diagnostics;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Localization;
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

    public MainWindow() : this(autoConnect: true) { }

    internal MainWindow(bool autoConnect)
    {
        TopSolidTheme.InitializeResources(this);
        settings = settingsStore.Load();
        StudioStrings.Apply(settings.InterfaceLanguage);
        StudioStrings.InitializeResources(this);
        InitializeComponent();
        InitializeShell();
        InitializeConnectionIndicator();
        InitializePreferences();
        elapsedTimer.Tick += (_, _) => ElapsedText.Text = StudioStrings.Get("Chat.Elapsed", ChatTranscript.Elapsed(chatClock.Elapsed.TotalMilliseconds));
        DevModeBox.IsChecked = settings.DevMode;
        TimeoutBox.ItemsSource = Enumerable.Range(1, 60);
        TimeoutBox.SelectedItem = settings.RequestTimeoutMinutes;
        FastGptOssBox.IsChecked = !settings.OllamaFastGptOss;
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
            OnUi(() =>
            {
                if (!refreshRunning && !closing)
                {
                    connectionHealth.Set("Mcp", mcp.IsConnected ? ConnectionSeverity.Ready : ConnectionSeverity.Error, mcp.IsConnected ? "Health.McpReady" : "Health.McpUnavailable");
                    if (!mcp.IsConnected) connectionHealth.Set("TopSolid", ConnectionSeverity.Warning, "Health.TopSolidBlocked");
                }
                UpdateStatus();
            });
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
        Loaded += async (_, _) =>
        {
            if (DevModeBox.IsChecked == true) OpenDeveloper();
            if (autoConnect && !startupConnectionStarted)
            {
                startupConnectionStarted = true;
                await RefreshConnectionsAsync();
            }
        };
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
        FastGptOssBox.IsEnabled = local;
        CloudServiceBox.SelectedValue = settings.CloudService;
        CloudServiceBox.Visibility = CloudServiceLabel.Visibility = local ? Visibility.Collapsed : Visibility.Visible;
        EndpointBox.Text = local ? settings.OllamaServerUrl : settings.CloudBaseUrl;
        EndpointBox.IsReadOnly = !local && settings.CloudService != CloudServices.Custom;
        EndpointBox.ToolTip = EndpointBox.IsReadOnly ? StudioStrings.Text("Service URL is filled automatically. Choose Custom OpenAI-compatible to use another endpoint.") : null;
        ModelBox.ItemsSource = null;
        ModelBox.Text = local ? settings.OllamaModel : settings.CloudModel;
        ApiKeyBox.Password = local ? "" : settings.ApiKey;
        ApiKeyBox.IsEnabled = !local;
        ApiKeyBox.ToolTip = local ? null : StudioStrings.Text(CloudServices.Get(settings.CloudService).KeyHint);
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
        settings.OllamaFastGptOss = FastGptOssBox.IsChecked != true;
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
        InvalidateAiHealth();
        UpdateStatus();
    }

    private void Endpoint_Changed(object sender, TextChangedEventArgs e)
    {
        if (loading || ApiKeyBox == null) return;
        if (visibleProvider != "Ollama") ApiKeyBox.Clear();
        modelStatus = "not tested";
        InvalidateAiHealth();
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
        InvalidateAiHealth();
        UpdateStatus();
    }

    private void ModelConfiguration_Changed(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        var configuredModel = visibleProvider == "Ollama" ? settings.OllamaModel : settings.CloudModel;
        if (ModelBox.Text.Trim() == configuredModel &&
            (visibleProvider == "Ollama" || ApiKeyBox.Password == settings.ApiKey)) return;
        modelStatus = "not tested";
        InvalidateAiHealth();
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
            SettingsFeedback.Text = StudioStrings.Text("Settings saved.");
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
                Title = StudioStrings.Get("Dialog.SaveLog"),
                Filter = StudioStrings.Get("Dialog.LogFilter"),
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
        var picker = new OpenFileDialog { Filter = StudioStrings.Get("Dialog.McpFilter"), CheckFileExists = true };
        if (picker.ShowDialog(this) == true) ServerPathBox.Text = picker.FileName;
    }

    private async void ListModels_Click(object sender, RoutedEventArgs e) => await RunOperation(async token =>
    {
        ReadProvider();
        await RefreshModelList(token, openSelector: true);
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
        var connected = TopSolidConnectionStatus.IsConnected(result);
        connectionHealth.Set("TopSolid", connected ? ConnectionSeverity.Ready : ConnectionSeverity.Warning, connected ? "Health.TopSolidReady" : "Health.TopSolidStarting");
    });

    private void EnsureSession()
    {
        ReadProvider();
        // Never persist or log the signature. Keep intent and receipts across providers.
        var signature = string.Join("\n", settings.Provider, settings.CloudService, settings.CloudBaseUrl, settings.CloudModel,
            settings.OllamaServerUrl, settings.OllamaModel, settings.ApiKey, settings.RequestTimeoutMinutes, settings.OllamaFastGptOss);
        if (session != null && sessionConfiguration == signature) { session.ResponseLanguage = settings.ResponseLanguage; session.DeveloperMode = settings.DevMode; return; }
        var nextProvider = ProviderFactory.Create(settings);
        var history = session?.GetConversationSnapshot();
        provider?.Dispose();
        provider = nextProvider;
        session = new ChatSession(provider, mcp) { ResponseLanguage = settings.ResponseLanguage, DeveloperMode = settings.DevMode };
        if (history != null) session.RestoreConversation(history);
        session.ConfirmChangeAsync = ConfirmChange;
        session.AskUserAsync = AskUser;
        session.Trace += trace =>
        {
            if (trace.Kind is "Tool result" or "Tool error" or "CAD change")
                responsePresenter.ObserveToolResult(trace.Text, trace.Arguments, trace.ToolName);
            RecordTrace(trace.Kind, trace.Text);
            if (trace.Kind == "CAD change") RecordChat("TopSolid result", trace.Text);
        };
        if (sessionConfiguration != null) RecordChat("System", StudioStrings.Text("Configuration changed. Conversation preserved."));
        RecordTrace("Model settings", settings.Provider + "; model=" + (settings.Provider == AppSettings.OllamaProvider ? settings.OllamaModel : settings.CloudModel));
        sessionConfiguration = signature;
    }

    private Task<QuestionAnswer?> AskUser(UserQuestion question, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var dialog = new QuestionWindow(question, mcp) { Owner = this };
        using var registration = token.Register(() => OnUi(() => { if (dialog.IsVisible) dialog.Close(); }));
        activityAwaitingApproval = true;
        SetActivity("Activity.Question", QuestionWindow.IconKey(question.ItemKind), waitingForUser: true);
        try
        {
            var accepted = dialog.ShowDialog() == true && !token.IsCancellationRequested;
            if (accepted) RecordChat("You", question.Title + "\n" + dialog.Answer!.Summary);
            RecordTrace("Question", accepted ? "User answered: " + question.Kind : "User cancelled the question.");
            return Task.FromResult(accepted ? dialog.Answer : null);
        }
        finally
        {
            activityAwaitingApproval = false;
            SetActivity(token.IsCancellationRequested ? "Activity.Cancelling" : "Activity.PreparingResponse");
        }
    }

    private Task<bool> ConfirmChange(JObject proposal, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var decision = PermissionPolicy.Evaluate(permissionMode, proposal);
        RecordTrace("Permission", $"{permissionMode}: {decision.Reason}");
        if (!decision.RequiresApproval) return Task.FromResult(true);
        responsePresenter.Observe(proposal);
        var dialog = new ChangeConfirmationWindow(proposal, settings.DevMode, responsePresenter, mcp) { Owner = this };
        using var registration = token.Register(() => OnUi(() => { if (dialog.IsVisible) dialog.Close(); }));
        activityAwaitingApproval = true;
        SetActivity("Activity.Approval", "status", waitingForUser: true);
        try { return Task.FromResult(dialog.ShowDialog() == true && !token.IsCancellationRequested); }
        finally
        {
            activityAwaitingApproval = false;
            SetActivity(token.IsCancellationRequested ? "Activity.Cancelling" : "Activity.TopSolid", "document");
        }
    }

    private async void Send_Click(object sender, RoutedEventArgs e) => await SendMessage();

    private async Task SendMessage()
    {
        if (operation != null || loadingAttachments || (string.IsNullOrWhiteSpace(MessageBox.Text) && attachments.Count == 0)) return;
        var text = MessageBox.Text.Trim();
        var submittedAttachments = attachments.ToArray();
        chatClock.Restart(); ElapsedText.Text = StudioStrings.Get("Chat.Elapsed", "0.0 s"); elapsedTimer.Start();
        try { await RunOperation(async token =>
        {
            EnsureSession();
            RecordChat("You", text + (submittedAttachments.Length == 0 ? "" : "\n\n" + StudioStrings.Get("Chat.Files", string.Join(", ", submittedAttachments.Select(a => a.Name)))));
            MessageBox.Clear();
            attachments.Clear(); RenderAttachments();
            try
            {
                var reply = await session!.SendAsync(text, submittedAttachments, token);
                modelStatus = session.LastResponseUsedModel ? "response received" : "not used (direct MCP)";
                if (session.LastResponseUsedModel)
                {
                    connectionHealth.Remove("AiRuntime");
                    connectionHealth.Set("Ai", ConnectionSeverity.Ready, "Health.AiInferenceReady");
                }
                RecordChat("Assistant", reply);
            }
            catch
            {
                if (string.IsNullOrEmpty(MessageBox.Text)) MessageBox.Text = text;
                if (attachments.Count == 0) { attachments.AddRange(submittedAttachments); RenderAttachments(); }
                modelStatus = token.IsCancellationRequested ? "cancelled" : "request failed";
                throw;
            }
        }); }
        finally {
            chatClock.Stop(); elapsedTimer.Stop();
            ElapsedText.Text = StudioStrings.Get("Chat.Elapsed", ChatTranscript.Elapsed(chatClock.Elapsed.TotalMilliseconds));
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
        catch (OperationCanceledException) { if (modelStatus == "listing models") modelStatus = "cancelled"; RecordTrace("Cancelled", "Request cancelled. An interrupted MCP session may need reconnecting."); if (chatClock.IsRunning) RecordChat("System", StudioStrings.Text("Request cancelled.")); }
        catch (Exception ex) { if (modelStatus == "listing models") modelStatus = "model listing failed"; ShowError(ex); }
        finally { operation = null; if (!closing) { SetBusy(false); UpdateStatus(); } }
    }

    private void SetBusy(bool busy)
    {
        activityAwaitingApproval = false;
        SetActivity(busy ? (chatClock.IsRunning ? "Activity.Thinking" : "Activity.Working") : null);
        ConfigurationPanel.IsEnabled = !busy;
        ModelBox.IsEnabled = !busy;
        PermissionBox.IsEnabled = !busy;
        AttachButton.IsEnabled = !busy && !loadingAttachments;
        AttachmentsPanel.IsEnabled = !busy;
        SendButton.IsEnabled = !busy && !loadingAttachments;
        SendButton.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        CancelButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        UpdateConnectionIndicator();
        ServerPathBox.IsEnabled = SaveSettingsButton.IsEnabled = !busy;
        ResponseLanguageBox.IsEnabled = !busy;
        developerWindow?.UpdateConnectionState(mcp.IsConnected, busy, mcp.IsMutationInFlight);
        ClearButton.IsEnabled = !busy && !loadingAttachments;
        CancelButton.IsEnabled = busy && !mcp.IsMutationInFlight && operation?.IsCancellationRequested != true;
    }

    private void UpdateStatus()
    {
        StatusText.Text = StudioStrings.Get("Chat.Status", mcp.IsConnected ? StudioStrings.Get("Chat.ConnectedTools", mcp.Tools.Count) : StudioStrings.Text("disconnected"), LocalizedModelStatus());
        UpdateConnectionIndicator();
        developerWindow?.UpdateConnectionState(mcp.IsConnected, operation != null, mcp.IsMutationInFlight);
        CancelButton.IsEnabled = operation != null && !mcp.IsMutationInFlight && !operation.IsCancellationRequested;
        if (mcp.IsMutationInFlight) StatusText.Text += " · " + StudioStrings.Text("Applying CAD change; waiting for commit/rollback");
        if (operation != null && mcp.IsMutationInFlight && !activityAwaitingApproval) SetActivity("Activity.Applying", "operation");
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (operation != null || loadingAttachments) return;
        session?.Clear();
        sessionLog.Clear();
        responsePresenter.Clear();
        lastPresentedChatSequence = 0;
        ChatBox.Clear();
        ElapsedText.Text = "";
        TraceBox.Clear();
        attachments.Clear(); RenderAttachments();
        EmptyState.Visibility = Visibility.Visible;
        ShowChat();
        RefreshDeveloper();
        diagnosticLog.Write("info", "chat.cleared");
    }
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (mcp.IsMutationInFlight || operation == null) return;
        SetActivity("Activity.Cancelling", "status");
        CancelButton.IsEnabled = false;
        operation.Cancel();
    }
    private async void Message_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !composingText && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Control) { e.Handled = true; await SendMessage(); }
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
            ["devMode"] = settings.DevMode,
            ["appearanceMode"] = settings.AppearanceMode,
            ["interfaceLanguage"] = settings.InterfaceLanguage,
            ["responseLanguage"] = settings.ResponseLanguage,
            ["permissionMode"] = permissionMode.ToString(),
            ["topSolidTheme"] = themeFollower?.Current.Name,
            ["mcpServerPath"] = settings.McpServerPath,
            ["settingsFilePath"] = settingsStore.FilePath
        });
    }

    private void RecordTrace(string kind, string text)
    {
        if (kind == "Model" && text.StartsWith("Request ", StringComparison.Ordinal))
        {
            Interlocked.Exchange(ref modelRequestStartedTicks, DateTimeOffset.UtcNow.UtcTicks);
            OnUi(() => { if (!closing) { connectionTimer.Start(); UpdateConnectionIndicator(); } });
        }
        else if (kind == "Timing" && text.StartsWith("Model request ", StringComparison.Ordinal))
            Interlocked.Exchange(ref modelRequestStartedTicks, 0);
        var safe = diagnosticLog.Redact(text);
        if (kind is "Tool result" or "Tool error" or "CAD change" or "TopSolid status") responsePresenter.ObserveToolResult(safe);
        sessionLog.AddTrace(kind, safe);
        var level = kind.Contains("error", StringComparison.OrdinalIgnoreCase) ? "error" : "info";
        diagnosticLog.Write(level, "trace", data: new JObject { ["kind"] = kind, ["text"] = safe });
        var activitySource = operation;
        OnUi(() => { UpdateActivityFromTrace(kind, safe, activitySource); AppendTraceToUi(kind, safe); });
    }

    private void AppendTraceToUi(string kind, string safe)
    {
        TraceBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {kind}: {safe}\n\n");
        if (TraceBox.Text.Length > 120000) TraceBox.Text = TraceBox.Text[^90000..];
        TraceBox.ScrollToEnd();
        RefreshDeveloper();
    }

    private void RecordChat(string role, string text)
    {
        var safe = diagnosticLog.Redact(text);
        double? elapsed = chatClock.IsRunning && role != "You" ? chatClock.Elapsed.TotalMilliseconds : null;
        sessionLog.AddChat(role, safe, elapsed);
        diagnosticLog.Write("info", "chat.message", data: new JObject { ["role"] = role, ["text"] = safe, ["elapsedMilliseconds"] = elapsed });
        OnUi(() => { AppendPendingChat(); RefreshDeveloper(); });
    }

    private void ShowError(Exception error)
    {
        if (error is AiProviderException or System.Net.Http.HttpRequestException || (chatClock.IsRunning && error is TimeoutException or ArgumentException))
        {
            var failure = ConnectionHealth.AiFailure(error);
            connectionHealth.Set("AiRuntime", failure.Severity, failure.Key);
        }
        diagnosticLog.WriteException("handled.error", error);
        RecordTrace("Error", error.Message);
        RecordChat("System", StudioStrings.Text(error.Message));
        if (SettingsPage.Visibility == Visibility.Visible) SettingsFeedback.Text = responsePresenter.Present(StudioStrings.Text(error.Message), settings.DevMode);
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
        connectionTimer.Stop();
        refreshRotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
        connectionDialog?.Close();
        operation?.Cancel();
        try { await mcp.DisposeAsync(); }
        finally { provider?.Dispose(); elapsedTimer.Stop(); developerTimer.Stop(); themeFollower?.Dispose(); developerWindow?.Close(); closeReady = true; _ = Dispatcher.BeginInvoke(new Action(Close)); }
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

