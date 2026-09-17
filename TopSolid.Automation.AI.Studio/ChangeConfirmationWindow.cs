using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.AI.Studio;

/// <summary>Shows only the immutable server proposal; no model-generated approval text.</summary>
public sealed class ChangeConfirmationWindow : Window
{
    public ChangeConfirmationWindow(JObject proposal)
    {
        Title = "Confirm TopSolid change";
        Width = 620; Height = 560; MinWidth = 450; MinHeight = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new DockPanel { Margin = new Thickness(16) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var reject = new Button { Content = "Cancel", IsCancel = true, IsDefault = true, MinWidth = 90, Margin = new Thickness(6) };
        var approve = new Button { Content = "Apply change", MinWidth = 110, Margin = new Thickness(6) };
        reject.Click += (_, _) => DialogResult = false;
        approve.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(reject); buttons.Children.Add(approve);
        DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
        var heading = new TextBlock
        {
            Text = $"Target: {proposal["target"]?["name"]}\nTool: {proposal["toolName"]}\nLength units: {proposal["inputLengthUnits"]}\n{proposal["effect"]}",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12)
        };
        if (proposal["target"]?["affectedDocuments"] is JArray affected && affected.Count > 1)
            heading.Text += $"\nAffected synchronized documents: {affected.Count} (listed below).";
        DockPanel.SetDock(heading, Dock.Top); panel.Children.Add(heading);
        panel.Children.Add(new TextBox { Text = proposal.ToString(Formatting.Indented), IsReadOnly = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"), Padding = new Thickness(8) });
        Content = panel;
    }
}
