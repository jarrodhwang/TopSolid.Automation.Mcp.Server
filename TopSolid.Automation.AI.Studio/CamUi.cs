using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio;

internal static class CamUi
{
    internal static void Window(Window window, string key, double width, double height)
    {
        window.Title = StudioStrings.Get(key); window.Width = width; window.Height = height;
        window.MinWidth = Math.Min(width, 700); window.MinHeight = Math.Min(height, 480);
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner; window.ShowInTaskbar = false;
        window.Icon = TopSolidIcons.Get("operation"); TopSolidTheme.ApplyWindow(window);
        window.SetResourceReference(Control.BackgroundProperty, "WindowBrush"); window.SetResourceReference(Control.ForegroundProperty, "TextBrush");
    }
    internal static Button Button(string key, Action action) {
        var button = new Button { Content = StudioStrings.Get(key), Margin = new Thickness(4), Padding = new Thickness(10, 5, 10, 5) };
        button.Click += (_, _) => action(); return button;
    }
    internal static TextBox Field(Panel panel, string key, string text, int max = 256, bool readOnly = false)
    {
        panel.Children.Add(new TextBlock { Text = StudioStrings.Get(key), Margin = new Thickness(0, 9, 0, 3) });
        var box = new TextBox { Text = text, MaxLength = max, IsReadOnly = readOnly, MinWidth = 120 };
        System.Windows.Automation.AutomationProperties.SetName(box, StudioStrings.Get(key)); panel.Children.Add(box); return box;
    }
    internal static DataGridTextColumn Column(string key, string property, int width = 140, bool readOnly = false) =>
        new() { Header = StudioStrings.Get(key), Binding = new Binding(property), Width = width, IsReadOnly = readOnly };
    internal static WrapPanel Options(CamMethodOptions options)
    {
        var panel = new WrapPanel();
        foreach (var property in typeof(CamMethodOptions).GetProperties())
        {
            var box = new CheckBox { Content = StudioStrings.Get("Cam.Option." + property.Name), Margin = new Thickness(4, 6, 12, 6) };
            box.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding(property.Name) { Source = options, Mode = BindingMode.TwoWay });
            panel.Children.Add(box);
        }
        return panel;
    }
}
