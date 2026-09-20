using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Settings;

namespace TopSolid.Automation.AI.Studio;

internal sealed class CamOperationDetailWindow : Window
{
    private sealed record Field(CamParameterDraft Draft, Control Editor)
    {
        internal JObject Arguments() => Draft.Arguments((Editor as TextBox)?.Text ?? "", ((Editor as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag as JToken);
    }
    private readonly IMcpClient client;
    private readonly Func<JObject, CancellationToken, Task<bool>>? confirm;
    private readonly Action<ChatTrace> trace;
    private readonly Action<string, string> documentChanged;
    private readonly CancellationTokenSource lifetime = new();
    private readonly CamParameterFavorites favorites;
    private readonly ListBox pages = new() { Name = "DetailPages", MinWidth = 200, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox search = new() { Name = "DetailSearch", MinHeight = 32, Margin = new Thickness(0, 0, 0, 10) };
    private readonly StackPanel rows = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button apply = new() { Name = "ApplyDetails", Content = StudioStrings.Get("Cam.EditReview"), MinWidth = 130 };
    private readonly Button refresh = new() { Content = StudioStrings.Get("Cam.Reload"), MinWidth = 100, Margin = new Thickness(0, 0, 8, 0) };
    private readonly Dictionary<string, Field> fields = new(StringComparer.Ordinal);
    private IReadOnlyList<JObject> parameters = [];
    private JObject element;
    private bool busy, closed, loaded, applying;
    internal Task Loading { get; private set; } = Task.CompletedTask;

    internal CamOperationDetailWindow(string operationName, string? toolText, string iconKey, JObject element, IMcpClient client,
        Func<JObject, CancellationToken, Task<bool>>? confirm, Action<ChatTrace> trace, Action<string, string> documentChanged,
        CamParameterFavorites? favorites = null)
    {
        this.client = client; this.confirm = confirm; this.trace = trace; this.documentChanged = documentChanged;
        this.element = (JObject)element.DeepClone(); this.favorites = favorites ?? new();
        Title = StudioStrings.Get("Cam.OperationDetail"); Width = 1120; Height = 740; MinWidth = 790; MinHeight = 480;
        ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner; Icon = TopSolidIcons.Get(iconKey);
        StudioStrings.InitializeResources(this); TopSolidTheme.ApplyWindow(this);
        SetResourceReference(BackgroundProperty, "WindowBrush"); SetResourceReference(ForegroundProperty, "TextBrush");
        var root = new DockPanel(); root.SetResourceReference(Panel.BackgroundProperty, "WindowBrush"); Content = root;
        var title = new StackPanel();
        title.Children.Add(new TextBlock { Text = operationName, FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(toolText)) title.Children.Add(new TextBlock { Text = toolText.Replace('\n', ' '), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
        var header = DialogLayout.Toolbar(DialogLayout.Heading(Icon, title)); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var close = new Button { Content = StudioStrings.Get("Common.Close"), MinWidth = 90, Margin = new Thickness(8, 0, 0, 0) };
        close.Click += (_, _) => Close();
        DialogLayout.Command(refresh, "refresh"); DialogLayout.Command(apply, "approve"); DialogLayout.Command(close, "cancel");
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; actions.Children.Add(refresh); actions.Children.Add(apply); actions.Children.Add(close);
        var footer = new DockPanel(); DockPanel.SetDock(actions, Dock.Right); footer.Children.Add(actions); footer.Children.Add(status);
        status.Margin = new Thickness(0, 0, 12, 0); AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        var bar = DialogLayout.Footer(footer); DockPanel.SetDock(bar, Dock.Bottom); root.Children.Add(bar);
        var body = new Grid { Margin = new Thickness(12) }; root.Children.Add(body);
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(214) }); body.ColumnDefinitions.Add(new ColumnDefinition());
        pages.Margin = new Thickness(0, 0, 12, 0); body.Children.Add(pages);
        var content = new DockPanel(); Grid.SetColumn(content, 1); body.Children.Add(content);
        AutomationProperties.SetName(search, StudioStrings.Get("Cam.SearchParameters")); search.ToolTip = StudioStrings.Get("Cam.SearchParameters");
        var searchBar = new DockPanel();
        searchBar.Children.Add(new TextBlock { Text = StudioStrings.Get("Cam.SearchParameters"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 10) });
        searchBar.Children.Add(search); DockPanel.SetDock(searchBar, Dock.Top); content.Children.Add(searchBar);
        var headings = RowGrid(); headings.Margin = new Thickness(0, 0, 18, 8);
        AddCell(headings, new TextBlock { Text = StudioStrings.Get("Cam.Parameter"), FontWeight = FontWeights.SemiBold }, 1);
        AddCell(headings, new TextBlock { Text = StudioStrings.Get("Cam.CurrentValue"), FontWeight = FontWeights.SemiBold }, 2);
        AddCell(headings, new TextBlock { Text = StudioStrings.Get("Cam.NewValue"), FontWeight = FontWeights.SemiBold }, 3);
        DockPanel.SetDock(headings, Dock.Top); content.Children.Add(headings);
        content.Children.Add(new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        foreach (var page in CamOperationDetails.Pages) pages.Items.Add(new ListBoxItem { Tag = page, Padding = new Thickness(8), HorizontalContentAlignment = HorizontalAlignment.Stretch });
        UpdatePageLabels(); pages.SelectedIndex = 1;
        pages.SelectionChanged += (_, _) => Render(); search.TextChanged += (_, _) => Render();
        refresh.Click += async (_, _) => { if (Discard()) await Reload(); };
        apply.Click += async (_, _) => await Apply();
        Loaded += (_, _) => { Loading = Reload(); };
        Closing += (_, e) => { if (applying || !Discard()) e.Cancel = true; };
        Closed += (_, _) => { closed = true; lifetime.Cancel(); lifetime.Dispose(); };
    }

    private bool CanEdit => confirm != null && client.IsConnected && client is IConfirmableMcpClient && client.Tools.Any(t => t.Name == CamParameterEditRequest.WriteTool && t.RequiresConfirmation);
    private string SelectedPage => (string?)((ListBoxItem?)pages.SelectedItem)?.Tag ?? "cam-tool";
    private bool Dirty => fields.Values.Any(f => { try { return f.Draft.Changed(f.Arguments()); } catch (ArgumentException) { return true; } });
    private bool Discard() => !Dirty || MessageBox.Show(this, StudioStrings.Get("Cam.Discard"), Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    private void SetBusy(bool value)
    {
        busy = value; pages.IsEnabled = search.IsEnabled = rows.IsEnabled = !value;
        refresh.IsEnabled = !value; apply.IsEnabled = !value && loaded && CanEdit && Dirty;
    }

    private async Task Reload(Dictionary<string, JObject>? pending = null)
    {
        SetBusy(true); loaded = false; status.Text = StudioStrings.Get("Cam.Loading");
        try
        {
            var result = await CamOperationDetails.Load(client, element, lifetime.Token);
            if (closed) return;
            parameters = result; fields.Clear(); loaded = true;
            if (pending != null)
                foreach (var row in parameters)
                    if (pending.TryGetValue((string)row["name"]!, out var change) && GetField(row) is { } field)
                    {
                        if ((string?)change["valueType"] != field.Draft.Type || !JToken.DeepEquals(change["unitType"], field.Draft.Type == "Real" ? field.Draft.Row["unitType"] : null)) continue;
                        try
                        {
                            field.Draft.ValidateValue(change[field.Draft.Field]);
                            if (field.Editor is TextBox box) box.Text = field.Draft.Type == "Real" ? ((double)change[field.Draft.Field]! * field.Draft.Scale).ToString("G15", System.Globalization.CultureInfo.CurrentCulture) : (string?)change[field.Draft.Field] ?? change[field.Draft.Field]!.ToString();
                            if (field.Editor is ComboBox combo) combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => JToken.DeepEquals(i.Tag as JToken, change[field.Draft.Field]));
                        }
                        catch (ArgumentException) { }
                    }
            UpdatePageLabels(); Render();
            status.Text = StudioStrings.Get("Cam.ParameterCount", parameters.Count, parameters.Count(r => (bool?)r["editSupported"] == true));
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException or TimeoutException or Newtonsoft.Json.JsonException)
        {
            status.Text = StudioStrings.Get("Cam.LoadFailed"); trace(new ChatTrace("Tool error", error.Message));
        }
        finally { if (!closed) SetBusy(false); }
    }

    private void UpdatePageLabels()
    {
        foreach (ListBoxItem item in pages.Items)
        {
            var page = (string)item.Tag;
            var count = parameters.Count(r => page == "cam-favorites" ? favorites.Names.Contains((string)r["name"]!) : CamOperationDetails.Page(r) == page);
            item.Content = DialogLayout.Heading(TopSolidIcons.Get(page), new TextBlock { Text = CamOperationDetails.Label(page) + "  " + count, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            AutomationProperties.SetName(item, CamOperationDetails.Label(page));
        }
    }

    private Field? GetField(JObject row)
    {
        var name = (string)row["name"]!;
        if (fields.TryGetValue(name, out var existing)) return existing;
        CamParameterDraft draft;
        try { draft = new CamParameterDraft(CamOperationDetails.Receipt(element, row)); } catch (ArgumentException) { return null; }
        Control editor;
        if (draft.Options is { Count: > 0 } options)
        {
            var combo = new ComboBox { MinHeight = 32 };
            foreach (var option in options.OfType<JObject>())
            {
                var item = new ComboBoxItem { Content = (string?)option["label"] ?? (string?)option["name"] ?? option["value"]!.ToString(), Tag = option["value"]!.DeepClone() };
                combo.Items.Add(item); if (JToken.DeepEquals(draft.Initial, option["value"])) combo.SelectedItem = item;
            }
            combo.SelectionChanged += (_, _) => Changed(); editor = combo;
        }
        else
        {
            var box = new TextBox { Text = draft.InitialText, MinHeight = 32, MaxLength = 4000, VerticalContentAlignment = VerticalAlignment.Center,
                AcceptsReturn = draft.Type == "Text", TextWrapping = TextWrapping.Wrap, MaxHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            box.TextChanged += (_, _) => Changed(); editor = box;
        }
        editor.IsEnabled = CanEdit; AutomationProperties.SetName(editor, draft.Name + (draft.Unit.Length > 0 ? " · " + draft.Unit : ""));
        var field = new Field(draft, editor); fields.Add(name, field); return field;
        void Changed() { if (!busy) apply.IsEnabled = loaded && CanEdit && Dirty; }
    }

    private void Render()
    {
        // Detach reusable editors before rebuilding the selected page. Drafts survive search/page changes.
        foreach (var field in fields.Values) if (field.Editor.Parent is Panel parent) parent.Children.Remove(field.Editor);
        rows.Children.Clear(); var page = SelectedPage; var term = search.Text.Trim(); var count = 0;
        var visible = parameters.Where(r => page == "cam-favorites" ? favorites.Names.Contains((string)r["name"]!) : CamOperationDetails.Page(r) == page)
            .Where(r => term.Length == 0 || ((string)r["name"]! + " " + CamOperationDetails.Name(r) + " " + CamOperationDetails.Value(r)).Contains(term, StringComparison.CurrentCultureIgnoreCase));
        foreach (var group in visible.GroupBy(CamOperationDetails.Group))
        {
            if (group.Key.Length > 0)
            {
                var heading = new TextBlock { Text = group.Key, FontWeight = FontWeights.SemiBold, Margin = new Thickness(36, 10, 0, 8), TextWrapping = TextWrapping.Wrap };
                heading.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush"); rows.Children.Add(heading);
            }
            foreach (var row in group)
            {
                var name = (string)row["name"]!;
                count++;
                var line = RowGrid(); line.Margin = new Thickness(0, 0, 0, 8);
                var starIcon = new Image { Source = TopSolidIcons.Get("cam-favorites"), Width = 19, Height = 19, Opacity = favorites.Names.Contains(name) ? 1 : .3 };
                var star = new ToggleButton { Content = starIcon, IsChecked = favorites.Names.Contains(name), Width = 28, Height = 30, Padding = new Thickness(0), ToolTip = StudioStrings.Get("Cam.Favorite"), Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0) };
                AutomationProperties.SetName(star, StudioStrings.Get("Cam.Favorite") + " · " + CamOperationDetails.Name(row));
                star.Click += (_, _) =>
                {
                    try { favorites.Set(name, star.IsChecked == true); starIcon.Opacity = star.IsChecked == true ? 1 : .3; UpdatePageLabels(); if (page == "cam-favorites") Render(); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    { star.IsChecked = favorites.Names.Contains(name); status.Text = StudioStrings.Get("Cam.FavoriteFailed"); }
                };
                AddCell(line, star, 0);
                var label = new TextBlock { Text = CamOperationDetails.Name(row), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, ToolTip = name };
                AddCell(line, label, 1);
                AddCell(line, new TextBlock { Text = CamOperationDetails.Value(row), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }, 2);
                if (GetField(row) is { } field)
                {
                    var input = new DockPanel();
                    if (field.Draft.Unit.Length > 0) { var unit = new TextBlock { Text = field.Draft.Unit, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) }; DockPanel.SetDock(unit, Dock.Right); input.Children.Add(unit); }
                    input.Children.Add(field.Editor);
                    var stack = new StackPanel(); stack.Children.Add(input);
                    if ((string?)row["smartType"] is { } smart && smart != "Basic")
                        stack.Children.Add(new TextBlock { Text = StudioStrings.Get("Cam.ReplacesFormula"), TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
                    AddCell(line, stack, 3);
                }
                else
                {
                    var hint = (bool?)row["ambiguousName"] == true ? "Cam.AmbiguousParameter" : (bool?)row["isError"] == true || row["metadataErrors"] != null ? "Cam.ValueUnavailable" : (bool?)row["readOnly"] == true ? "Cam.ReadOnly" : "Cam.NativeEditor";
                    var readOnly = new TextBlock { Text = StudioStrings.Get(hint), ToolTip = (string?)row["error"] ?? (string?)row["editGuidance"], TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
                    readOnly.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush"); AddCell(line, readOnly, 3);
                }
                rows.Children.Add(line);
            }
        }
        if (count == 0) rows.Children.Add(new TextBlock { Text = StudioStrings.Get(page == "cam-favorites" ? "Cam.NoFavorites" : "Cam.NoParameters"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 22, 8, 0) });
    }

    internal async Task Apply()
    {
        if (busy || !loaded || !CanEdit) return;
        var pending = new Dictionary<string, JObject>(StringComparer.Ordinal);
        foreach (var (name, field) in fields)
        {
            try { var change = field.Arguments(); field.Editor.ClearValue(BorderBrushProperty); if (field.Draft.Changed(change)) pending.Add(name, change); }
            catch (ArgumentException)
            {
                pages.SelectedIndex = Array.IndexOf(CamOperationDetails.Pages, CamOperationDetails.Page(field.Draft.Row)); search.Clear(); Render();
                field.Editor.SetResourceReference(BorderBrushProperty, "DangerBrush"); field.Editor.BringIntoView(); field.Editor.Focus(); status.Text = StudioStrings.Get("Cam.InvalidValue"); return;
            }
        }
        if (pending.Count == 0) return;
        applying = true; SetBusy(true); string? conflict = null;
        try
        {
            var selected = pending.Keys.Select(name => fields[name].Draft.Receipt).ToArray();
            var result = await CamParameterEditRequest.RunSelected(Title, selected, client, (fresh, _) =>
            {
                foreach (var receipt in fresh)
                {
                    var draft = new CamParameterDraft(receipt); var original = fields[(string)draft.Row["name"]!].Draft;
                    if (!JToken.DeepEquals(draft.Initial, original.Initial) || draft.Type != original.Type ||
                        !JToken.DeepEquals(draft.Row["unitType"], original.Row["unitType"]) || (string?)draft.Row["formula"] != (string?)original.Row["formula"] ||
                        !JToken.DeepEquals(draft.Row["smartType"], original.Row["smartType"]) ||
                        !JToken.DeepEquals(draft.Row["source"]?.Type == JTokenType.Null ? null : draft.Row["source"], original.Row["source"]?.Type == JTokenType.Null ? null : original.Row["source"]))
                    { conflict = StudioStrings.Get("Cam.ChangedExternally"); throw new InvalidOperationException(conflict); }
                }
                return Task.FromResult<IReadOnlyList<JObject>?>(pending.Values.ToArray());
            }, confirm, trace, lifetime.Token, (before, after) =>
            {
                element["documentId"] = after; documentChanged(before, after);
            });
            // Read back the complete operation because changing one parameter can alter others.
            // Keep unapplied drafts after a declined/failed change; never automatically retry a write.
            await Reload(pending);
            if (loaded) status.Text = conflict ?? result.LastOrDefault(m => m.Role == "assistant" && !string.IsNullOrEmpty(m.Content))?.Content ?? "";
        }
        catch (OperationCanceledException) { loaded = false; status.Text = StudioStrings.Get("Cam.LoadFailed"); }
        catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException or TimeoutException or Newtonsoft.Json.JsonException)
        { loaded = false; status.Text = StudioStrings.Get("Cam.LoadFailed"); trace(new ChatTrace("Tool error", error.Message)); }
        finally { applying = false; if (!closed) SetBusy(false); }
    }

    private static Grid RowGrid()
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        foreach (var width in new[] { 1.25, 1.0, 1.3 }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Star) });
        return grid;
    }
    private static void AddCell(Grid grid, FrameworkElement element, int column)
    { Grid.SetColumn(element, column); element.Margin = new Thickness(0, 0, 10, 0); grid.Children.Add(element); }
}
