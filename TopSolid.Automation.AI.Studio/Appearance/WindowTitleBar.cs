using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace TopSolid.Automation.AI.Studio.Appearance;

/// <summary>One caption for main, modal and diagnostic windows. WindowChrome retains native sizing,
/// snapping, caption dragging, double-click and the system menu without a transparent render layer.</summary>
public sealed class WindowTitleBar : Grid
{
    private Window? host;
    private readonly Button minimize;
    private readonly Button maximize;

    public WindowTitleBar()
    {
        Height = 34;
        SetResourceReference(StyleProperty, "WindowCaption");
        ColumnDefinitions.Add(new()); ColumnDefinitions.Add(new() { Width = GridLength.Auto });

        var title = new DockPanel { Margin = new Thickness(8, 0, 8, 0) };
        var icon = new Image { Width = 22, Height = 22, Margin = new Thickness(0, 0, 8, 0) };
        icon.SetBinding(Image.SourceProperty, WindowBinding("Icon"));
        title.Children.Add(icon);
        var label = new TextBlock { FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TitleTextBrush");
        label.SetBinding(TextBlock.TextProperty, WindowBinding("Title"));
        title.Children.Add(label);
        var titleSurface = new Border { Child = title };
        titleSurface.SetResourceReference(StyleProperty, "CaptionTitleSurface");
        Children.Add(titleSurface);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        minimize = Caption("\uE921", "Window.Minimize", w => SystemCommands.MinimizeWindow(w));
        maximize = Caption("\uE922", "Window.Maximize", w =>
        {
            if (w.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(w);
            else SystemCommands.MaximizeWindow(w);
        });
        buttons.Children.Add(minimize); buttons.Children.Add(maximize);
        buttons.Children.Add(Caption("\uE8BB", "Window.Close", w => SystemCommands.CloseWindow(w)));
        var buttonSurface = new Border { Child = buttons };
        buttonSurface.SetResourceReference(StyleProperty, "CaptionButtonSurface");
        SetColumn(buttonSurface, 1); Children.Add(buttonSurface);
        Loaded += (_, _) =>
        {
            host = Window.GetWindow(this);
            if (host == null) return;
            host.StateChanged += UpdateButtons;
            UpdateButtons(null, EventArgs.Empty);
        };
        Unloaded += (_, _) => { if (host != null) host.StateChanged -= UpdateButtons; host = null; };
    }

    private Button Caption(string glyph, string key, Action<Window> action)
    {
        var button = new Button { Content = glyph, Tag = key, FontFamily = new FontFamily("Segoe MDL2 Assets") };
        var mouseActivation = false;
        button.SetResourceReference(StyleProperty, "CaptionButton");
        button.SetResourceReference(ToolTipProperty, "Ui." + key);
        button.SetResourceReference(AutomationProperties.NameProperty, "Ui." + key);
        WindowChrome.SetIsHitTestVisibleInChrome(button, true);
        button.PreviewMouseLeftButtonDown += (_, _) => mouseActivation = true;
        button.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space) mouseActivation = false;
        };
        button.Click += (_, _) =>
        {
            var clearMouseFocus = mouseActivation;
            mouseActivation = false;
            if (host != null) action(host);
            if (clearMouseFocus)
                button.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(Keyboard.ClearFocus));
        };
        return button;
    }

    private void UpdateButtons(object? sender, EventArgs e)
    {
        if (host == null) return;
        minimize.Visibility = host.ShowInTaskbar && host.ResizeMode != ResizeMode.NoResize ? Visibility.Visible : Visibility.Collapsed;
        maximize.Visibility = host.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip ? Visibility.Visible : Visibility.Collapsed;
        maximize.Content = host.WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        var key = host.WindowState == WindowState.Maximized ? "Ui.Window.Restore" : "Ui.Window.Maximize";
        maximize.SetResourceReference(ToolTipProperty, key);
        maximize.SetResourceReference(AutomationProperties.NameProperty, key);
    }

    private static Binding WindowBinding(string path) => new(path) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Window), 1) };
}
