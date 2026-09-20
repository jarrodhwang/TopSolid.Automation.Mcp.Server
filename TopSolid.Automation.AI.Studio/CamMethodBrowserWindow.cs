using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;

namespace TopSolid.Automation.AI.Studio;

internal sealed class CamMethodBrowserWindow : Window
{
    private readonly TreeView tree = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 8, 4, 8) };
    private readonly TextBlock path = new() { TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(4, 4, 4, 8) };
    private readonly Button choose;
    private readonly CancellationTokenSource lifetime = new();
    private readonly IMcpClient client;
    private readonly Func<JObject, CancellationToken, Task<bool>>? confirm;
    private bool selecting, closed;
    private int generation;
    internal JObject? Selected { get; private set; }

    internal CamMethodBrowserWindow(IMcpClient client, Func<JObject, CancellationToken, Task<bool>>? confirm = null)
    {
        this.client = client; this.confirm = confirm;
        CamUi.Window(this, "Cam.PdmExplorer", 820, 620);
        var root = new DockPanel { Margin = new Thickness(12) }; Content = root;
        var footer = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        DockPanel.SetDock(path, Dock.Top); root.Children.Add(path);
        var refresh = CamUi.Button("Cam.BrowserRefresh", Reset); footer.Children.Add(refresh);
        var cancel = CamUi.Button("Cam.Cancel", () => DialogResult = false); cancel.IsCancel = true; footer.Children.Add(cancel);
        choose = CamUi.Button("Cam.SelectMethod", () => { }); choose.IsDefault = true; choose.IsEnabled = false; footer.Children.Add(choose);
        AutomationProperties.SetName(tree, StudioStrings.Get("Cam.PdmExplorer")); root.Children.Add(tree);
        VirtualizingStackPanel.SetIsVirtualizing(tree, true);
        VirtualizingStackPanel.SetVirtualizationMode(tree, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(tree, true);
        tree.SelectedItemChanged += (_, _) => {
            choose.IsEnabled = !selecting && tree.SelectedItem is TreeViewItem { Tag: Node { Receipt: { } row } } && CamMethodBrowser.IsMethod(row);
            path.Text = tree.SelectedItem is TreeViewItem { Tag: Node node } ? node.Path : "";
        };
        async Task Select()
        {
            if (selecting || tree.SelectedItem is not TreeViewItem { Tag: Node { Receipt: { } row } } || !CamMethodBrowser.IsMethod(row)) return;
            selecting = true; tree.IsEnabled = refresh.IsEnabled = choose.IsEnabled = false;
            status.Text = StudioStrings.Get("Cam.BrowserLoading");
            try {
                var result = await CamMethodBrowser.Select(client, (JObject)row.DeepClone(), confirm, lifetime.Token);
                if (result != null && !closed) { Selected = result; DialogResult = true; }
                else if (!closed) status.Text = StudioStrings.Get("CamBrowse.Cancelled");
            } catch (OperationCanceledException) { }
            catch (Exception error) { if (!closed) status.Text = error.Message; }
            finally { selecting = false; if (!closed) tree.IsEnabled = refresh.IsEnabled = choose.IsEnabled = true; }
        }
        choose.Click += async (_, _) => await Select();
        tree.MouseDoubleClick += async (_, e) => {
            // Only document leaves can select; expansion and folder navigation never open documents.
            if (e.OriginalSource is FrameworkElement element && FindItem(element) is { IsSelected: true, Tag: Node { Receipt: { } row } } && CamMethodBrowser.IsMethod(row))
                await Select();
        };
        Closed += (_, _) => { closed = true; lifetime.Cancel(); };
        Reset();
    }
    private static TreeViewItem? FindItem(DependencyObject element)
    {
        while (element != null) { if (element is TreeViewItem item) return item; element = System.Windows.Media.VisualTreeHelper.GetParent(element); }
        return null;
    }
    private void Reset()
    {
        generation++;
        tree.Items.Clear(); choose.IsEnabled = false; path.Text = status.Text = "";
        tree.Items.Add(Container(StudioStrings.Get("Cam.BrowserProjects"), "project", "topsolid_list_projects", null, null, [], ""));
        tree.Items.Add(Container(StudioStrings.Get("Cam.BrowserLibraries"), "library", "topsolid_list_libraries", null, null, [], ""));
    }
    private sealed class Node
    {
        internal JObject? Receipt;
        internal required string Path;
        internal required CamMethodBrowser.Pages Pages;
        internal bool Busy;
        internal int Shown;
    }
    private static object Header(string name, string icon)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
        panel.Children.Add(new Image { Source = TopSolidIcons.Get(icon), Width = 18, Height = 18, Margin = new Thickness(0, 0, 6, 0) });
        panel.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center }); return panel;
    }
    private TreeViewItem Container(string name, string icon, string tool, string? id, JObject? receipt, HashSet<string> ancestors, string parentPath)
    {
        var node = new Node { Receipt = receipt, Path = parentPath.Length == 0 ? name : parentPath + " / " + name, Pages = new(client, tool, id) };
        var version = generation;
        var item = new TreeViewItem { Header = Header(name, icon), Tag = node, ToolTip = id }; AutomationProperties.SetName(item, name);
        item.Items.Add(new TreeViewItem { Header = StudioStrings.Get("Cam.BrowserLoading"), IsEnabled = false });
        async Task Load(bool more)
        {
            if (node.Busy || closed || version != generation || !more && node.Pages.Loaded) return;
            node.Busy = true; status.Text = StudioStrings.Get("Cam.BrowserLoading");
            try {
                await node.Pages.Fetch(lifetime.Token);
                if (closed || version != generation) return;
                var rows = node.Pages.Rows.Skip(node.Shown).ToArray();
                if (rows.Any(row => (receipt == null || (string?)row["kind"] == "folder") &&
                    (ancestors.Contains((string)row["pdmObjectId"]!) || ancestors.Count >= 64)))
                    throw new InvalidOperationException(StudioStrings.Get("List.Incomplete"));
                if (node.Shown == 0) item.Items.Clear();
                else if (item.Items.Count > 0 && item.Items[item.Items.Count - 1] is Button) item.Items.RemoveAt(item.Items.Count - 1);
                foreach (var row in rows) {
                    var key = (string)row["pdmObjectId"]!; var label = (string?)row["name"] ?? key;
                    if (receipt == null || (string?)row["kind"] == "folder") {
                        if (ancestors.Contains(key) || ancestors.Count >= 64) throw new InvalidOperationException(StudioStrings.Get("List.Incomplete"));
                        var next = new HashSet<string>(ancestors, StringComparer.Ordinal) { key };
                        item.Items.Add(Container(label, receipt == null ? icon : "folder", "topsolid_list_pdm_children", key, row, next, node.Path));
                    } else {
                        var iconKey = TopSolidIcons.DocumentKey(row);
                        if (CamMethodBrowser.IsMethod(row) && iconKey is null or "document") iconKey = "operation";
                        var leaf = new TreeViewItem { Header = Header(label, iconKey ?? "document"),
                            Tag = new Node { Receipt = row, Path = node.Path + " / " + label, Pages = node.Pages }, ToolTip = key,
                            IsEnabled = CamMethodBrowser.IsMethod(row) };
                        AutomationProperties.SetName(leaf, label); item.Items.Add(leaf);
                    }
                }
                node.Shown = node.Pages.Rows.Count;
                if (node.Pages.More) { var load = CamUi.Button("Cam.BrowserMore", () => { }); load.Click += async (_, _) => await Load(true); item.Items.Add(load); }
                if (node.Shown == 0) item.Items.Add(new TreeViewItem { Header = StudioStrings.Get("CamBrowse.Empty"), IsEnabled = false });
                status.Text = "";
            } catch (OperationCanceledException) { }
            catch (Exception error) { if (!closed && version == generation) {
                status.Text = error.Message;
                if (!node.Pages.Loaded) {
                    item.Items.Clear(); var retry = CamUi.Button("Cam.BrowserRefresh", () => { });
                    retry.Click += async (_, _) => await Load(true); item.Items.Add(retry);
                }
            } }
            finally { node.Busy = false; }
        }
        item.Expanded += async (_, e) => { if (ReferenceEquals(e.OriginalSource, item)) await Load(false); };
        return item;
    }
}
