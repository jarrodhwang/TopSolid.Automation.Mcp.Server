using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TopSolid.Automation.AI.Studio;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Connections;
using TopSolid.Automation.AI.Studio.Diagnostics;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.Tests;

internal static partial class UiShellTests
{
    private static async Task VerifyConnectionIndicator(MainWindow window, string output)
    {
        var health = (ConnectionHealth)Field(window, "connectionHealth")!;
        var running = typeof(MainWindow).GetField("refreshRunning", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var rotation = (RotateTransform)Field(window, "refreshRotation")!;
        foreach (var condition in health.Conditions) health.Remove(condition.Component);
        running.SetValue(window, true);
        foreach (var component in new[] { "Mcp", "TopSolid", "Ai" }) health.Pending(component, "Health." + component + "Checking");
        await Layout(window);
        Check.True(rotation.HasAnimatedProperties && (bool)Field(window, "refreshSpinning")!, "Checking did not start the compositor animation");
        Check.True(ReferenceEquals(TopSolidIcons.Get("refresh"), Control<Image>(window, "RefreshIcon").Source), "Pending check did not use the supplied normal icon");
        await Task.Delay(150);
        Check.True(rotation.Angle > 0 && rotation.Angle < 360, "Refresh animation is not advancing smoothly");
        running.SetValue(window, false);
        foreach (var component in new[] { "Mcp", "TopSolid", "Ai" }) health.Set(component, ConnectionSeverity.Ready, "Health." + component + "Ready");
        Check.True(!rotation.HasAnimatedProperties && rotation.Angle == 0, "Ready state left a rotating icon");
        Check.Equal("Refresh complete", Control<Button>(window, "RefreshButton").ToolTip as string, "Ready feedback");
        health.Set("TopSolid", ConnectionSeverity.Warning, "Health.TopSolidNotRunning");
        Check.True(ReferenceEquals(TopSolidIcons.Get("refresh-warning"), Control<Image>(window, "RefreshIcon").Source), "Recoverable condition did not use the supplied warning-refresh icon");
        var session = (SessionLog)Field(window, "sessionLog")!;
        var beforeCancel = session.Snapshot().Trace.Count(t => t.Kind == "Refresh");
        ExerciseStatusModal(window, retry: false);
        Check.Equal(beforeCancel, session.Snapshot().Trace.Count(t => t.Kind == "Refresh"), "Cancel retried a connection");

        var model = Control<ComboBox>(window, "ModelBox"); var server = Control<TextBox>(window, "ServerPathBox");
        var savedModel = model.Text; var savedPath = server.Text;
        try
        {
            model.Text = ""; server.Text = System.IO.Path.Combine(output, "missing-mcp-server.exe");
            health.Set("Mcp", ConnectionSeverity.Error, "Health.McpUnavailable");
            Check.True(ReferenceEquals(TopSolidIcons.Get("refresh-error"), Control<Image>(window, "RefreshIcon").Source), "Crucial failure did not use the supplied red error icon");
            ExerciseStatusModal(window, retry: true);
            for (var attempt = 0; attempt < 100 && Field(window, "operation") != null; attempt++) await Task.Delay(50);
            Check.True(Field(window, "operation") == null, "Retry left the application busy");
            Check.Equal(beforeCancel + 1, session.Snapshot().Trace.Count(t => t.Kind == "Refresh"), "Retry did not run exactly one check");
            Check.True(health.Conditions.Any(c => c.MessageKey == "Health.AiConfiguration") && health.Conditions.Any(c => c.MessageKey == "Health.McpUnavailable"), "One failed dependency hid the other failure");
            Check.True(!rotation.HasAnimatedProperties, "Failed check left its animation running");

            foreach (var dark in new[] { false, true })
            {
                TopSolidTheme.Apply(new TopSolidThemeSnapshot(dark, dark ? "TopSolid Dark" : "TopSolid Classic", "Connection UI fixture"));
                var dialog = new ConnectionStatusWindow { Owner = window, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -18000, Top = -18000 };
                try
                {
                    dialog.Update(health, canRefresh: true); dialog.Show(); await Layout(dialog);
                    Check.True(Descendants<Button>(dialog).Single(b => b.IsCancel).IsDefault, "Status dialog should default to Cancel");
                    var text = string.Join("\n", Descendants<TextBlock>(dialog).Select(t => t.Text));
                    Check.True(text.Contains("MCP server") && text.Contains("not fully configured") && !text.Contains("missing-mcp-server.exe"), "Status dialog omitted readable errors or exposed a raw path");
                    Render(dialog, System.IO.Path.Combine(output, "connection-errors-" + (dark ? "dark" : "light") + "-en.png"));
                    StudioStrings.Apply("ko"); dialog.Update(health, canRefresh: false); await Layout(dialog);
                    Check.Equal("연결 상태", dialog.Title, "Status title did not translate");
                    Check.Equal("새로 고침", (string)dialog.RefreshButton.Content, "Retry button did not translate");
                    Check.True(!dialog.RefreshButton.IsEnabled, "Pending work allowed overlapping refreshes");
                    Render(dialog, System.IO.Path.Combine(output, "connection-errors-" + (dark ? "dark" : "light") + "-ko.png"));
                }
                finally { dialog.Close(); StudioStrings.Apply("en"); }
            }
        }
        finally { model.Text = savedModel; server.Text = savedPath; }

        // Production constructor path: Loaded automatically attempts both services once.
        // Empty model and an absent executable prevent any external provider/CAD calls.
        var startup = new MainWindow { ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
        try
        {
            Control<CheckBox>(startup, "DevModeBox").IsChecked = false;
            Control<ComboBox>(startup, "InterfaceLanguageBox").SelectedValue = "en";
            Control<ComboBox>(startup, "ModelBox").Text = "";
            Control<TextBox>(startup, "ServerPathBox").Text = System.IO.Path.Combine(output, "missing-startup-server.exe");
            startup.Show(); await Layout(startup);
            for (var attempt = 0; attempt < 100 && Field(startup, "operation") != null; attempt++) await Task.Delay(50);
            Check.True((bool)Field(startup, "startupConnectionStarted")!, "Startup did not initiate connection checks");
            var startupLog = (SessionLog)Field(startup, "sessionLog")!;
            Check.Equal(1, startupLog.Snapshot().Trace.Count(t => t.Kind == "Refresh"), "Startup did not complete one automatic check");
            startup.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); await Layout(startup);
            Check.Equal(1, startupLog.Snapshot().Trace.Count(t => t.Kind == "Refresh"), "Repeated Loaded events repeated startup network checks");
            Check.True(startup.OwnedWindows.OfType<ConnectionStatusWindow>().All(d => !d.IsVisible), "Startup failure unexpectedly interrupted the user with a modal");
            Render(startup, System.IO.Path.Combine(output, "connection-startup-error.png"));
        }
        finally
        {
            startup.Close();
            for (var n = 0; n < 100 && startup.IsVisible; n++) await Task.Delay(20);
            Check.True(!startup.IsVisible, "Startup error prevented closing");
        }
    }

    private static void ExerciseStatusModal(MainWindow window, bool retry)
    {
        Exception? failure = null; var observed = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            var dialog = window.OwnedWindows.OfType<ConnectionStatusWindow>().FirstOrDefault(d => d.IsVisible);
            if (dialog == null) return;
            timer.Stop(); observed = true;
            try
            {
                Check.True(dialog.RefreshButton.IsEnabled, "Idle status dialog disabled retry");
                (retry ? dialog.RefreshButton : Descendants<Button>(dialog).Single(b => b.IsCancel)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception error) { failure = error; dialog.Close(); }
        };
        timer.Start();
        try { Control<Button>(window, "RefreshButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
        finally { timer.Stop(); }
        if (failure != null) throw failure;
        Check.True(observed, "Clicking the status icon did not open its modal explanation");
    }
}
