using System.Security.Cryptography;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio;
using TopSolid.Automation.AI.Studio.Chat;
using System.Windows.Documents;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.Tests;

internal static class UiSmokeTests
{
    public static Task Run(string? serverPath, string? liveOllamaModel = null, string? approvedNativePlan = null, bool render = true)
    {
        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.UnhandledException += (_, args) =>
            {
                args.Handled = true;
                completion.TrySetException(args.Exception);
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            };
            dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    await ExerciseWindow(serverPath, liveOllamaModel, approvedNativePlan, render);
                    await CloseAfterConnectFailure();
                    await CloseDuringConnect(serverPath);
                    VerifyConfirmationDialog();
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true, Name = "TopSolid AI WPF smoke test" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static async Task ExerciseWindow(string? serverPath, string? liveOllamaModel, string? approvedNativePlan, bool render)
    {
        var settingsPath = new SettingsStore().FilePath;
        var before = Fingerprint(settingsPath);
        // A Window that has never been shown makes its whole visual tree
        // effectively invisible. Host this test window off screen so WPF runs
        // its real layout/render pipeline without taking focus from the user.
        var window = CreateOffscreenWindow();
        var nativeDocument = approvedNativePlan == null ? null : ApprovedFixtureDocument(approvedNativePlan);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => closed.TrySetResult();
        try
        {
            if (nativeDocument != null)
            {
                // A real owner handle is needed for the real modal confirmation window.
                window.ShowInTaskbar = false; window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -10000; window.Top = -10000; window.Show();
            }
            // A normal shown window creates its editable-control templates
            // before user input. Do the same for this offscreen owned window.
            LayoutWindow(window);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            T Control<T>(string name) where T : FrameworkElement => window.FindName(name) as T
                ?? throw new InvalidOperationException("Missing UI control: " + name);
            Control<Button>("SettingsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Control<ListBox>("SettingsNavigation").SelectedValue = "ai";
            LayoutWindow(window);
            var provider = Control<ComboBox>("ProviderBox");
            var cloudService = Control<ComboBox>("CloudServiceBox");
            var endpoint = Control<TextBox>("EndpointBox");
            var key = Control<PasswordBox>("ApiKeyBox");
            var model = Control<ComboBox>("ModelBox");
            var wait = Control<ComboBox>("TimeoutBox");
            Check.Equal(60, wait.Items.Count, "AI wait must allow 1–60 minutes");
            wait.SelectedItem = 30;
            var faster = Control<CheckBox>("FastGptOssBox"); faster.IsChecked = false;
            var path = Control<TextBox>("ServerPathBox");
            var developer = OpenDeveloperFixture(window);
            var connect = developer.ConnectButton;
            var saveLog = Control<Button>("SaveLogButton");
            var statusButton = developer.StatusButton;
            var statusText = Control<TextBlock>("StatusText");
            var trace = Control<TextBox>("TraceBox");
            var chat = Control<ChatTranscript>("ChatBox");
            Check.True(!statusButton.IsEnabled, "TopSolid status button should require MCP connection");
            Check.True(saveLog.IsEnabled && (string)saveLog.Content == "Save log", "Save log action should be available when idle");
            Check.True(statusText.Text.Contains("disconnected", StringComparison.Ordinal), "Initial MCP status should be disconnected");
            provider.SelectedIndex = 1;
            LayoutWindow(window);
            Check.Equal(30, (int)wait.SelectedItem, "Provider switching lost the configured timeout");
            Check.True(faster.IsVisible && faster.IsChecked == false, "Ollama thinking choice should be visible and retained");
            Check.True(!key.IsEnabled, "Ollama should not accept a cloud API key");
            provider.SelectedIndex = 0;
            Check.True(!faster.IsVisible, "Ollama-specific latency control should be hidden for cloud APIs");
            Check.True(key.IsEnabled, "Cloud configuration should accept an API key");
            cloudService.SelectedValue = "gemini";
            Check.Equal("https://generativelanguage.googleapis.com/v1beta/openai", endpoint.Text, "Gemini UI preset URL");
            Check.True(endpoint.IsReadOnly, "Preset URLs must not require manual entry");
            key.Password = "ui-gemini-test"; model.Text = "ui-gemini-model";
            cloudService.SelectedValue = "anthropic";
            Check.Equal("https://api.anthropic.com/v1", endpoint.Text, "Anthropic UI preset URL");
            Check.True(key.Password != "ui-gemini-test", "Switching service must not transfer credentials");
            key.Password = "ui-claude-test"; model.Text = "ui-claude-model";
            cloudService.SelectedValue = "gemini";
            Check.Equal("ui-gemini-test", key.Password, "Switching back restores that service's key");
            Check.Equal("ui-gemini-model", model.Text, "Switching back restores that service's model");
            cloudService.SelectedValue = "custom";
            Check.True(!endpoint.IsReadOnly, "Custom endpoints remain editable");
            // Use only synthetic display values. The constructor reads settings;
            // no Save event is ever invoked, and the file hash is checked below.
            endpoint.Text = "https://example.test/v1";
            key.Clear();
            model.Text = "configure-a-tool-capable-model";
            trace.Clear();
            chat.Clear();
            path.Text = serverPath ?? Path.Combine(AppContext.BaseDirectory, "McpServer", "TopSolid.Automation.Mcp.Server.AddIn.exe");
            Check.True(File.Exists(path.Text), "Supply --server <exe> with --ui-smoke when no server is bundled beside the test harness");
            connect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntil(() => connect.IsEnabled, "MCP Connect button did not finish");
            Check.True(statusText.Text.Contains("connected (187 tools)", StringComparison.Ordinal), "UI did not show discovered tools: " + trace.Text);
            Check.True(trace.Text.Contains("topsolid_get_active_document", StringComparison.Ordinal), "Tool discovery should be visible in trace");
            Check.True(statusButton.IsEnabled, "Connected UI should enable the TopSolid status button");
            statusButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntil(() => connect.IsEnabled, "TopSolid status button did not finish");
            Check.True(trace.Text.Contains("TopSolid status", StringComparison.Ordinal) && trace.Text.Contains("connected", StringComparison.Ordinal),
                "TopSolid status result should be visible in trace");
            Check.True(!trace.Text.Contains("Error:", StringComparison.Ordinal), "UI smoke produced an error: " + trace.Text);
            if (string.IsNullOrWhiteSpace(liveOllamaModel))
            {
                // Direct MCP needs no running inference service. Exercise the real
                // Send handler, elapsed label and per-role rendering together.
                provider.SelectedIndex = 1; endpoint.Text = "http://localhost:11434"; model.Text = "ui-no-inference";
                Control<TextBox>("MessageBox").Text = "List all projects name order by cretaion date (oldest to newest)";
                var send = Control<Button>("SendButton"); send.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitUntil(() => send.IsEnabled, "Direct PDM UI request did not finish");
                const string statusMarker = "TopSolid status: ";
                using var statusReader = new Newtonsoft.Json.JsonTextReader(new StringReader(trace.Text[(trace.Text.LastIndexOf(statusMarker, StringComparison.Ordinal) + statusMarker.Length)..].TrimStart()));
                // Trace formatting expands JSON over several lines. Read exactly
                // one object, leaving any subsequent timestamped events untouched.
                var statusEnvelope = JObject.Load(statusReader);
                var statusData = statusEnvelope["structuredContent"] ?? JObject.Parse((string)statusEnvelope["content"]![0]!["text"]!);
                var nativeAvailable = (bool)statusData["connected"]!;
                Check.True(nativeAvailable ? chat.Text.Contains("(complete)") : chat.Text.Contains("TopSolid query failed"),
                    "Direct PDM request must render the complete list when connected or the real connection failure when unavailable: " + chat.Text);
                Check.True(Control<TextBlock>("ElapsedText").Text.StartsWith("Elapsed "), "Elapsed duration missing from chat");
                var answer = chat.Document.Blocks.OfType<Paragraph>().Last();
                Check.Equal(((SolidColorBrush)window.FindResource("TextBrush")).Color, ((SolidColorBrush)answer.Foreground).Color, "AI answer must follow the readable theme text color");
                Check.True(new TextRange(answer.ContentStart, answer.ContentEnd).Text.Contains(" s"), "Final response must retain elapsed time");
                Check.True(chat.Document.Blocks.OfType<Paragraph>().Any(p => p.Tag is "You"), "User role must remain distinct in the transcript");
                Check.True(statusText.Text.Contains("not used (direct MCP)"), "Direct MCP must not claim model inference");
                Check.True(!trace.Text.Contains("Model: Request"), "UI list should not contact a model");
            }
            Control<TextBox>("MessageBox").Text = "Am I connected to TopSolid?";
            if (!string.IsNullOrWhiteSpace(liveOllamaModel))
            {
                provider.SelectedIndex = 1;
                endpoint.Text = "http://localhost:11434";
                model.Text = liveOllamaModel;
                LayoutWindow(window);
                Control<TextBox>("MessageBox").Text = nativeDocument == null ? "Am I connected to TopSolid? Use the available tool and answer in English." :
                    $"In the existing document with documentId {nativeDocument}, create one native XY sketch containing a circle of radius 7 mm, centered at world X=200 mm, Y=0, Z=0. Name the sketch AI Circle. Read the target document first and use the circle tool. Do not save or make any other changes. Answer in English.";
                var approvals = 0;
                var confirmationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                if (nativeDocument != null)
                {
                    confirmationTimer.Tick += (_, _) =>
                    {
                        var dialog = window.OwnedWindows.OfType<ChangeConfirmationWindow>().FirstOrDefault(d => d.IsVisible);
                        if (dialog == null) return;
                        var panel = (DockPanel)dialog.Content;
                        var proposal = JObject.Parse(dialog.ProposalBox.Text);
                        var args = (JObject)proposal["arguments"]!;
                        var affected = proposal["target"]?["affectedDocuments"] as JArray;
                        var allowed = approvals == 0 && affected?.Count == 1 && (string?)affected[0]?["documentId"] == nativeDocument &&
                            (string?)proposal["toolName"] == "topsolid_create_circle2d" &&
                            (string?)args["documentId"] == nativeDocument && (string?)args["placement"] == "xy" &&
                            ((string?)args["units"] ?? "mm") == "mm" && (double?)args["radius"] == 7 && (double?)args["x"] == 200 &&
                            ((double?)args["y"] ?? 0) == 0 && ((double?)args["z"] ?? 0) == 0 && (string?)args["name"] == "AI Circle";
                        if (allowed)
                        {
                            approvals++;
                            File.WriteAllText("artifacts/native-workflow/ai-circle-proposal.json", proposal.ToString());
                        }
                        (allowed ? dialog.ApproveButton : dialog.RejectButton)
                            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    };
                    confirmationTimer.Start();
                }
                var send = Control<Button>("SendButton");
                send.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                try { await WaitUntil(() => send.IsEnabled, "Live Ollama UI chat did not finish", TimeSpan.FromMinutes(5)); }
                finally { confirmationTimer.Stop(); }
                Check.True(trace.Text.Contains(nativeDocument == null ? "Tool call: topsolid_get_status" : "Tool call: topsolid_create_circle2d", StringComparison.Ordinal),
                    "The live model did not visibly invoke the discovered TopSolid status tool");
                if (nativeDocument != null)
                {
                    File.WriteAllText("artifacts/native-workflow/ai-circle-ui-trace.txt", trace.Text);
                    File.WriteAllText("artifacts/native-workflow/ai-circle-ui-chat.txt", chat.Text);
                    Check.True(approvals == 1 && trace.Text.Contains("CAD change:", StringComparison.Ordinal) && trace.Text.Contains("geometryReadBack", StringComparison.Ordinal), "The approved circle did not produce a native geometry receipt");
                    Check.True(!trace.Text.Contains("Tool error:", StringComparison.Ordinal), "The live circle tool failed: " + trace.Text);
                }
                Check.True(chat.Text.Contains("Assistant", StringComparison.Ordinal),
                    "The live model did not produce a final assistant answer: " + chat.Text);
                Check.True(statusText.Text.Contains("response received", StringComparison.Ordinal),
                    "UI did not confirm live inference success");
                Check.True(!trace.Text.Contains("Error:", StringComparison.Ordinal), "Live UI chat produced an error: " + trace.Text);
                Console.WriteLine("Live UI Ollama model: " + liveOllamaModel);
                Console.WriteLine("Live UI conversation: " + chat.Text.ReplaceLineEndings(" | "));
            }

            // Render only this program's own WPF visual tree. No desktop capture.
            if (string.IsNullOrWhiteSpace(liveOllamaModel))
            {
                cloudService.SelectedValue = "gemini";
                key.Clear();
                model.Text = "";
            }
            ((System.Windows.Controls.Grid)window.FindName("SettingsPage")).Visibility = Visibility.Collapsed;
            ((System.Windows.Controls.Grid)window.FindName("ChatPage")).Visibility = Visibility.Visible;
            var root = LayoutWindow(window);
            if (!string.IsNullOrWhiteSpace(liveOllamaModel))
                Check.True(statusText.Text.Contains("response received", StringComparison.Ordinal),
                    "Layout changed the status of a completed live model response");
            Console.WriteLine("WPF controls: direct MCP Send, themed replies, elapsed time, distinct roles and unchanged saved settings checked.");
            if (render) {
            const int width = 1032;
            const int height = 792;
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var background = new DrawingVisual();
            using (var drawing = background.RenderOpen()) drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            bitmap.Render(background);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var output = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "artifacts", "ui-smoke.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            using (var stream = File.Create(output)) encoder.Save(stream);
            Check.True(new FileInfo(output).Length > 5000, "WPF render is unexpectedly empty");
            Console.WriteLine("WPF own-window render: " + output);
            }
            connect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntil(() => connect.IsEnabled, "MCP Disconnect button did not finish");
            Check.True(statusText.Text.Contains("disconnected", StringComparison.Ordinal), "UI did not return to disconnected status");
            Check.True(!statusButton.IsEnabled, "Disconnected UI should disable TopSolid status");
        }
        finally
        {
            window.Close();
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check.Equal(before, Fingerprint(settingsPath), "UI smoke changed the user's saved settings");
        }
    }

    private static string Fingerprint(string path) => File.Exists(path)
        ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : "absent";

    private static string ApprovedFixtureDocument(string hash)
    {
        var planPath = Path.GetFullPath("scripts/LiveWorkflow/fixture-plan.json");
        Check.Equal(Fingerprint(planPath).ToLowerInvariant(), hash.ToLowerInvariant(), "Approval must match the exact native fixture plan");
        var receipts = JObject.Parse(File.ReadAllText("artifacts/native-workflow/receipts.json"));
        Check.Equal(hash.ToLowerInvariant(), (string?)receipts["planSha256"], "Native receipts belong to a different approval");
        return (string?)receipts["documents"]?["MCP Contour and Extrusion"] ?? throw new InvalidOperationException("Create the approved isolated native fixture first.");
    }

    private static void VerifyConfirmationDialog()
    {
        StudioStrings.Apply("en");
        foreach (var approved in new[] { false, true })
        {
            var proposal = new JObject { ["toolName"] = "topsolid_create_rectangle2d", ["target"] = new JObject { ["name"] = "Test fixture part", ["documentId"] = "test-only-revision" },
                ["inputLengthUnits"] = "mm", ["effect"] = "Add native geometry. Changes are not saved automatically.",
                ["arguments"] = new JObject { ["documentId"] = "test-only-revision", ["width"] = 20, ["height"] = 10, ["placement"] = "xy" } };
            var dialog = new ChangeConfirmationWindow(proposal) { ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
            var root = (DockPanel)dialog.Content;
            root.Background = SystemColors.WindowBrush;
            var buttons = new[] { dialog.RejectButton, dialog.ApproveButton };
            Check.True(buttons.Single(b => (string)b.Content == "Cancel").IsDefault, "Confirmation should default to Cancel");
            Check.True(!buttons.Single(b => (string)b.Content == "Approve").IsDefault, "Enter must not implicitly approve a change");
            Check.True(dialog.ProposalBox.IsReadOnly, "Preview must be immutable");
            dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(() =>
            {
                if (!approved)
                {
                    root.Measure(new Size(580, 500)); root.Arrange(new Rect(0, 0, 580, 500)); root.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(580, 500, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.GetFullPath("artifacts/confirmation-smoke.png")); encoder.Save(stream);
                }
                buttons.Single(b => (string)b.Content == (approved ? "Approve" : "Cancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            Check.Equal(approved, dialog.ShowDialog() == true, "Confirmation button returned the wrong decision");
        }
    }

    private static FrameworkElement LayoutWindow(MainWindow window)
    {
        var root = (FrameworkElement)window.Content;
        if (root is Panel panel) panel.Background = Brushes.White;
        root.Measure(new Size(1032, 792));
        root.Arrange(new Rect(0, 0, 1032, 792));
        root.UpdateLayout();
        return root;
    }

    private static async Task CloseAfterConnectFailure()
    {
        var window = CreateOffscreenWindow();
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => closed.TrySetResult();
        try
        {
            ((TextBox)window.FindName("ServerPathBox")).Text = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe");
            var connect = OpenDeveloperFixture(window).ConnectButton;
            connect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntil(() => connect.IsEnabled, "Failed MCP connection left the UI busy");
            Check.True(((TextBox)window.FindName("TraceBox")).Text.Contains("Error", StringComparison.Ordinal),
                "A missing MCP executable should produce visible error feedback");
        }
        finally
        {
            window.Close();
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static async Task CloseDuringConnect(string? serverPath)
    {
        var window = CreateOffscreenWindow();
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => closed.TrySetResult();
        ((TextBox)window.FindName("ServerPathBox")).Text = serverPath ?? Path.Combine(AppContext.BaseDirectory,
            "McpServer", "TopSolid.Automation.Mcp.Server.AddIn.exe");
        OpenDeveloperFixture(window).ConnectButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.Close();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // Allow cancellation continuations and queued status notifications to run
        // on this same dispatcher after the window has closed.
        await Task.Delay(100);
    }

    private static MainWindow CreateOffscreenWindow()
    {
        var window = new MainWindow(autoConnect: false) { ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
        // Do not let a user's saved Dev Mode open a dashboard during initial layout.
        ((CheckBox)window.FindName("DevModeBox")).IsChecked = false;
        ((ComboBox)window.FindName("InterfaceLanguageBox")).SelectedValue = "en";
        window.Show();
        LayoutWindow(window);
        return window;
    }

    private static DeveloperWindow OpenDeveloperFixture(MainWindow window)
    {
        ((CheckBox)window.FindName("DevModeBox")).IsChecked = true;
        var developer = typeof(MainWindow).GetField("developerWindow", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(window) as DeveloperWindow
            ?? throw new InvalidOperationException("Dev Mode did not open its diagnostic window.");
        developer.ShowInTaskbar = false;
        developer.Left = -20000; developer.Top = -20000;
        return developer;
    }

    private static async Task WaitUntil(Func<bool> condition, string message, TimeSpan? limit = null)
    {
        using var timeout = new CancellationTokenSource(limit ?? TimeSpan.FromSeconds(20));
        try
        {
            do { await Task.Delay(25, timeout.Token); } while (!condition());
        }
        catch (OperationCanceledException) { throw new TimeoutException(message); }
    }
}
