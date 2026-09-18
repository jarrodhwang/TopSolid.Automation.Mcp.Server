using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.Tests;

internal static class DialogThemeUiTests
{
    internal static async Task Run(MainWindow owner, Action<Window, string> render)
    {
        var originalTheme = TopSolidTheme.Current; var originalLanguage = StudioStrings.CurrentLanguage;
        try
        {
            StudioStrings.Apply("en");
            var error = Position(new ErrorWindow("The selected operation is no longer available. Refresh the operation list and choose it again.", "Fixture only: operation changed after preparation."), owner);
            try
            {
                error.Show(); await Layout(error);
                VerifyFrame(error); VerifyFrame(owner);
                foreach (var dark in new[] { false, true })
                {
                    // Change resources on the same open dialog: no reopen, duplicate chrome or stale palette.
                    TopSolidTheme.Apply(new(dark, "Dialog fixture", "UI test")); await Layout(error);
                    var expected = (Brush)Application.Current.FindResource("ToolbarGradientBrush");
                    var header = Descendants<Border>(error).Single(b => b.Style == error.FindResource("DialogToolbar"));
                    Check.True(ReferenceEquals(expected, header.Background), "An open dialog retained the previous toolbar palette");
                    Check.Equal(dark ? "#FF292929" : "#FFF6F6FA", error.Background.ToString(), "Dialog background did not follow theme");
                    Check.Equal(1, Descendants<WindowTitleBar>(error).Count(), "Theme refresh duplicated the caption");
                    render(error, "error-" + (dark ? "dark" : "light") + ".png");
                }
                StudioStrings.Apply("ko"); await Layout(error);
                Check.True(error.Title.Contains("요청"), "Open error title did not follow language");
                Check.True(Descendants<Button>(error).Any(b => AutomationProperties.GetName(b) == "닫기"), "Caption buttons are not localized");
                error.Width = error.MinWidth; error.Height = error.MinHeight; await Layout(error);
                Descendants<Expander>(error).Single().IsExpanded = true; await Layout(error);
                render(error, "error-ko-minimum.png");
                var custom = new Dictionary<string, string> { ["global_dialog_back"] = "#20252A", ["iconsbar_toolstrip_background"] = "#324350", ["iconsbar_background_right"] = "#253744", ["tabcontrol_up"] = "#405060", ["tabcontrol_down"] = "#203040" };
                TopSolidTheme.Apply(new(true, "Workshop", "fixture", custom)); await Layout(error);
                Check.Equal(Color.FromRgb(50, 67, 80), ((LinearGradientBrush)error.FindResource("ToolbarGradientBrush")).GradientStops[0].Color, "Custom TopSolid toolbar palette was discarded");
                Check.Equal(Color.FromRgb(64, 80, 96), ((LinearGradientBrush)error.FindResource("ButtonGradientBrush")).GradientStops[0].Color, "Custom command palette was discarded");
            }
            finally { error.Close(); }
            StudioStrings.Apply("en");
            var before = owner.OwnedWindows.Count;
            typeof(MainWindow).GetMethod("ShowError", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, [new InvalidOperationException("Refresh the selected document before retrying.")]);
            Check.Equal(before, owner.OwnedWindows.Count, "Automatic failure opened an unsolicited modal");
            Check.True(((Button)owner.FindName("ErrorButton")).IsVisible, "Handled errors have no route to themed details");
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            var opened = false;
            timer.Tick += (_, _) =>
            {
                var dialog = owner.OwnedWindows.OfType<ErrorWindow>().FirstOrDefault(); if (dialog == null) return;
                opened = true; timer.Stop();
                Check.True(!Descendants<Expander>(dialog).Any(), "User error details expose developer internals");
                CaptionClose(dialog).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };
            timer.Start();
            try { ((Button)owner.FindName("ErrorButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            finally { timer.Stop(); }
            Check.True(opened, "Error command did not open the themed error dialog");
            Func<CancellationToken, Task> recovered = _ => Task.CompletedTask;
            await (Task)typeof(MainWindow).GetMethod("RunOperation", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, [recovered])!;
            Check.True(((Button)owner.FindName("ErrorButton")).Visibility == Visibility.Collapsed, "A successful retry retained an obsolete error indicator");
            await VerifyCaptionCancels(owner);
            await VerifyScrolling(owner);
            Console.WriteLine("Dialog themes: shared captions, live light/dark/custom palettes, localized controls, error entry point, safe close and scrolling checked.");
        }
        finally { StudioStrings.Apply(originalLanguage); TopSolidTheme.Apply(originalTheme); }
    }

    private static async Task VerifyCaptionCancels(Window owner)
    {
        var question = Position(new QuestionWindow(UserQuestionTests.ProjectQuestion()), owner);
        question.Loaded += (_, _) => question.Dispatcher.BeginInvoke(() => CaptionClose(question).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
        Check.True(question.ShowDialog() != true && question.Answer == null, "Caption close answered a question");
        var proposal = new JObject { ["toolName"] = "topsolid_create_project", ["arguments"] = new JObject { ["name"] = "Test" }, ["target"] = new JObject { ["name"] = "Test" } };
        var approval = Position(new ChangeConfirmationWindow(proposal), owner);
        approval.Loaded += (_, _) => approval.Dispatcher.BeginInvoke(() => CaptionClose(approval).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
        Check.True(approval.ShowDialog() != true, "Caption close approved a CAD change");
        await Layout(owner);
    }

    private static async Task VerifyScrolling(Window owner)
    {
        var window = Position(new ErrorWindow(string.Join("\n", Enumerable.Range(0, 100).Select(i => $"Diagnostic line {i}: fixture text for scroll navigation."))), owner);
        try
        {
            window.Show(); await Layout(window);
            var scroll = Descendants<ScrollViewer>(window).First(); scroll.ScrollToEnd(); await Layout(window);
            Check.True(scroll.VerticalOffset > 0, "Themed scrollbar cannot reach long content");
            var track = Descendants<Track>(scroll).First(t => t.Orientation == Orientation.Vertical);
            Check.True(track.Value > 0 && track.Thumb.ActualHeight > 0, "Scrollbar thumb lost its viewport/value binding");
            scroll.ScrollToHome(); await Layout(window); Check.Equal(0d, scroll.VerticalOffset, "Scroll home did not return to the beginning");
        }
        finally { window.Close(); }
    }

    private static void VerifyFrame(Window window)
    {
        var caption = Descendants<WindowTitleBar>(window).Single();
        Check.True(window.WindowStyle == WindowStyle.None && !window.AllowsTransparency, "Shared chrome must retain hardware-compatible window rendering");
        Check.Equal(34d, WindowChrome.GetWindowChrome(window).CaptionHeight, "Caption hit region does not match visible title");
        Check.True(caption.ActualHeight == 34 && CaptionClose(window).IsVisible, "Shared caption has no accessible close action");
        var content = (FrameworkElement)window.Content;
        Check.True(content.TransformToAncestor(window).Transform(new Point()).Y >= 34, "Native drag area overlaps dialog controls");
    }

    private static Button CaptionClose(Window window) => Descendants<Button>(Descendants<WindowTitleBar>(window).Single()).Single(b => AutomationProperties.GetName(b) == StudioStrings.Get("Window.Close"));
    private static T Position<T>(T window, Window owner) where T : Window { window.Owner = owner; window.ShowInTaskbar = false; window.ShowActivated = false; window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -20000; window.Top = -20000; return window; }
    private static async Task Layout(Window window) { window.UpdateLayout(); await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle); }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T value) yield return value; foreach (var item in Descendants<T>(child)) yield return item; }
    }
}
