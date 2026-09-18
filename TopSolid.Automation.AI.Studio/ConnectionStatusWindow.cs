using System.Windows;
using System.Windows.Controls;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Connections;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio;

public sealed class ConnectionStatusWindow : Window
{
    private string? renderedState;
    private readonly StackPanel rows = new();
    private readonly TextBlock heading = new() { FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 18) };
    private readonly TextBlock note = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 0) };
    private readonly Button cancel = new() { IsCancel = true, IsDefault = true, MinWidth = 100, Padding = new Thickness(12, 7, 12, 7) };
    public Button RefreshButton { get; } = new() { MinWidth = 110, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 7, 12, 7) };

    public ConnectionStatusWindow()
    {
        StudioStrings.InitializeResources(this);
        Width = 560; Height = 470; MinWidth = 420; MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "WindowBrush"); SetResourceReference(ForegroundProperty, "TextBrush");
        TopSolidTheme.ApplyWindow(this);
        var layout = new DockPanel { Margin = new Thickness(22) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        RefreshButton.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(cancel); buttons.Children.Add(RefreshButton);
        DockPanel.SetDock(buttons, Dock.Bottom); layout.Children.Add(buttons);
        DockPanel.SetDock(heading, Dock.Top); layout.Children.Add(heading);
        var body = new StackPanel(); body.Children.Add(rows); body.Children.Add(note);
        layout.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = layout;
        Loaded += (_, _) => cancel.Focus();
    }

    public void Update(ConnectionHealth health, bool canRefresh)
    {
        var state = StudioStrings.CurrentLanguage + canRefresh + string.Join("|", health.Conditions.Select(c => c.Component + c.Severity + c.MessageKey));
        if (renderedState == state) return;
        renderedState = state;
        Title = heading.Text = StudioStrings.Get("Health.Title");
        cancel.Content = StudioStrings.Get("Common.Cancel"); RefreshButton.Content = StudioStrings.Get("Common.Refresh");
        RefreshButton.IsEnabled = canRefresh;
        Icon = TopSolidIcons.Get(health.Severity == ConnectionSeverity.Error ? "refresh-error" : health.HasProblems ? "refresh-warning" : "refresh");
        rows.Children.Clear();
        foreach (var condition in health.Conditions)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
            row.Children.Add(new Image { Source = TopSolidIcons.Get(condition.Severity == ConnectionSeverity.Error ? "refresh-error" :
                condition.Severity == ConnectionSeverity.Warning ? "refresh-warning" : "refresh"), Width = 26, Height = 26, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 12, 0) });
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = StudioStrings.Get("Health.Component." + (condition.Component == "AiRuntime" ? "Ai" : condition.Component)), FontWeight = FontWeights.SemiBold, FontSize = 14 });
            text.Children.Add(new TextBlock { Text = StudioStrings.Get(condition.MessageKey), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), FontSize = 13 });
            row.Children.Add(text); rows.Children.Add(row);
        }
        note.Text = StudioStrings.Get(canRefresh ? "Health.VerificationNote" : "Health.WaitNote");
    }
}
