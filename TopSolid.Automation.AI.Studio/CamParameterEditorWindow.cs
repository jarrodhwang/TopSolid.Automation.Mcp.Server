using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio;

internal sealed class CamParameterEditorWindow : Window
{
    internal IReadOnlyList<JObject>? Changes { get; private set; }
    private readonly List<(CamParameterDraft Draft, Control Editor)> editors = [];

    internal CamParameterEditorWindow(IReadOnlyList<JObject> receipts)
    {
        Title = StudioStrings.Get("Cam.EditSelected"); Width = 850; Height = 610; MinWidth = 580; MinHeight = 380;
        ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Icon = TopSolidIcons.Get("parameter"); StudioStrings.InitializeResources(this); TopSolidTheme.ApplyWindow(this);
        SetResourceReference(BackgroundProperty, "WindowBrush"); SetResourceReference(ForegroundProperty, "TextBrush");
        var root = new DockPanel(); Content = root;
        var heading = DialogLayout.Toolbar(DialogLayout.Heading(Icon, new TextBlock { Text = Title, FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }));
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var error = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        error.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush"); AutomationProperties.SetLiveSetting(error, AutomationLiveSetting.Polite);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = StudioStrings.Get("Common.Cancel"), IsCancel = true, MinWidth = 100, Margin = new Thickness(8,0,8,0) };
        var next = new Button { Content = StudioStrings.Get("Cam.EditReview"), IsDefault = true, MinWidth = 125 };
        DialogLayout.Command(cancel, "cancel"); DialogLayout.Command(next, "approve"); actions.Children.Add(cancel); actions.Children.Add(next);
        var footer = new DockPanel(); DockPanel.SetDock(actions, Dock.Right); footer.Children.Add(actions); footer.Children.Add(error);
        var footerBar = DialogLayout.Footer(footer); DockPanel.SetDock(footerBar, Dock.Bottom); root.Children.Add(footerBar);
        var rows = new Grid { Margin = new Thickness(14) };
        rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
        rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.8, GridUnitType.Star) });
        rows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var column = 0; column < 3; column++) Add(new TextBlock { Text = StudioStrings.Get(new[] { "Cam.Parameter", "Cam.CurrentValue", "Cam.NewValue" }[column]), FontWeight = FontWeights.SemiBold, Margin = new Thickness(4,0,8,12) }, 0, column);
        foreach (var receipt in receipts)
        {
            var draft = new CamParameterDraft(receipt); var index = rows.RowDefinitions.Count;
            rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Add(new TextBlock { Text = draft.Name, FontSize = 13, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4,8,14,8) }, index, 0);
            Add(new TextBlock { Text = draft.Current, FontSize = 13, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4,8,14,8) }, index, 1);
            Control editor;
            if (draft.Options is { Count: > 0 } options)
            {
                var combo = new ComboBox { MinHeight = 34 };
                foreach (var option in options.OfType<JObject>())
                {
                    var item = new ComboBoxItem { Content = (string?)option["label"] ?? (string?)option["name"] ?? option["value"]!.ToString(), Tag = option["value"]!.DeepClone() };
                    combo.Items.Add(item); if (JToken.DeepEquals(option["value"], draft.Initial)) combo.SelectedItem = item;
                }
                editor = combo;
            }
            else editor = new TextBox { Text = draft.InitialText, MinHeight = 34, MaxLength = 4000, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(8,4,8,4) };
            editor.FontSize = 13; editor.Margin = new Thickness(0,4,4,4);
            AutomationProperties.SetName(editor, draft.Name + (draft.Unit.Length > 0 ? " · " + draft.Unit : ""));
            var field = new DockPanel();
            if (draft.Unit.Length > 0) { var unit = new TextBlock { Text = draft.Unit, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6,0,0,0) }; DockPanel.SetDock(unit, Dock.Right); field.Children.Add(unit); }
            field.Children.Add(editor); Add(field, index, 2); editors.Add((draft, editor));
        }
        root.Children.Add(new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        next.Click += (_, _) =>
        {
            var changes = new List<JObject>(); Control? invalid = null;
            foreach (var (draft, editor) in editors)
            {
                try
                {
                    var arguments = draft.Arguments((editor as TextBox)?.Text ?? "", ((editor as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag as JToken);
                    editor.ClearValue(BorderBrushProperty); if (draft.Changed(arguments)) changes.Add(arguments);
                }
                catch (ArgumentException) { editor.SetResourceReference(BorderBrushProperty, "DangerBrush"); invalid ??= editor; }
            }
            if (invalid != null) { error.Text = StudioStrings.Get("Cam.InvalidValue"); invalid.Focus(); return; }
            Changes = changes; DialogResult = true;
        };
        void Add(UIElement element, int row, int column) { Grid.SetRow(element,row); Grid.SetColumn(element,column); rows.Children.Add(element); }
    }
}
