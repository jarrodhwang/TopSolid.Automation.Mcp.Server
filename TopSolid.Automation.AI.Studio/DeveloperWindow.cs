using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Diagnostics;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio;

/// <summary>A bounded, passive view of the same redacted session data used by log export.</summary>
public sealed class DeveloperWindow : Window
{
    private const int MaximumDisplayedEntries = 400;
    private readonly ObservableCollection<DetailRow> events = [];
    private readonly ObservableCollection<DetailRow> responses = [];
    private readonly ObservableCollection<DetailRow> toolRows = [];
    private readonly ListBox eventList;
    private readonly ListBox responseList;
    private readonly ListBox toolList;
    private readonly TextBox eventDetail;
    private readonly TextBox responseDetail;
    private readonly TextBox toolDetail;
    private readonly TextBlock sessionSummary = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock turnLabel = new();
    private readonly TextBlock eventCount = new();
    private readonly TextBlock responseCount = new();
    private readonly TextBlock catalogueCount = new();
    private readonly Grid gauges = new() { Margin = new Thickness(0, 0, 0, 14) };
    private readonly TextBlock footer = new();
    private readonly Border footerBar;
    private readonly MetricCard total = new("Turn elapsed", "Includes waits and overhead");
    private readonly MetricCard model = new("Model", "Measured completed requests");
    private readonly MetricCard tool = new("MCP tools", "Excludes user approval wait");
    private readonly MetricCard confirmation = new("Approval wait", "Measured confirmation dialogs");
    private readonly TextBox search;
    private readonly List<McpToolDefinition> catalogue = [];
    private string? catalogueSignature;
    private long lastChatSequence = -1;
    private long lastTraceSequence = -1;
    private int lastChatCount = -1;
    private int lastTraceCount = -1;
    private DeveloperTurnMetrics metrics = new(0, null, null, null, 0, 0);
    private readonly GpuUsagePanel gpuPanel = new();
    private readonly TabControl tabs;
    private readonly TabItem gpuTab;
    private readonly Action? connectToggle;
    private readonly Action? checkStatus;
    private readonly List<Action> translations = [];
    private bool connected;
    private bool busy;
    private bool mutationInFlight;
    private SessionLogSnapshot? lastSnapshot;
    private IReadOnlyList<McpToolDefinition> lastTools = [];
    private TimeSpan lastElapsed;
    private bool lastRunning;

