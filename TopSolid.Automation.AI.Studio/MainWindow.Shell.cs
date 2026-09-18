using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio;

public partial class MainWindow
{
    private TopSolidThemeFollower? themeFollower;
    private DeveloperWindow? developerWindow;
    private readonly DispatcherTimer developerTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly List<ChatAttachment> attachments = [];
    private PermissionMode permissionMode = PermissionMode.AskForApproval;
    private bool loadingAttachments;
    private bool composingText;

    private void InitializeShell()
    {
        WelcomeIcon.Source = Icon = TopSolidIcons.Get("app");
        SettingsIcon.Source = TopSolidIcons.Get("settings");
        ClearButtonIcon.Source = TopSolidIcons.Get("document");
        ChatIcon.Source = TopSolidIcons.Get("window");
        DeveloperButtonIcon.Source = TopSolidIcons.Get("developer");
        RefreshIcon.Source = TopSolidIcons.Get("refresh");
        themeFollower = new TopSolidThemeFollower(this, settings.AppearanceMode);
        themeFollower.Changed += (_, _) => UpdateThemeStatus();
        UpdateThemeStatus();
        developerTimer.Tick += (_, _) => RefreshDeveloper();
        TextCompositionManager.AddPreviewTextInputStartHandler(MessageBox, (_, _) => composingText = true);
        TextCompositionManager.AddPreviewTextInputHandler(MessageBox, (_, _) => composingText = false);
        MessageBox.LostKeyboardFocus += (_, _) => composingText = false;
        MessageBox.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) composingText = false; };
        Deactivated += (_, _) => DropOverlay.Visibility = Visibility.Collapsed;
    }

    private void UpdateThemeStatus()
    {
        if (themeFollower == null) return;
        var source = StudioStrings.Get("Appearance." + char.ToUpperInvariant(themeFollower.Mode[0]) + themeFollower.Mode[1..]);
        if (themeFollower.Mode == "topsolid") source = StudioStrings.Get("Appearance.TopSolid");
        ThemeStatusText.Text = themeFollower.Mode switch
        {
            "topsolid" => source + " · " + StudioStrings.Text(themeFollower.Current.Name),
            "system" => source + " · " + StudioStrings.Get(themeFollower.Current.IsDark ? "Appearance.Dark" : "Appearance.Light"),
            _ => source
        };
        ThemeStatusText.ToolTip = themeFollower.Current.Source;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        SettingsPage.Visibility = Visibility.Visible;
        ChatPage.Visibility = Visibility.Collapsed;
        DropOverlay.Visibility = Visibility.Collapsed;
        ControlChrome.SetIsSelected(SettingsButton, true);
        ControlChrome.SetIsSelected(ChatButton, false);
    }
    private void Chat_Click(object sender, RoutedEventArgs e) => ShowChat();
    private void ShowChat()
    {
        SettingsPage.Visibility = Visibility.Collapsed;
        ChatPage.Visibility = Visibility.Visible;
        ControlChrome.SetIsSelected(SettingsButton, false);
        ControlChrome.SetIsSelected(ChatButton, true);
        MessageBox.Focus();
    }
    private void Suggestion_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Text = (string)((Button)sender).Tag;
        MessageBox.Focus(); MessageBox.CaretIndex = MessageBox.Text.Length;
    }
    private void Message_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ComposerPlaceholder != null)
            ComposerPlaceholder.Visibility = MessageBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Permission_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loading) return;
        permissionMode = PermissionBox.SelectedIndex switch
        {
            1 => PermissionMode.ApproveForMe,
            2 => PermissionMode.FullAccess,
            _ => PermissionMode.AskForApproval
        };
        RecordTrace("Permission", $"Selected {permissionMode} for this application session.");
        PermissionBox.ToolTip = PermissionPolicy.Description(permissionMode);
    }

    private async void Attach_Click(object sender, RoutedEventArgs e)
    {
        if (operation != null || loadingAttachments) return;
        var picker = new OpenFileDialog { Title = StudioStrings.Text("Add files to this message"), Multiselect = true,
            Filter = ChatAttachments.FileDialogFilter, CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        await AddAttachmentsAsync(picker.FileNames);
    }

    // Picker and drag/drop use the same bounded read and atomic addition path.
    private async Task AddAttachmentsAsync(string[] paths)
    {
        if (operation != null || loadingAttachments || closing || paths.Length == 0) return;
        loadingAttachments = true;
        AttachButton.IsEnabled = SendButton.IsEnabled = ClearButton.IsEnabled = false;
        AttachmentsPanel.IsEnabled = false;
        try
        {
            if (attachments.Count + paths.Length > ChatAttachments.MaximumFiles)
                throw new ArgumentException($"Attach up to {ChatAttachments.MaximumFiles} files per message.");
            var added = new List<ChatAttachment>();
            foreach (var path in paths)
                added.Add(await ChatAttachments.LoadAsync(path, CancellationToken.None));
            var combined = attachments.Concat(added).ToArray();
            ChatAttachments.Validate(combined);
            if (closing) return;
            attachments.AddRange(added);
            RenderAttachments();
            MessageBox.Focus();
        }
        catch (Exception ex) { if (!closing) ShowError(ex); }
        finally
        {
            loadingAttachments = false;
            if (!closing) { AttachButton.IsEnabled = SendButton.IsEnabled = ClearButton.IsEnabled = AttachmentsPanel.IsEnabled = operation == null; }
        }
    }

    private void Files_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: false)) return;
        e.Handled = true;
        var available = operation == null && !loadingAttachments && !closing;
        e.Effects = available && (e.AllowedEffects & DragDropEffects.Copy) != 0 ? DragDropEffects.Copy : DragDropEffects.None;
        DropOverlay.Visibility = e.Effects == DragDropEffects.Copy ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Files_DragLeave(object sender, DragEventArgs e)
    {
        if (!new Rect(ChatPage.RenderSize).Contains(e.GetPosition(ChatPage))) DropOverlay.Visibility = Visibility.Collapsed;
    }

    private async void Files_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: false)) return;
        e.Handled = true;
        e.Effects = DragDropEffects.None;
        if (operation != null || loadingAttachments || closing || (e.AllowedEffects & DragDropEffects.Copy) == 0) return;
        try
        {
            if (e.Data.GetData(DataFormats.FileDrop, autoConvert: false) is string[] paths)
            {
                e.Effects = DragDropEffects.Copy;
                await AddAttachmentsAsync(paths);
            }
        }
        catch (Exception ex) { e.Effects = DragDropEffects.None; if (!closing) ShowError(ex); }
    }

    private void RenderAttachments()
    {
        AttachmentsPanel.Children.Clear();
        foreach (var attachment in attachments)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new Image { Source = TopSolidIcons.Get("attachment"), Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0) });
            panel.Children.Add(new TextBlock { Text = attachment.Name, MaxWidth = 170, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
            var remove = new Button { Content = "×", Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(6, 0, 0, 0), BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                ToolTip = StudioStrings.Get("Common.Remove", attachment.Name) };
            System.Windows.Automation.AutomationProperties.SetName(remove, StudioStrings.Get("Common.Remove", attachment.Name));
            remove.Click += (_, _) => { attachments.Remove(attachment); RenderAttachments(); };
            panel.Children.Add(remove);
            var chip = new Border { Child = panel, Padding = new Thickness(10, 5, 6, 5), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 6, 6),
                ToolTip = $"{attachment.Name} · {attachment.ByteLength:N0} bytes\n" + StudioStrings.Text("Included with your next message to the selected AI provider.") };
            chip.SetResourceReference(BackgroundProperty, "PanelBrush");
            AttachmentsPanel.Children.Add(chip);
        }
        AttachmentsPanel.Visibility = attachments.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void DevMode_Changed(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        settings.DevMode = DevModeBox.IsChecked == true;
        if (session != null) session.DeveloperMode = settings.DevMode;
        RefreshChatPresentation();
        SettingsFeedback.Text = "";
        DeveloperButton.Visibility = OpenDeveloperButton.Visibility = settings.DevMode ? Visibility.Visible : Visibility.Collapsed;
        if (settings.DevMode) OpenDeveloper();
        else { developerWindow?.Close(); developerTimer.Stop(); }
    }
    private void Developer_Click(object sender, RoutedEventArgs e) => OpenDeveloper();
    private void OpenDeveloper()
    {
        if (DevModeBox.IsChecked != true || closing) return;
        DeveloperButton.Visibility = OpenDeveloperButton.Visibility = Visibility.Visible;
        if (developerWindow == null)
        {
            developerWindow = new DeveloperWindow(() => SaveLog_Click(this, new RoutedEventArgs()),
                () => Connect_Click(this, new RoutedEventArgs()), () => Status_Click(this, new RoutedEventArgs()))
                { Owner = this, ShowActivated = ShowActivated, ShowInTaskbar = ShowInTaskbar };
            developerWindow.Closed += (_, _) => { developerWindow = null; developerTimer.Stop(); ControlChrome.SetIsSelected(DeveloperButton, false); };
            developerWindow.Show(); developerTimer.Start();
            ControlChrome.SetIsSelected(DeveloperButton, true);
        }
        else { if (developerWindow.WindowState == WindowState.Minimized) developerWindow.WindowState = WindowState.Normal; developerWindow.Activate(); }
        RefreshDeveloper();
        developerWindow?.UpdateConnectionState(mcp.IsConnected, operation != null, mcp.IsMutationInFlight);
    }
    private void RefreshDeveloper()
    {
        if (developerWindow is not { IsVisible: true }) return;
        developerWindow.UpdateSnapshot(sessionLog.Snapshot(), mcp.Tools.Count > 0 ? mcp.Tools : lastDiscoveredTools, chatClock.Elapsed, chatClock.IsRunning);
    }
}
