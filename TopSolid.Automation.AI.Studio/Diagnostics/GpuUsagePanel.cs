using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio.Diagnostics;

/// <summary>A passive local-machine view. Remote/cloud model hardware is never inferred from local counters.</summary>
internal sealed class GpuUsagePanel : Grid, IDisposable
{
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock updated = new() { FontSize = 11, Margin = new Thickness(0, 4, 0, 12) };
    private readonly StackPanel adapters = new();
    private readonly Dictionary<string, AdapterCard> cards = [];
    private readonly TextBlock hint = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };
    private CancellationTokenSource? sampling;
    private bool disposed;
    private GpuUsageSample? lastSample;

    public GpuUsagePanel()
    {
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        RowDefinitions.Add(new());
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        status.FontWeight = FontWeights.SemiBold;
        Children.Add(status); SetRow(updated, 1); Children.Add(updated);
        updated.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
        var scroll = new ScrollViewer { Content = adapters, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        SetRow(scroll, 2); Children.Add(scroll);
        hint.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
        SetRow(hint, 3); Children.Add(hint);
        StudioStrings.Changed += LanguageChanged;
        UpdateLabels();
    }

    public void SetActive(bool active)
    {
        if (disposed) return;
        if (!active)
        {
            sampling?.Cancel(); sampling = null;
            return;
        }
        if (sampling != null) return;
        // Samples belong to a continuous interval; don't connect stale values across a hidden tab.
        cards.Clear(); adapters.Children.Clear(); lastSample = null;
        var source = sampling = new CancellationTokenSource();
        UpdateLabels();
        GpuUsageSample? pendingSample = null;
        var updateQueued = 0;
        var worker = GpuUsageMonitor.RunAsync(sample =>
        {
            if (source.IsCancellationRequested || Dispatcher.HasShutdownStarted) return;
            Interlocked.Exchange(ref pendingSample, sample);
            // A busy UI keeps only the latest sample and at most one queued callback.
            if (Interlocked.Exchange(ref updateQueued, 1) != 0) return;
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                Interlocked.Exchange(ref updateQueued, 0);
                var latest = Interlocked.Exchange(ref pendingSample, null);
                if (latest != null && ReferenceEquals(sampling, source) && !source.IsCancellationRequested && !disposed) UpdateSample(latest);
            }));
        }, source.Token);
        // Native PDH calls are synchronous but run on the worker. Never close their handles concurrently.
        _ = worker.ContinueWith(completed =>
        {
            // Observe an unexpected worker failure without letting a diagnostic panel crash the shell.
            _ = completed.Exception;
            if (Dispatcher.HasShutdownStarted) { source.Dispose(); return; }
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ReferenceEquals(sampling, source))
                {
                    if (completed.IsFaulted && !disposed) UpdateSample(GpuUsageSample.Unavailable(completed.Exception?.GetBaseException().Message));
                    sampling = null;
                }
                source.Dispose();
            }));
        }, TaskScheduler.Default);
    }

    private void UpdateSample(GpuUsageSample sample)
    {
        lastSample = sample;
        var ids = sample.Adapters.Select(adapter => adapter.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var stale in cards.Keys.Where(id => !ids.Contains(id)).ToArray())
        {
            adapters.Children.Remove(cards[stale]); cards.Remove(stale);
        }
        for (var index = 0; index < sample.Adapters.Count; index++)
        {
            var adapter = sample.Adapters[index];
            if (!cards.TryGetValue(adapter.Id, out var card))
            {
                card = new AdapterCard(adapter.Id); cards.Add(adapter.Id, card); adapters.Children.Add(card);
            }
            card.Update(adapter, index);
        }
        UpdateLabels();
    }

    private void LanguageChanged() => UpdateLabels();
    private void UpdateLabels()
    {
        status.Text = StudioStrings.Text(lastSample == null ? "Sampling GPU counters…" : lastSample.Adapters.Count == 0 ? "GPU counters unavailable" : "Local computer GPUs");
        updated.Text = lastSample == null ? StudioStrings.Text("No GPU samples yet.") : lastSample.Adapters.Count == 0
            ? StudioStrings.Text("Windows GPU counters are not available on this computer.")
            : StudioStrings.Text("Last sample") + "  " + lastSample.Timestamp.ToLocalTime().ToString("T", CultureInfo.CurrentCulture);
        updated.ToolTip = lastSample?.Error;
        hint.Text = StudioStrings.Text("Samples refresh every 2 seconds while this tab is open. Usage is the busiest engine per adapter; memory is current usage.");
        foreach (var card in cards.Values) card.UpdateLabels();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; sampling?.Cancel(); sampling = null;
        StudioStrings.Changed -= LanguageChanged;
    }

    private sealed class AdapterCard : Border
    {
        private readonly TextBlock title = new() { FontWeight = FontWeights.SemiBold, FontSize = 15 };
        private readonly TextBlock utilization = new() { FontSize = 24, FontWeight = FontWeights.SemiBold };
        private readonly TextBlock dedicated = new();
        private readonly TextBlock shared = new();
        private readonly TextBlock engineLabel = new() { FontSize = 11 };
        private readonly TextBlock dedicatedLabel = new() { FontSize = 11 };
        private readonly TextBlock sharedLabel = new() { FontSize = 11 };
        private readonly GpuHistoryChart chart = new() { Height = 94, Margin = new Thickness(0, 10, 0, 0) };
        private GpuAdapterSample? sample;
        private int index;

        public AdapterCard(string id)
        {
            Margin = new Thickness(0, 0, 0, 10); Padding = new Thickness(16); BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(4);
            SetResourceReference(BackgroundProperty, "PanelBrush"); SetResourceReference(BorderBrushProperty, "BorderBrush");
            var root = new StackPanel(); Child = root;
            root.Children.Add(title);
            var identifier = new TextBlock { Text = id, FontFamily = new FontFamily("Consolas"), FontSize = 10, Margin = new Thickness(0, 3, 0, 12) };
            identifier.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush"); root.Children.Add(identifier);
            var metrics = new Grid();
            for (var column = 0; column < 3; column++) metrics.ColumnDefinitions.Add(new());
            var labels = new[] { engineLabel, dedicatedLabel, sharedLabel };
            var values = new[] { utilization, dedicated, shared };
            for (var column = 0; column < 3; column++)
            {
                var group = new StackPanel(); SetColumn(group, column);
                labels[column].SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
                values[column].FontSize = 24; values[column].FontWeight = FontWeights.SemiBold;
                group.Children.Add(labels[column]); group.Children.Add(values[column]); metrics.Children.Add(group);
            }
            root.Children.Add(metrics); root.Children.Add(chart);
        }

        public void Update(GpuAdapterSample value, int adapterIndex)
        {
            sample = value; index = adapterIndex;
            chart.Add(value.UtilizationPercent); UpdateLabels();
        }
        public void UpdateLabels()
        {
            title.Text = StudioStrings.Text("GPU adapter") + " " + (index + 1).ToString(CultureInfo.CurrentCulture);
            engineLabel.Text = StudioStrings.Text("Busiest engine");
            dedicatedLabel.Text = StudioStrings.Text("Dedicated memory"); sharedLabel.Text = StudioStrings.Text("Shared memory");
            utilization.Text = sample?.UtilizationPercent?.ToString("F1", CultureInfo.CurrentCulture) + (sample?.UtilizationPercent == null ? "—" : " %");
            dedicated.Text = FormatMemory(sample?.DedicatedBytes); shared.Text = FormatMemory(sample?.SharedBytes);
        }
        private static string FormatMemory(double? bytes) => !bytes.HasValue ? "—" : bytes.Value >= 1024 * 1024 * 1024
            ? (bytes.Value / (1024 * 1024 * 1024)).ToString("F2", CultureInfo.CurrentCulture) + " GiB"
            : (bytes.Value / (1024 * 1024)).ToString("F0", CultureInfo.CurrentCulture) + " MiB";
    }

    private sealed class GpuHistoryChart : FrameworkElement
    {
        private const int Capacity = 60;
        private readonly Queue<double?> values = new();
        public void Add(double? value) { values.Enqueue(value); while (values.Count > Capacity) values.Dequeue(); InvalidateVisual(); }
        protected override void OnRender(DrawingContext drawing)
        {
            base.OnRender(drawing);
            var border = TryFindResource("BorderBrush") as Brush ?? Brushes.Gray;
            var accent = TryFindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue;
            var width = ActualWidth; var height = ActualHeight;
            if (width <= 0 || height <= 0) return;
            var gridPen = new Pen(border, 0.5);
            for (var line = 0; line <= 4; line++) drawing.DrawLine(gridPen, new Point(0, line * height / 4), new Point(width, line * height / 4));
            var linePen = new Pen(accent, 1.8);
            Point? previous = null; var index = Capacity - values.Count;
            foreach (var value in values)
            {
                if (value == null) { previous = null; index++; continue; }
                var point = new Point(index++ * width / (Capacity - 1), height * (1 - value.Value / 100));
                if (previous is { } start) drawing.DrawLine(linePen, start, point);
                else drawing.DrawEllipse(accent, null, point, 2, 2);
                previous = point;
            }
        }
    }
}
