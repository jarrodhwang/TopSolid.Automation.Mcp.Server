using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio;

/// <summary>Explicitly opened error review. Automatic failures stay in chat and logs, without modal loops.</summary>
public sealed class ErrorWindow : Window
{
    public ErrorWindow(string message, string? developerDetails = null)
    {
        StudioStrings.InitializeResources(this);
        SetResourceReference(TitleProperty, "Ui.Error.Title");
        Icon = TopSolidIcons.Get("error");
        Width = 600; Height = 380; MinWidth = 440; MinHeight = 300;
        ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        TopSolidTheme.ApplyWindow(this);
        var layout = new DockPanel();
        var close = new Button { IsCancel = true, IsDefault = true, MinWidth = 100, HorizontalAlignment = HorizontalAlignment.Right };
        close.SetResourceReference(ContentControl.ContentProperty, "Ui.Window.Close");
        DialogLayout.Command(close, "cancel"); close.Click += (_, _) => Close();
        var footer = DialogLayout.Footer(close); DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
        var title = new TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        title.SetResourceReference(TextBlock.TextProperty, "Ui.Error.Title");
        var header = DialogLayout.Toolbar(DialogLayout.Heading(Icon, title)); DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header);
        var body = new StackPanel();
        var text = new TextBox { Text = message[..Math.Min(message.Length, 24000)], IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalContentAlignment = VerticalAlignment.Top,
            BorderThickness = new Thickness(0), Padding = new Thickness(0), FontSize = 14 };
        text.SetResourceReference(BackgroundProperty, "WindowBrush"); body.Children.Add(text);
        if (!string.IsNullOrEmpty(developerDetails))
        {
            var details = new Expander { Margin = new Thickness(0, 12, 0, 0) };
            details.SetResourceReference(HeaderedContentControl.HeaderProperty, "Ui.Error.Details");
            details.Content = new TextBox { Text = developerDetails[..Math.Min(developerDetails.Length, 24000)], IsReadOnly = true,
                FontFamily = new FontFamily("Consolas"), FontSize = 12, MaxHeight = 170, TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            body.Children.Add(details);
        }
        layout.Children.Add(DialogLayout.Body(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled })); Content = layout;
    }
}
