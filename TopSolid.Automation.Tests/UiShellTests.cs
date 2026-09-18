using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Diagnostics;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

/// <summary>Run separately (--ui-shell): isolated offscreen UI fixtures, with no provider/MCP calls or settings saves.</summary>
internal static partial class UiShellTests
{
    private static bool captureImages;
    public static Task Run(bool render = true)
    {
        captureImages = render;
        // Make fixture captures independent of the host's GPU/display session.
        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.UnhandledException += (_, args) => { args.Handled = true; completion.TrySetException(args.Exception); dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); };
            dispatcher.BeginInvoke(async () =>
            {
                Application? application = null;
                try
                {
                    Check.True(Application.Current == null, "Run --ui-shell in its own process");
                    application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    application.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("/TopSolid.Automation.AI.Studio;component/Appearance/TopSolidStyles.xaml", UriKind.Relative)
                    });
                    await Exercise();
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally { application?.Shutdown(); dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true, Name = "TopSolid UI shell fixture" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task;
    }

    private static async Task Exercise()
    {
        var output = Path.GetFullPath(Path.Combine("artifacts", "ui-redesign"));
        Directory.CreateDirectory(output);
        var settingsPath = new SettingsStore().FilePath;
        var settingsBefore = Fingerprint(settingsPath);
        var window = new MainWindow(autoConnect: false) { ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
        var devMode = Control<CheckBox>(window, "DevModeBox");
        devMode.IsChecked = false; // A saved Dev Mode preference must not open an uncontrolled extra fixture window.
        window.Show();
        await Layout(window);
        Control<ComboBox>(window, "InterfaceLanguageBox").SelectedValue = "en";
        var appearance = Control<ComboBox>(window, "AppearanceModeBox");
        Check.Equal(4, appearance.Items.Count, "Appearance must expose dark, light, system and TopSolid");
        foreach (var mode in new[] { "dark", "light", "system", "topsolid" })
        {
            appearance.SelectedValue = mode; await Layout(window);
            Check.Equal(mode, ((TopSolidThemeFollower)Field(window, "themeFollower")!).Mode, "Appearance selection did not reach the follower");
        }
        Check.True(window.FindName("ConnectButton") == null && window.FindName("StatusButton") == null,
            "Developer connection controls must not remain in the user window");
        // Fixtures explicitly select both palettes; production follower remains covered by separate parser tests.
        (Field(window, "themeFollower") as IDisposable)?.Dispose();
        Control<ComboBox>(window, "ProviderBox").SelectedIndex = 0;
        Control<ComboBox>(window, "CloudServiceBox").SelectedValue = "custom";
        Control<TextBox>(window, "EndpointBox").Text = "https://example.test/v1";
        Control<PasswordBox>(window, "ApiKeyBox").Clear();
        Control<ComboBox>(window, "ModelBox").Text = "fixture-tool-capable-model";
        Control<TextBox>(window, "ServerPathBox").Text = @"C:\TopSolidAutomation\McpServer\TopSolid.Automation.Mcp.Server.AddIn.exe";
        var session = (SessionLog)Field(window, "sessionLog")!;
        await VerifyResponsePresentation(window, output);
        await ApprovalActivityUiTests.Run(window, (target, name) => Render(target, Path.Combine(output, name)));
        await QuestionUiTests.Run(window, (target, name) => Render(target, Path.Combine(output, name)));
        await GraphicPreviewUiTests.Run(window, (target, name) => Render(target, Path.Combine(output, name)));
        await VerifyConnectionIndicator(window, output);
        await DialogThemeUiTests.Run(window, (target, name) => Render(target, Path.Combine(output, name)));
        session.Clear();
        session.AddChat("You", "프로젝트 목록을 확인하고 새 2D 스케치를 준비해 주세요.");
        session.AddTrace("Model", "UI fixture request; no inference performed");
        session.AddTrace("Timing", "Model request 1: 1.25 s");
        session.AddTrace("Tool call", "topsolid_get_status {\"includeDocument\":true}");
        session.AddTrace("Tool result", "{\"connected\":true,\"document\":{\"name\":\"테스트파트\",\"type\":\"Part\"},\"fixture\":true}");
        session.AddTrace("Timing", "Tool topsolid_get_status: 0.40 s; user confirmation: 0.00 s");
        session.AddChat("Assistant", "현재 문서: 테스트파트\n\n2D 스케치 작업을 시작할 수 있습니다. 원 또는 사각형의 치수와 위치를 알려주세요.\n\nUI preview data only — no model request or CAD change was performed.", 1650);
        var transcript = Control<ChatTranscript>(window, "ChatBox");
        transcript.Clear();
        foreach (var entry in session.Snapshot().Chat) transcript.AppendMessage(entry.Role, entry.Text, entry.ElapsedMilliseconds);
        Control<FrameworkElement>(window, "EmptyState").Visibility = Visibility.Collapsed;
        Control<TextBox>(window, "MessageBox").Text = "XY 평면에 지름 20 mm 원을 작성해 주세요.";
        var fixtureTools = new List<McpToolDefinition>
        {
            new() { Name = "topsolid_get_status", Description = "Read TopSolid connection and active document.",
                InputSchema = JObject.Parse("{\"type\":\"object\",\"properties\":{\"includeDocument\":{\"type\":\"boolean\"}}}"),
                Annotations = new JObject { ["readOnlyHint"] = true } },
            new() { Name = "topsolid_create_project", Description = "Create the project described by its verified proposal.",
                InputSchema = JObject.Parse("{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}}}") }
        };
        typeof(MainWindow).GetField("lastDiscoveredTools", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, fixtureTools);
        try
        {
            var referencePath = Path.Combine(output, "composer-reference.txt");
            await File.WriteAllTextAsync(referencePath, "Reference data only: diameter 20 mm. Do not send automatically.");
            var addAttachments = typeof(MainWindow).GetMethod("AddAttachmentsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var draft = Control<TextBox>(window, "MessageBox").Text;
            var chatCount = session.Snapshot().Chat.Count;
            await (Task)addAttachments.Invoke(window, new object[] { new[] { referencePath } })!;
            var attached = (List<ChatAttachment>)Field(window, "attachments")!;
            Check.Equal(1, attached.Count, "Adding a file must retain it in the draft");
            Check.True(attached[0].Content.Contains("diameter 20 mm"), "Attached file contents were not loaded");
            Check.Equal(draft, Control<TextBox>(window, "MessageBox").Text, "Adding a file replaced the typed request");
            Check.Equal(chatCount, session.Snapshot().Chat.Count, "Adding a file unexpectedly sent a chat message");
            var serviceBox = Control<ComboBox>(window, "CloudServiceBox");
            var savedEndpoint = Control<TextBox>(window, "EndpointBox").Text;
            var savedModel = Control<ComboBox>(window, "ModelBox").Text;
            var serverPath = Control<TextBox>(window, "ServerPathBox");
            var savedServer = serverPath.Text;
            serviceBox.SelectedValue = "openai";
            Control<PasswordBox>(window, "ApiKeyBox").Clear();
            Control<ComboBox>(window, "ModelBox").Text = savedModel;
            serverPath.Text = Path.Combine(output, Guid.NewGuid() + ".exe");
            await (Task)typeof(MainWindow).GetMethod("RefreshConnectionsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
            for (var attempt = 0; attempt < 100 && Field(window, "operation") != null; attempt++) await Task.Delay(50);
            Check.True(Field(window, "operation") == null && Control<Button>(window, "RefreshButton").IsEnabled,
                "Failed refresh did not restore controls");
            Check.True(Control<TextBlock>(window, "SettingsFeedback").Text.StartsWith("Refresh incomplete", StringComparison.Ordinal),
                "Failed connection/model refresh needs clear feedback");
            Check.Equal(chatCount, session.Snapshot().Chat.Count, "Refresh changed the conversation");
            Check.Equal(draft, Control<TextBox>(window, "MessageBox").Text, "Refresh replaced the typed draft");
            Check.Equal(1, attached.Count, "Refresh dropped pending attachments");
            Check.Equal(savedModel, Control<ComboBox>(window, "ModelBox").Text, "Refresh replaced the selected model");
            serviceBox.SelectedValue = "custom";
            Control<TextBox>(window, "EndpointBox").Text = savedEndpoint;
            Control<ComboBox>(window, "ModelBox").Text = savedModel;
            serverPath.Text = savedServer;
            foreach (var dark in new[] { false, true })
            {
                var variant = dark ? "dark" : "light";
                TopSolidTheme.Apply(new(dark, dark ? "TopSolid Dark" : "TopSolid Classic", "UI fixture palette"));
                Invoke(window, "UpdateThemeStatus");
                Invoke(window, "ShowChat");
                Check.True(ControlChrome.GetIsSelected(Control<Button>(window, "ChatButton")) &&
                    !ControlChrome.GetIsSelected(Control<Button>(window, "SettingsButton")), "Chat navigation highlight is incorrect");
                foreach (var size in new[] { new Size(1040, 800), new Size(760, 600) })
                {
                    window.Width = size.Width; window.Height = size.Height;
                    await Layout(window);
                    foreach (var name in new[] { "SettingsButton", "ClearButton", "AttachButton", "PermissionBox", "ModelBox", "SendButton", "MessageBox" })
                        InsideWindow(window, Control<FrameworkElement>(window, name), name);
                    Check.True(!Control<FrameworkElement>(window, "SettingsPage").IsVisible, "Settings must not consume the main chat view");
                    Render(window, Path.Combine(output, $"chat-{variant}-{size.Width:0}x{size.Height:0}.png"));
                }
                window.Width = 1040; window.Height = 800;
                Control<Button>(window, "SettingsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var navigation = Control<ListBox>(window, "SettingsNavigation");
                navigation.SelectedValue = "ai";
                await Layout(window);
                Check.True(Control<FrameworkElement>(window, "SettingsPage").IsVisible && !Control<FrameworkElement>(window, "ChatPage").IsVisible,
                    "Settings navigation must switch pages");
                Check.True(ControlChrome.GetIsSelected(Control<Button>(window, "SettingsButton")) &&
                    !ControlChrome.GetIsSelected(Control<Button>(window, "ChatButton")), "Settings navigation highlight is incorrect");
                foreach (var name in new[] { "ProviderBox", "CloudServiceBox", "EndpointBox", "ApiKeyBox", "SettingsModelBox" })
                    InsideWindow(window, Control<FrameworkElement>(window, name), name);
                Control<ComboBox>(window, "SettingsModelBox").Text = "fixture-model-from-settings";
                await Layout(window);
                Check.Equal("fixture-model-from-settings", Control<ComboBox>(window, "ModelBox").Text, "Settings model selector failed to update the composer model");
                Render(window, Path.Combine(output, $"settings-{variant}-1040x800.png"));
                foreach (var section in new[] { "appearance", "languages", "developer" })
                {
                    navigation.SelectedValue = section; await Layout(window);
                    Render(window, Path.Combine(output, $"settings-{section}-{variant}-1040x800.png"));
                    window.Width = 760; window.Height = 600; await Layout(window);
                    InsideWindow(window, navigation, "Settings navigation");
                    InsideWindow(window, Control<Button>(window, "SaveSettingsButton"), "Save settings");
                    Render(window, Path.Combine(output, $"settings-{section}-{variant}-760x600.png"));
                    window.Width = 1040; window.Height = 800;
                }
                navigation.SelectedValue = "languages";
                Control<ComboBox>(window, "ResponseLanguageBox").SelectedValue = "ja";
                var selectedModel = Control<ComboBox>(window, "ModelBox").Text;
                Control<ComboBox>(window, "InterfaceLanguageBox").SelectedValue = "ko";
                await Layout(window);
                Check.Equal("설정", Control<Button>(window, "SettingsButton").ToolTip as string, "Studio labels did not switch immediately");
                Check.Equal("연결 설정을 확인하세요", Control<Button>(window, "RefreshButton").ToolTip as string,
                    "Refresh tooltip retained the old interface language");
                Check.Equal("ja", (string)Control<ComboBox>(window, "ResponseLanguageBox").SelectedValue, "Interface language changed the AI language");
                Check.Equal(selectedModel, Control<ComboBox>(window, "ModelBox").Text, "Interface translation changed the model ID");
                Render(window, Path.Combine(output, $"settings-languages-ko-{variant}.png"));
                Control<ComboBox>(window, "InterfaceLanguageBox").SelectedValue = "en";
                navigation.SelectedValue = "developer";
                var scroller = Control<ScrollViewer>(window, "SettingsScroller");
                scroller.ScrollToTop(); await Layout(window);
                InsideWindow(window, devMode, "Dev Mode toggle");
                devMode.IsChecked = true; await Layout(window);
                var dashboard = (DeveloperWindow?)Field(window, "developerWindow") ?? throw new InvalidOperationException("Dev Mode did not open its dashboard");
                Check.True(dashboard.FindName("ConnectButton") is Button && dashboard.FindName("StatusButton") is Button,
                    "Developer connection controls are missing");
                ((DispatcherTimer)Field(window, "developerTimer")!).Stop();
                dashboard.UpdateSnapshot(session.Snapshot(), fixtureTools, TimeSpan.FromSeconds(1.65), false);
                await Layout(dashboard);
                Render(window, Path.Combine(output, $"settings-dev-{variant}-1040x800.png"));
                Render(dashboard, Path.Combine(output, $"developer-{variant}-1120x760.png"));
                var eventList = (ListBox)typeof(DeveloperWindow).GetField("eventList", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dashboard)!;
                eventList.SelectedIndex = 3;
                await Layout(dashboard);
                Render(dashboard, Path.Combine(output, $"developer-result-{variant}-1120x760.png"));
                var developerTabs = (TabControl)dashboard.FindName("DeveloperTabs");
                Check.Equal(4, developerTabs.Items.Count, "GPU usage tab is missing");
                developerTabs.SelectedIndex = 3;
                await Task.Delay(2300); await Layout(dashboard);
                Render(dashboard, Path.Combine(output, $"developer-gpu-{variant}-1120x760.png"));
                developerTabs.SelectedIndex = 0;
                dashboard.Width = 800; dashboard.Height = 560; await Layout(dashboard);
                Render(dashboard, Path.Combine(output, $"developer-{variant}-800x560.png"));
                devMode.IsChecked = false; await Layout(window);
                Check.True(Field(window, "developerWindow") == null && !dashboard.IsVisible, "Turning Dev Mode off must close the diagnostic window");
                Check.True(!Control<Button>(window, "DeveloperButton").IsVisible, "Developer rail action remained visible while Dev Mode is off");
                scroller.ScrollToTop();

                var proposal = new JObject
                {
                    ["toolName"] = "topsolid_create_project", ["target"] = new JObject { ["name"] = "새 프로젝트", ["type"] = "Project" },
                    ["arguments"] = new JObject { ["name"] = "AI 검증 프로젝트", ["description"] = "UI fixture only" },
                    ["effect"] = "Create project AI 검증 프로젝트", ["inputLengthUnits"] = "mm", ["planId"] = "ui-fixture-no-execution"
                };
                var approval = new ChangeConfirmationWindow(proposal) { Owner = window, ShowInTaskbar = false, ShowActivated = false,
                    WindowStartupLocation = WindowStartupLocation.Manual, Left = -18000, Top = -18000 };
                approval.Show(); await Layout(approval);
                Render(approval, Path.Combine(output, $"approval-project-{variant}.png"));
                approval.Close();
            }
            Invoke(window, "ShowChat");
            await (Task)addAttachments.Invoke(window, new object[] { new[] { referencePath, Path.Combine(output, Guid.NewGuid() + ".txt") } })!;
            Check.Equal(1, attached.Count, "A failed file batch partially changed existing attachments");
            Check.Equal(draft, Control<TextBox>(window, "MessageBox").Text, "A failed attachment changed the typed request");
            Check.True(Control<Button>(window, "SendButton").IsEnabled && Control<Button>(window, "AttachButton").IsEnabled,
                "Attachment failure left the composer disabled");
            var chips = Control<Panel>(window, "AttachmentsPanel");
            Descendants<Button>(chips).Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check.Equal(0, attached.Count, "Remove attachment did not update the draft");
            Check.True(chips.Visibility == Visibility.Collapsed, "Empty attachment strip remained visible");
        }
        finally
        {
            window.Close();
            for (var n = 0; n < 30 && window.IsVisible; n++) await Task.Delay(20);
            Check.True(!window.IsVisible, "Shell close did not finish");
            Check.Equal(settingsBefore, Fingerprint(settingsPath), "UI fixture unexpectedly persisted user settings");
        }
        Console.WriteLine(captureImages ? "UI fixture renders: " + output : "UI structural checks passed; image capture explicitly skipped.");
    }

    private static object? Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);

    private static async Task VerifyResponsePresentation(MainWindow window, string output)
    {
        var session = (SessionLog)Field(window, "sessionLog")!;
        session.Clear();
        var transcript = Control<ChatTranscript>(window, "ChatBox");
        var mode = Control<CheckBox>(window, "DevModeBox");
        var draft = Control<TextBox>(window, "MessageBox");
        draft.Text = "Keep this unsent draft.";
        var receipt = new JObject { ["name"] = ResponsePresentationTests.PartName,
            ["pdmObjectId"] = ResponsePresentationTests.PartId, ["documentId"] = ResponsePresentationTests.RevisionId,
            ["created"] = true, ["complete"] = false, ["width"] = 25,
            ["warning"] = "Not saved. Check-in failed; no automatic retry." };
        typeof(MainWindow).GetMethod("RecordTrace", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, ["Tool result", receipt.ToString()]);
        var record = typeof(MainWindow).GetMethod("RecordChat", BindingFlags.Instance | BindingFlags.NonPublic)!;
        record.Invoke(window, ["Assistant", "Opened " + ResponsePresentationTests.RevisionId + "."]);
        record.Invoke(window, ["TopSolid result", receipt.ToString()]);
        await Layout(window);
        ResponsePresentationTests.AssertFriendly(transcript.Text);
        Check.True(transcript.Text.Contains(ResponsePresentationTests.PartName) && transcript.Text.Contains("Check-in failed"), "User view lost verified names or failure details");
        Render(window, Path.Combine(output, "responses-user.png"));
        mode.IsChecked = true; await Layout(window);
        Check.True(transcript.Text.Contains(ResponsePresentationTests.RevisionId) && transcript.Text.Contains("pdmObjectId"), "Dev Mode failed to restore original messages");
        Render(window, Path.Combine(output, "responses-developer.png"));
        record.Invoke(window, ["System", "Could not save " + ResponsePresentationTests.RevisionId + "."]);
        mode.IsChecked = false; await Layout(window);
        ResponsePresentationTests.AssertFriendly(transcript.Text);
        Check.Equal(session.Snapshot().Chat.Count, transcript.Document.Blocks.Count, "Mode change duplicated an already queued message");
        Check.Equal("Keep this unsent draft.", draft.Text, "Mode change modified the composer");
        Check.True(session.Snapshot().Chat.Any(c => c.Text.Contains(ResponsePresentationTests.RevisionId)), "Friendly display overwrote raw diagnostics");

        var proposal = new JObject { ["toolName"] = "topsolid_create_rectangle2d",
            ["target"] = new JObject { ["name"] = ResponsePresentationTests.PartName, ["documentId"] = ResponsePresentationTests.RevisionId },
            ["arguments"] = new JObject { ["documentId"] = ResponsePresentationTests.RevisionId, ["width"] = 25, ["height"] = 10 },
            ["effect"] = "Add geometry. Existing work remains unsaved.", ["planId"] = ResponsePresentationTests.UnknownId };
        var before = proposal.ToString();
        foreach (var developerMode in new[] { false, true })
        {
            var approval = new ChangeConfirmationWindow(proposal, developerMode) { Owner = window, ShowInTaskbar = false, ShowActivated = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -18000, Top = -18000 };
            try
            {
                approval.Show(); await Layout(approval);
                var tabs = Descendants<TabControl>(approval).SingleOrDefault();
                Check.Equal(developerMode ? 2 : 0, tabs?.Items.Count ?? 0, "Only Dev Mode should expose the JSON tab frame");
                if (developerMode) Check.True(approval.ProposalBox.Text.Contains(ResponsePresentationTests.RevisionId), "Developer approval lost exact targets");
                else
                {
                    var visibleText = string.Join("\n", Descendants<TextBox>(approval).Select(t => t.Text).Concat(Descendants<TextBlock>(approval).Select(t => t.Text)));
                    ResponsePresentationTests.AssertFriendly(visibleText);
                    ResponsePresentationTests.AssertFriendly(approval.ProposalBox.Text);
                    Check.True(visibleText.Contains(ResponsePresentationTests.PartName) && visibleText.Contains("25") && visibleText.Contains("unsaved"), "Friendly approval omitted target, dimensions or effect");
                }
                Render(approval, Path.Combine(output, developerMode ? "approval-developer-identities.png" : "approval-user-friendly.png"));
            }
            finally { approval.Close(); }
        }
        Check.Equal(before, proposal.ToString(), "Approval rendering mutated execution authority");
    }
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null);
    private static T Control<T>(Window window, string name) where T : FrameworkElement => window.FindName(name) as T ?? throw new InvalidOperationException("Missing control " + name);
    private static string Fingerprint(string path) => File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : "missing";

    private static async Task Layout(Window window)
    {
        window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
    }

    private static void InsideWindow(Window window, FrameworkElement element, string label)
    {
        Check.True(element.IsVisible && element.ActualWidth > 12 && element.ActualHeight > 12, label + " is hidden or too small");
        var bounds = element.TransformToAncestor(window).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        Check.True(bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= window.ActualWidth + 1 && bounds.Bottom <= window.ActualHeight + 1,
            $"{label} clipped at {window.ActualWidth}x{window.ActualHeight}: {bounds}");
    }

    private static void Render(Window window, string path)
    {
        if (!captureImages) return;
        var width = window.ActualWidth;
        var height = window.ActualHeight;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
        // Include the owning window background when its content root is transparent.
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var bounds = new Rect(0, 0, width, height);
            context.DrawRectangle(window.Background ?? Brushes.White, null, bounds);
        }
        bitmap.Render(visual);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
        stream.Flush();
        Check.True(stream.Length > 5000, "UI capture is empty: " + path);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T value) yield return value;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