    public Button ConnectButton { get; } = new() { Name = "ConnectButton", Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 6, 0) };
    public Button StatusButton { get; } = new() { Name = "StatusButton", Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 6, 0) };

    public DeveloperWindow(Action exportLog, Action? connectToggle = null, Action? checkStatus = null)
    {
        ArgumentNullException.ThrowIfNull(exportLog);
        TopSolidTheme.InitializeResources(this);
        StudioStrings.InitializeResources(this);
        this.connectToggle = connectToggle; this.checkStatus = checkStatus;
        NameScope.SetNameScope(this, new NameScope());
        RegisterName("ConnectButton", ConnectButton); RegisterName("StatusButton", StatusButton);
        Translate(this, TitleProperty, "TopSolid Automation AI · Developer");
        Width = 1120; Height = 760; MinWidth = 800; MinHeight = 560;
        Icon = TopSolidIcons.Get("developer");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        SetResourceReference(BackgroundProperty, "WindowBrush");
        SetResourceReference(ForegroundProperty, "TextBrush");
        TopSolidTheme.ApplyWindow(this);

        var root = new Grid();
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new());
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Content = root;

        var heading = new DockPanel();
        var controls = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 490 };
        ConnectButton.Click += (_, _) => { if (ConnectButton.IsEnabled) connectToggle?.Invoke(); };
        StatusButton.Click += (_, _) => { if (StatusButton.IsEnabled) checkStatus?.Invoke(); };
        Translate(StatusButton, ContentControl.ContentProperty, "Check TopSolid");
        DialogLayout.Command(ConnectButton, "connect"); DialogLayout.Command(StatusButton, "refresh");
        controls.Children.Add(ConnectButton); controls.Children.Add(StatusButton);
        var export = new Button { Padding = new Thickness(14, 6, 14, 6), VerticalAlignment = VerticalAlignment.Center };
        Translate(export, ContentControl.ContentProperty, "Save log");
        DialogLayout.Command(export, "save");
        export.Click += (_, _) => exportLog();
        controls.Children.Add(export);
        DockPanel.SetDock(controls, Dock.Right); heading.Children.Add(controls);
        var title = new StackPanel();
        var titleText = new TextBlock { FontWeight = FontWeights.SemiBold, FontSize = 16 };
        Translate(titleText, TextBlock.TextProperty, "Developer"); title.Children.Add(titleText);
        title.Children.Add(sessionSummary);
        sessionSummary.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
        heading.Children.Add(DialogLayout.Heading(TopSolidIcons.Get("developer"), title)); root.Children.Add(DialogLayout.Toolbar(heading));

        Grid.SetRow(turnLabel, 1); turnLabel.Margin = new Thickness(16, 12, 16, 6); root.Children.Add(turnLabel);
        gauges.Margin = new Thickness(16, 0, 16, 14);
        foreach (var card in new[] { total, model, tool, confirmation })
        {
            var index = gauges.ColumnDefinitions.Count;
            gauges.ColumnDefinitions.Add(new());
            Grid.SetColumn(card, index);
            card.Margin = new Thickness(index == 0 ? 0 : 8, 0, 0, 0);
            gauges.Children.Add(card);
        }
        Grid.SetRow(gauges, 2); root.Children.Add(gauges);

        tabs = new TabControl { Name = "DeveloperTabs", Padding = new Thickness(10), Margin = new Thickness(16, 12, 16, 12) }; RegisterName(tabs.Name, tabs);
        eventList = CreateList(events); responseList = CreateList(responses); toolList = CreateList(toolRows);
        eventDetail = CreateDetail(); responseDetail = CreateDetail(); toolDetail = CreateDetail();
        HookDetail(eventList, eventDetail); HookDetail(responseList, responseDetail); HookDetail(toolList, toolDetail);
        tabs.Items.Add(CreateTab("Events & results", CreateDetailPage(eventList, eventDetail, eventCount)));
        tabs.Items.Add(CreateTab("Chat responses", CreateDetailPage(responseList, responseDetail, responseCount)));
        var cataloguePage = new DockPanel();
        var catalogueHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var searchLabel = new TextBlock { Text = "Find tool", Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        Translate(searchLabel, TextBlock.TextProperty, "Find tool");
        search = new TextBox { MinWidth = 180, MaxLength = 200, ToolTip = "Filter tool names and descriptions", Padding = new Thickness(6) };
        Translate(search, ToolTipProperty, "Filter tool names and descriptions");
        search.TextChanged += (_, _) => RefreshCatalogue();
        catalogueHeader.Children.Add(searchLabel); catalogueHeader.Children.Add(search);
        DockPanel.SetDock(catalogueHeader, Dock.Top); cataloguePage.Children.Add(catalogueHeader);
        cataloguePage.Children.Add(CreateDetailPage(toolList, toolDetail, catalogueCount));
        tabs.Items.Add(CreateTab("MCP catalogue", cataloguePage));
        gpuTab = CreateTab("GPU usage", gpuPanel); tabs.Items.Add(gpuTab);
        tabs.SelectionChanged += (_, args) => { if (ReferenceEquals(args.Source, tabs)) UpdateGpuSampling(); };
        IsVisibleChanged += (_, _) => UpdateGpuSampling();
        StateChanged += (_, _) => UpdateGpuSampling();
        Grid.SetRow(tabs, 3); root.Children.Add(tabs);

        footer.Text = "Gauges show measured latest-turn durations against elapsed time. Events and responses show the latest 400 retained entries; Save log exports the retained session.";
        footer.TextWrapping = TextWrapping.Wrap; footer.FontSize = 11;
        footer.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
        Translate(footer, TextBlock.TextProperty, footer.Text);
        footerBar = DialogLayout.Footer(footer); Grid.SetRow(footerBar, 4); root.Children.Add(footerBar);
        UpdateSnapshot(new SessionLogSnapshot(), [], TimeSpan.Zero, false);
        UpdateConnectionState(false, false, false);
        StudioStrings.Changed += LanguageChanged;
        Closed += (_, _) => { gpuPanel.Dispose(); StudioStrings.Changed -= LanguageChanged; };
    }

    private void UpdateGpuSampling()
    {
        var showGpu = ReferenceEquals(tabs.SelectedItem, gpuTab);
        turnLabel.Visibility = gauges.Visibility = footerBar.Visibility = showGpu ? Visibility.Collapsed : Visibility.Visible;
        gpuPanel.SetActive(IsVisible && WindowState != WindowState.Minimized && showGpu);
    }

    public void UpdateConnectionState(bool connected, bool busy, bool mutationInFlight)
    {
        Dispatcher.VerifyAccess();
        this.connected = connected; this.busy = busy; this.mutationInFlight = mutationInFlight;
        ConnectButton.Content = StudioStrings.Text(connected ? "Disconnect MCP" : "Connect MCP");
        ConnectButton.IsEnabled = connectToggle != null && !busy && !mutationInFlight;
        StatusButton.IsEnabled = checkStatus != null && connected && !busy && !mutationInFlight;
        ControlChrome.SetIsSelected(ConnectButton, connected);
    }

    private TabItem CreateTab(string title, UIElement content)
    {
        var tab = new TabItem { Content = content }; Translate(tab, HeaderedContentControl.HeaderProperty, title); return tab;
    }

    private void Translate(DependencyObject target, DependencyProperty property, string native)
    {
        void Apply() => target.SetValue(property, StudioStrings.Text(native));
        translations.Add(Apply); Apply();
    }

    private void LanguageChanged()
    {
        foreach (var translate in translations) translate();
        foreach (var card in new[] { total, model, tool, confirmation }) card.UpdateLabels();
        UpdateConnectionState(connected, busy, mutationInFlight);
        lastChatCount = -1; lastTraceCount = -1;
        if (lastSnapshot != null) UpdateSnapshot(lastSnapshot, lastTools, lastElapsed, lastRunning);
        RefreshCatalogue();
    }

    /// <summary>Call on the owning dispatcher with the redacted session snapshot. Timer ticks only update gauges.</summary>
    public void UpdateSnapshot(SessionLogSnapshot snapshot, IReadOnlyList<McpToolDefinition> tools, TimeSpan elapsed, bool running)
    {
        Dispatcher.VerifyAccess();
        lastSnapshot = snapshot; lastTools = tools; lastElapsed = elapsed; lastRunning = running;
        var chatSequence = snapshot.Chat.LastOrDefault()?.Sequence ?? 0;
        var traceSequence = snapshot.Trace.LastOrDefault()?.Sequence ?? 0;
        var changed = chatSequence != lastChatSequence || traceSequence != lastTraceSequence ||
            snapshot.Chat.Count != lastChatCount || snapshot.Trace.Count != lastTraceCount;
        if (changed)
        {
            metrics = DeveloperTurnMetrics.From(snapshot);
            SyncRows(events, snapshot.Trace.TakeLast(MaximumDisplayedEntries).Select(entry => new DetailRow(entry.Sequence,
                $"{entry.TimestampUtc.ToLocalTime():HH:mm:ss}  {entry.Kind}", Preview(entry.Text), entry.Text)), eventList);
            SyncRows(responses, snapshot.Chat.TakeLast(MaximumDisplayedEntries).Select(entry => new DetailRow(entry.Sequence,
                $"{entry.TimestampUtc.ToLocalTime():HH:mm:ss}  {StudioStrings.Text(entry.Role)}" +
                (entry.ElapsedMilliseconds is { } duration ? $"  ·  {duration / 1000:F1} s" : ""), Preview(entry.Text), entry.Text)), responseList);
            eventCount.Text = StudioStrings.Get("Dev.EventsCount", events.Count, snapshot.Trace.Count);
            responseCount.Text = StudioStrings.Get("Dev.MessagesCount", responses.Count, snapshot.Chat.Count);
            sessionSummary.Text = StudioStrings.Get("Dev.SessionSummary", snapshot.Chat.Count, snapshot.Trace.Count,
                snapshot.Trace.Count(entry => entry.Kind == "Tool call"),
                snapshot.Trace.Count(entry => entry.Kind.Contains("error", StringComparison.OrdinalIgnoreCase)));
            lastChatSequence = chatSequence; lastTraceSequence = traceSequence;
            lastChatCount = snapshot.Chat.Count; lastTraceCount = snapshot.Trace.Count;
        }
        turnLabel.Text = StudioStrings.Text(metrics.StartSequence == 0 ? "No chat turn recorded" : running ? "Current turn · running" : "Latest turn · stopped");
        var elapsedSeconds = Math.Max(0, elapsed.TotalSeconds);
        total.Update(metrics.StartSequence == 0 ? null : elapsedSeconds, elapsedSeconds);
        model.Update(metrics.ModelSeconds, elapsedSeconds);
        tool.Update(metrics.ToolSeconds, elapsedSeconds);
        confirmation.Update(metrics.ConfirmationSeconds, elapsedSeconds);

        // Tool objects stay stable after discovery. A reconnect may replace them; include schema identity.
        var signature = string.Join("\n", tools.Select(item => item.Name + "|" + item.Description + "|" +
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item.InputSchema) + "|" + item.RequiresConfirmation));
        if (signature != catalogueSignature)
        {
            catalogueSignature = signature; catalogue.Clear(); catalogue.AddRange(tools);
            RefreshCatalogue();
        }
    }

    private void RefreshCatalogue()
    {
        var filter = search.Text.Trim();
        var filtered = catalogue.Where(item => filter.Length == 0 || item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            item.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)).OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        var selected = (toolList.SelectedItem as DetailRow)?.Title;
        toolRows.Clear();
        foreach (var item in filtered)
        {
            // Defer potentially large JSON formatting until selection.
            toolRows.Add(new DetailRow(toolRows.Count + 1, item.Name,
                StudioStrings.Text(item.RequiresConfirmation ? "Change-capable" : "Read-only") + " · " + Preview(item.Description), "", () =>
                    item.Name + Environment.NewLine + item.Description + Environment.NewLine + Environment.NewLine +
                    new JObject { ["inputSchema"] = item.InputSchema.DeepClone(), ["annotations"] = item.Annotations.DeepClone() }.ToString(Formatting.Indented)));
        }
        toolList.SelectedItem = toolRows.FirstOrDefault(row => row.Title == selected);
        if (toolList.SelectedItem == null && toolRows.Count > 0) toolList.SelectedIndex = 0;
        if (toolRows.Count == 0) toolDetail.Text = StudioStrings.Text(catalogue.Count == 0 ? "Connect MCP to discover tools." : "No matching tools.");
        catalogueCount.Text = StudioStrings.Get("Dev.CatalogueCount", filtered.Length, catalogue.Count);
    }

    private static void SyncRows(ObservableCollection<DetailRow> rows, IEnumerable<DetailRow> incoming, ListBox list)
    {
        var values = incoming.ToArray();
        var selectedSequence = (list.SelectedItem as DetailRow)?.Sequence;
        var followingLatest = rows.Count == 0 || ReferenceEquals(list.SelectedItem, rows.LastOrDefault());
        var sequenceSet = values.Select(row => row.Sequence).ToHashSet();
        for (var index = rows.Count - 1; index >= 0; index--) if (!sequenceSet.Contains(rows[index].Sequence)) rows.RemoveAt(index);
        var desiredRows = values.ToDictionary(row => row.Sequence);
        for (var index = 0; index < rows.Count; index++)
            if (rows[index].Title != desiredRows[rows[index].Sequence].Title) rows[index] = desiredRows[rows[index].Sequence];
        var existing = rows.Select(row => row.Sequence).ToHashSet();
        foreach (var row in values) if (!existing.Contains(row.Sequence)) rows.Add(row);
        if (followingLatest && rows.Count > 0) { list.SelectedItem = rows[^1]; list.ScrollIntoView(rows[^1]); }
        else if (selectedSequence.HasValue) list.SelectedItem = rows.FirstOrDefault(row => row.Sequence == selectedSequence.Value);
        if (rows.Count > 0 && list.SelectedItem == null) list.SelectedIndex = 0;
    }

    private static string Preview(string text)
    {
        var preview = text.Length > 220 ? text[..220] + "…" : text;
        return preview.Replace('\r', ' ').Replace('\n', ' ');
    }

    private static ListBox CreateList(ObservableCollection<DetailRow> items)
    {
        var list = new ListBox { ItemsSource = items, BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        list.SetResourceReference(BackgroundProperty, "SurfaceBrush");
        VirtualizingPanel.SetIsVirtualizing(list, true);
        VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(list, true);
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(MarginProperty, new Thickness(4, 6, 4, 6));
        var title = new FrameworkElementFactory(typeof(TextBlock));
        title.SetBinding(TextBlock.TextProperty, new Binding(nameof(DetailRow.Title)));
        title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        title.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        panel.AppendChild(title);
        var preview = new FrameworkElementFactory(typeof(TextBlock));
        preview.SetBinding(TextBlock.TextProperty, new Binding(nameof(DetailRow.Preview)));
        preview.SetValue(TextBlock.FontSizeProperty, 11.0);
        preview.SetValue(TextBlock.MarginProperty, new Thickness(0, 3, 0, 0));
        preview.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        panel.AppendChild(preview);
        list.ItemTemplate = new DataTemplate { VisualTree = panel };
        return list;
    }

    private static TextBox CreateDetail()
    {
        var detail = new TextBox
        {
            Text = StudioStrings.Text("Select an entry to inspect its content."), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalContentAlignment = VerticalAlignment.Top,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FontFamily = new FontFamily("Consolas"), FontSize = 12, Padding = new Thickness(12), BorderThickness = new Thickness(0)
        };
        detail.SetResourceReference(BackgroundProperty, "CodeBrush");
        detail.SetResourceReference(ForegroundProperty, "TextBrush");
        return detail;
    }

    private static void HookDetail(ListBox list, TextBox detail) => list.SelectionChanged += (_, _) =>
    {
        if (list.SelectedItem is DetailRow row) detail.Text = DeveloperDetailFormatter.Format(row.LazyContent?.Invoke() ?? row.Content);
        else detail.Text = StudioStrings.Text("Select an entry to inspect its content.");
        detail.ScrollToHome();
    };

    private static UIElement CreateDetailPage(ListBox list, TextBox detail, TextBlock count)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new());
        count.FontSize = 11; count.Margin = new Thickness(0, 0, 0, 8);
        count.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
        root.Children.Add(count);
        var panes = new Grid();
        panes.ColumnDefinitions.Add(new() { Width = new GridLength(0.36, GridUnitType.Star), MinWidth = 220 });
        panes.ColumnDefinitions.Add(new() { Width = new GridLength(8) });
        panes.ColumnDefinitions.Add(new() { Width = new GridLength(0.64, GridUnitType.Star), MinWidth = 260 });
        panes.Children.Add(list);
        var splitter = new GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Stretch };
        splitter.SetResourceReference(BackgroundProperty, "BorderBrush");
        Grid.SetColumn(splitter, 1); panes.Children.Add(splitter);
        Grid.SetColumn(detail, 2); panes.Children.Add(detail);
        Grid.SetRow(panes, 1); root.Children.Add(panes);
        return root;
    }

    private sealed record DetailRow(long Sequence, string Title, string Preview, string Content, Func<string>? LazyContent = null);

    private sealed class MetricCard : Border
    {
        private readonly string nativeTitle;
        private readonly string nativeHint;
        private readonly TextBlock titleLabel = new() { FontWeight = FontWeights.SemiBold };
        private readonly TextBlock caption = new() { FontSize = 10, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock value = new() { FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 6) };
        private readonly ProgressBar bar = new() { Minimum = 0, Maximum = 1, Height = 4, Margin = new Thickness(0, 0, 0, 7) };
        public MetricCard(string title, string hint)
        {
            nativeTitle = title; nativeHint = hint;
            Padding = new Thickness(12); BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(3);
            SetResourceReference(BackgroundProperty, "PanelBrush"); SetResourceReference(BorderBrushProperty, "BorderBrush");
            var contents = new StackPanel();
            contents.Children.Add(titleLabel);
            contents.Children.Add(value); contents.Children.Add(bar);
            bar.SetResourceReference(ForegroundProperty, "AccentBrush");
            bar.SetResourceReference(BackgroundProperty, "BorderBrush");
            caption.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
            contents.Children.Add(caption); Child = contents;
            UpdateLabels();
        }

        public void UpdateLabels() { titleLabel.Text = StudioStrings.Text(nativeTitle); caption.Text = StudioStrings.Text(nativeHint); }

        public void Update(double? seconds, double elapsed)
        {
            value.Text = seconds.HasValue ? seconds.Value.ToString("F1", CultureInfo.CurrentCulture) + " s" : "—";
            bar.Value = seconds.HasValue && elapsed > 0 ? Math.Clamp(seconds.Value / elapsed, 0, 1) : 0;
            ToolTip = StudioStrings.Text(seconds.HasValue ? "Duration from the latest chat turn. Gauge scale: total turn elapsed time." : "No measured duration recorded for this turn.");
        }
    }
}
