using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TopSolid.Automation.AI.Studio.Appearance;

/// <summary>Compact native toolbars and action rows shared by every Studio dialog.</summary>
internal static class DialogLayout
{
    public static Border Toolbar(UIElement content) => StyledBorder(content, "DialogToolbar");
    public static Border Footer(UIElement content) => StyledBorder(content, "DialogFooter");
    public static Border Body(UIElement content) => new() { Padding = new Thickness(16), Child = content };

    public static DockPanel Heading(ImageSource icon, UIElement text)
    {
        var panel = new DockPanel();
        panel.Children.Add(new Image { Source = icon, Width = 28, Height = 28, Margin = new Thickness(0, 0, 10, 0) });
        panel.Children.Add(text);
        return panel;
    }

    public static void Command(Button button, string icon)
    {
        button.SetResourceReference(FrameworkElement.StyleProperty, "NativeToolButton");
        button.Tag = TopSolidIcons.Get(icon);
    }

    private static Border StyledBorder(UIElement content, string key)
    {
        var border = new Border { Child = content };
        border.SetResourceReference(FrameworkElement.StyleProperty, key);
        return border;
    }
}
