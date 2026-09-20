using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio.Preview;

internal sealed class MachineElementsWindow : Window
{
    private readonly HashSet<int> hidden;
    private readonly List<(CheckBox Box, MachineElement Element)> boxes = [];
    private bool synchronizing;
    internal IReadOnlyCollection<int> Hidden => hidden;
    internal MachineElementsWindow(MachineVisibility visibility)
    {
        hidden = new(visibility.Hidden);
        Title = StudioStrings.Get("Preview.MachineElements"); Width = 460; Height = 580; MinWidth = 340; MinHeight = 320;
        ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner; Icon = TopSolidIcons.Get("view-machine");
        StudioStrings.InitializeResources(this); TopSolidTheme.ApplyWindow(this);
        SetResourceReference(BackgroundProperty, "WindowBrush"); SetResourceReference(ForegroundProperty, "TextBrush");
        var root = new DockPanel(); root.SetResourceReference(Panel.BackgroundProperty, "WindowBrush"); Content = root;
        var title = DialogLayout.Toolbar(DialogLayout.Heading(Icon, new TextBlock { Text = Title, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }));
        DockPanel.SetDock(title, Dock.Top); root.Children.Add(title);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = StudioStrings.Get("Common.Cancel"), IsCancel = true, MinWidth = 95, Margin = new Thickness(0, 0, 8, 0) };
        var apply = new Button { Content = StudioStrings.Get("Preview.ApplyVisibility"), IsDefault = true, MinWidth = 95 };
        DialogLayout.Command(cancel, "cancel"); DialogLayout.Command(apply, "approve"); actions.Children.Add(cancel); actions.Children.Add(apply);
        apply.Click += (_, _) => DialogResult = true;
        var footer = DialogLayout.Footer(actions); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var tree = new TreeView { Margin = new Thickness(12) }; root.Children.Add(tree);
        var all = new MachineElement(StudioStrings.Get("Preview.MachineRoot"), visibility.Roots.SelectMany(n => n.Geometry).Distinct().ToArray(), visibility.Roots);
        tree.Items.Add(Item(all, true)); Sync();
        TreeViewItem Item(MachineElement element, bool expanded)
        {
            var box = new CheckBox { Content = DialogLayout.Heading(TopSolidIcons.Get("machine"), new TextBlock { Text = element.Name, VerticalAlignment = VerticalAlignment.Center }), Margin = new Thickness(2), VerticalContentAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(box, element.Name); boxes.Add((box, element));
            box.Click += (_, _) =>
            {
                if (synchronizing) return;
                if (box.IsChecked == true) hidden.ExceptWith(element.Geometry); else hidden.UnionWith(element.Geometry);
                Sync();
            };
            var item = new TreeViewItem { Header = box, IsExpanded = expanded };
            foreach (var child in element.Children) item.Items.Add(Item(child, false));
            return item;
        }
    }
    private void Sync()
    {
        synchronizing = true;
        foreach (var (box, element) in boxes)
        {
            var count = element.Geometry.Count(hidden.Contains);
            box.IsChecked = count == 0 ? true : count == element.Geometry.Count ? false : null;
        }
        synchronizing = false;
    }
}
