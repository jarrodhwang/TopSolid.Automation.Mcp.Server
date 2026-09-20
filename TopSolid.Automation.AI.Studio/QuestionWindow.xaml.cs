using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Preview;

namespace TopSolid.Automation.AI.Studio;

public partial class QuestionWindow : Window
{
    private sealed record Card(QuestionChoice Choice, int Group = 0, bool Grouped = false, bool CanShowDetail = false)
    {
        public string Label => Choice.Label;
        public string Detail => Choice.Detail;
        public ImageSource Icon => TopSolidIcons.Get(Choice.IconKey ?? IconKey(Choice.Kind));
        public string ToolText => Choice.ToolText ?? "";
        public bool IsOperation => Choice.Kind == "operation";
        public ImageSource DetailIcon => TopSolidIcons.Get("arguments");
        public string DetailTooltip => StudioStrings.Get("Cam.DetailTarget", Label);
        // Display formatting only; selection and execution continue to use the original Choice.
        private readonly string[] toolLines = (Choice.ToolText ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        private static readonly Regex ToolPrefix = new(@"^T\s*\d+(?:\s*[:：]\s*|$)", RegexOptions.CultureInvariant);
        private string FirstToolLine => toolLines.FirstOrDefault() ?? "";
        private Match PocketMatch => ToolPrefix.Match(FirstToolLine);
        public string ToolPocket => PocketMatch.Success ? PocketMatch.Value.Trim().TrimEnd(':', '：').Trim() : "";
        public string ToolName => PocketMatch.Success
            ? PocketMatch.Length == FirstToolLine.Length ? toolLines.ElementAtOrDefault(1) ?? "" : FirstToolLine[PocketMatch.Length..].Trim()
            : FirstToolLine;
        public string ToolKind => string.Join(" · ", toolLines.Skip(PocketMatch.Success && PocketMatch.Length == FirstToolLine.Length ? 2 : 1));
        public ImageSource ToolIcon => TopSolidIcons.Get(Choice.ToolIconKey ?? "cam-tool-generic");
        public string GroupLabel => string.IsNullOrWhiteSpace(ToolText) ? StudioStrings.Get(Choice.ToolGroupKey == "no-tool" ? "Question.NoTool" : "Question.ToolUnavailable") : ToolText.Replace('\n', ' ');
        public string GroupName => string.IsNullOrWhiteSpace(ToolName) ? GroupLabel : ToolName;
        public Visibility ToolVisibility => Grouped || string.IsNullOrWhiteSpace(ToolText) ? Visibility.Collapsed : Visibility.Visible;
        public GridLength ToolColumnWidth => Grouped || string.IsNullOrWhiteSpace(ToolText) ? new GridLength(0) : new GridLength(1.1, GridUnitType.Star);
        public string AccessibleDescription => string.Join(" · ", new[] { Detail, ToolText }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
    private UserQuestion question;
    private readonly HashSet<string> selected = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource lifetime = new();
    private bool filtering, synchronizingColor, loadingImage, closed;
    private ChatAttachment? attachment;
    private readonly GraphicPreviewPane? graphic;
    private readonly IMcpClient? detailClient;
    private string? inspectionKey;
    private bool IsCam => question.Kind == "select" && question.Choices.Count > 0 && question.Choices.All(c => c.Kind == "operation");
    internal Func<JObject, CancellationToken, Task<bool>>? ConfirmDetailChange { get; set; }
    internal Action<ChatTrace> DetailTrace { get; set; } = _ => { };
    public QuestionAnswer? Answer { get; private set; }

    public QuestionWindow(UserQuestion question, IGraphicPreviewClient? previewClient = null)
    {
        this.question = question;
        InitializeComponent(); StudioStrings.InitializeResources(this);
        detailClient = previewClient as IMcpClient;
        if (question.Choices.Any(c => !string.IsNullOrWhiteSpace(c.ToolText))) Width = 900;
        if (previewClient != null && question.Kind == "select" && question.Choices.Any(c => question.PreviewTargetFor(c.Key) != null))
        {
            graphic = new GraphicPreviewPane(previewClient, question.DocumentPreview);
            BodyHost.Content = null;
            BodyHost.Content = PreviewLayout.Wrap(this, QuestionBody, graphic, narrowTabs: true);
        }
        if (question.Kind is not ("select" or "image")) { MinHeight = 380; Height = question.Kind == "color" ? 520 : 440; EditorScroll.Visibility = Visibility.Visible; }
        Title = StudioStrings.Get("Question.Title"); Icon = TopSolidIcons.Get("question"); TopSolidTheme.ApplyWindow(this);
        CancelQuestion.Tag = TopSolidIcons.Get("cancel"); ContinueQuestion.Tag = TopSolidIcons.Get("approve"); BrowseImage.Tag = TopSolidIcons.Get("image");
        if (question.Kind == "select" && question.LoadMoreAsync != null)
        {
            LoadMore.Visibility = Visibility.Visible;
            LoadMore.Click += async (_, _) => await LoadNextPage();
        }
        UpdateHeader();
        SearchPanel.Visibility = ChoiceList.Visibility = question.Kind == "select" ? Visibility.Visible : Visibility.Collapsed;
        ChoiceList.SelectionMode = question.Multiple ? IsCam ? SelectionMode.Extended : SelectionMode.Multiple : SelectionMode.Single;
        ChoiceList.SelectionChanged += (_, e) =>
        {
            if (filtering) return;
            if (!question.Multiple && e.AddedItems.Count > 0) selected.Clear();
            foreach (Card card in e.RemovedItems) selected.Remove(card.Choice.Key);
            foreach (Card card in e.AddedItems) selected.Add(card.Choice.Key);
            inspectionKey = e.AddedItems.OfType<Card>().LastOrDefault()?.Choice.Key ??
                (inspectionKey != null && selected.Contains(inspectionKey) ? inspectionKey : question.Choices.LastOrDefault(c => selected.Contains(c.Key))?.Key);
            UpdateCount(); Validate();
            graphic?.SetTarget(question.PreviewTargetFor(inspectionKey) ?? question.DocumentPreview);
        };
        ChoiceList.GotKeyboardFocus += (_, e) =>
        {
            if (filtering || !IsCam || e.OriginalSource is not DependencyObject source ||
                ItemsControl.ContainerFromElement(ChoiceList, source) is not ListBoxItem { Content: Card card } || !selected.Contains(card.Choice.Key)) return;
            inspectionKey = card.Choice.Key; UpdateCount(); graphic?.SetTarget(question.PreviewTargetFor(inspectionKey));
        };
        SearchBox.TextChanged += (_, _) => Filter();
        GroupByToolIcon.Source = TopSolidIcons.Get("operation-group-tool");
        GroupByTool.Checked += (_, _) => Filter();
        GroupByTool.Unchecked += (_, _) => Filter();
        ClearSelection.Click += (_, _) => { selected.Clear(); inspectionKey = null; Filter(); graphic?.SetTarget(question.DocumentPreview); };
        ValueBox.TextChanged += (_, _) => Validate();
        HexBox.TextChanged += (_, _) => ColorFromHex();
        foreach (var box in new[] { RedBox, GreenBox, BlueBox }) box.TextChanged += (_, _) => ColorFromRgb();
        BrowseImage.Click += async (_, _) => await PickImage();
        ContinueQuestion.Click += (_, _) => { if (Validate(showError: true)) { Answer = BuildAnswer(); DialogResult = true; } };
        CancelQuestion.Click += (_, _) => Close();
        PreviousStep.Click += (_, _) => { Answer = UserQuestion.Back(); DialogResult = true; };
        Closed += (_, _) => { closed = true; lifetime.Cancel(); graphic?.Dispose(); lifetime.Dispose(); };
        if (question.Kind == "select")
        {
            if (question.InitialInspectionKey is { } initial && question.Choices.Any(c => c.Key == initial && c.Kind == "operation"))
            { selected.Add(initial); inspectionKey = initial; }
            Filter();
            if (inspectionKey != null) graphic?.SetTarget(question.PreviewTargetFor(inspectionKey));
        }
        else if (question.Kind == "color")
        {
            ColorPanel.Visibility = Visibility.Visible;
            foreach (var hex in new[] { "#EF4444", "#F97316", "#FACC15", "#22C55E", "#14B8A6", "#0EA5E9", "#3B82F6", "#8B5CF6", "#EC4899", "#FFFFFF", "#64748B", "#171717" })
            {
                var button = new Button { Width = 38, Height = 38, Margin = new Thickness(0, 0, 9, 9), Padding = new Thickness(0),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)), ToolTip = hex };
                button.SetResourceReference(StyleProperty, "SwatchButton"); AutomationProperties.SetName(button, hex);
                button.Click += (_, _) => HexBox.Text = hex;
                Swatches.Children.Add(button);
            }
        }
        else if (question.Kind == "image") { ImagePanel.Visibility = Visibility.Visible; QuestionHint.Text = StudioStrings.Get("Question.ImageHint"); }
        else
        {
            InputPanel.Visibility = Visibility.Visible;
            InputLabel.Text = StudioStrings.Get("Question.Kind." + question.Kind) + (question.Unit.Length > 0 ? " · " + question.Unit : "");
            AutomationProperties.SetName(ValueBox, InputLabel.Text);
            ValueBox.AcceptsReturn = question.Kind == "text"; ValueBox.MinHeight = question.Kind == "text" ? 120 : 46;
            RangeLabel.Text = string.Join("  ·  ", new[] { question.Minimum.HasValue ? StudioStrings.Get("Question.Minimum", question.Minimum) : "",
                question.Maximum.HasValue ? StudioStrings.Get("Question.Maximum", question.Maximum) : "" }.Where(s => s.Length > 0));
        }
        Loaded += (_, _) => { if (question.Kind == "select") SearchBox.Focus(); else if (question.Kind == "color") HexBox.Focus(); else if (question.Kind == "image") BrowseImage.Focus(); else ValueBox.Focus(); };
        Validate();
    }

    private void Filter()
    {
        filtering = true;
        try
        {
            var grouped = GroupByTool.IsChecked == true && GroupByTool.Visibility == Visibility.Visible;
            var runs = OperationToolGroups.Runs(question.Choices);
            var cards = question.Choices.Where(c => c.SearchText.Contains(SearchBox.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)).Select(c => new Card(c, runs[c.Key], grouped,
                c.Kind == "operation" && detailClient != null && question.PreviewTargetFor(c.Key)?["operation"] is JObject)).ToArray();
            // WPF can retain an equal record across ItemsSource replacement.
            // Clear the visual selection first, then restore only the exact keys
            // we retained; "Clear selection" must also allow reselecting that item.
            ChoiceList.UnselectAll();
            var view = new ListCollectionView(cards);
            if (grouped) view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Card.Group)));
            ChoiceList.ItemsSource = view;
            foreach (var card in cards.Where(c => selected.Contains(c.Choice.Key)))
                if (question.Multiple) ChoiceList.SelectedItems.Add(card); else ChoiceList.SelectedItem = card;
            EmptySearch.Visibility = cards.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateCount();
        }
        finally { filtering = false; }
        Validate();
    }

    private void UpdateCount()
    {
        SelectionCount.Text = IsCam ? StudioStrings.Get("Cam.DialogCount", question.Choices.Count, question.Total > 0 ? question.Total : question.Choices.Count, selected.Count) : question.IsBrowse
            ? StudioStrings.Get(question.HasMore ? "List.PartialCount" : "List.Count", ChoiceList.Items.Count, question.Choices.Count, question.Total)
            : StudioStrings.Get("Question.Count", ChoiceList.Items.Count, question.Choices.Count, selected.Count);
        ClearSelection.IsEnabled = selected.Count > 0;
        SelectionSummary.Visibility = selected.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SelectionSummary.Text = string.Join("; ", question.Choices.Where(c => selected.Contains(c.Key)).Take(8).Select(c => c.Label + (c.Detail.Length > 0 ? " — " + c.Detail : "")));
        SelectionSummary.ToolTip = SelectionSummary.Text;
    }
    private void OperationDetail_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { DataContext: Card card } || !card.CanShowDetail || detailClient?.IsConnected != true) return;
        var choice = card.Choice;
        inspectionKey = choice.Key;
        if (question.PreviewTargetFor(choice.Key)?["operation"] is not JObject operation) return;
        var dialog = new CamOperationDetailWindow(choice.Label, choice.ToolText, choice.IconKey ?? "operation", operation, detailClient,
            ConfirmDetailChange, DetailTrace, (before, after) => question.RebaseDocument(before, after)) { Owner = this };
        dialog.ShowDialog();
        graphic?.SetTarget(question.PreviewTargetFor(choice.Key) ?? question.DocumentPreview);
    }
    private QuestionAnswer BuildAnswer() => question.Answer(question.Kind == "color" ? HexBox.Text : ValueBox.Text, selected, attachment);
    private bool Validate(bool showError = false)
    {
        if (ContinueQuestion == null) return false;
        if (question.IsBrowse) return true;
        try
        {
            if (loadingImage) { ContinueQuestion.IsEnabled = false; return false; }
            _ = BuildAnswer();
            if (question.Kind == "color" && (!byte.TryParse(RedBox.Text, out _) || !byte.TryParse(GreenBox.Text, out _) || !byte.TryParse(BlueBox.Text, out _)))
                throw new ArgumentException(StudioStrings.Get("Question.ColorRequired"));
            ContinueQuestion.IsEnabled = true; ValidationMessage.Text = ""; return true;
        }
        catch (ArgumentException error)
        {
            ContinueQuestion.IsEnabled = false;
            ValidationMessage.Text = showError || ValueBox.Text.Length > 0 || HexBox.Text.Length > 0 ? error.Message : "";
            return false;
        }
    }

    private async Task LoadNextPage()
    {
        if (question.LoadMoreAsync == null) return;
        LoadMore.IsEnabled = false; ValidationMessage.Text = "";
        try
        {
            var next = await question.LoadMoreAsync(lifetime.Token);
            if (closed) return;
            question = next; UpdateHeader(); Filter();
            graphic?.SetTarget(question.PreviewTargetFor(inspectionKey) ?? question.DocumentPreview);
        }
        catch (OperationCanceledException) when (closed) { }
        catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException or Newtonsoft.Json.JsonException)
        { if (!closed) ValidationMessage.Text = StudioStrings.Get("List.Incomplete"); }
        finally { if (!closed) LoadMore.IsEnabled = true; }
    }

    private void ColorFromHex()
    {
        if (synchronizingColor) return;
        synchronizingColor = true;
        try
        {
            var answer = question.Answer(HexBox.Text).Data["value"]!;
            RedBox.Text = answer["r"]!.ToString(); GreenBox.Text = answer["g"]!.ToString(); BlueBox.Text = answer["b"]!.ToString();
            ColorPreview.Background = new SolidColorBrush(Color.FromRgb((byte)answer["r"]!, (byte)answer["g"]!, (byte)answer["b"]!));
        }
        catch (ArgumentException) { ColorPreview.Background = Brushes.Transparent; }
        finally { synchronizingColor = false; }
        Validate();
    }

    private void ColorFromRgb()
    {
        if (synchronizingColor) return;
        if (byte.TryParse(RedBox.Text, out var r) && byte.TryParse(GreenBox.Text, out var g) && byte.TryParse(BlueBox.Text, out var b))
            HexBox.Text = $"#{r:X2}{g:X2}{b:X2}";
        Validate(showError: true);
    }

    private async Task PickImage()
    {
        var picker = new OpenFileDialog { Filter = StudioStrings.Get("Question.ImageFilter"), Multiselect = false };
        if (picker.ShowDialog(this) != true) return;
        loadingImage = true; BrowseImage.IsEnabled = false; ValidationMessage.Text = StudioStrings.Get("Question.LoadingImage"); Validate();
        try
        {
            var image = await ChatAttachments.LoadAsync(picker.FileName, lifetime.Token);
            if (closed) return;
            SetImage(image);
        }
        catch (OperationCanceledException) when (closed) { }
        catch (Exception error) when (error is IOException or ArgumentException or NotSupportedException or InvalidOperationException or FormatException or UnauthorizedAccessException)
        { if (!closed) { attachment = null; ImagePreview.Source = null; ImageName.Text = ""; ValidationMessage.Text = error.Message; } }
        finally { loadingImage = false; if (!closed) { BrowseImage.IsEnabled = true; if (attachment != null) Validate(); } }
    }

    internal void SetImage(ChatAttachment image)
    {
        if (question.Kind != "image" || !image.IsImage) throw new ArgumentException(StudioStrings.Get("Question.ImageRequired"));
        ChatAttachments.Validate([image]);
        // Decode only the bounded snapshot, never a model-provided URL or a mutable file path.
        using var stream = new MemoryStream(Convert.FromBase64String(image.Image!.Base64));
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = Math.Min(900, image.Image.Width); bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
        attachment = image; ImagePreview.Source = bitmap; ImageName.Text = image.Name;
        Validate();
    }

    internal static string IconKey(string kind) => kind switch
    { "camParameter" or "integer" or "decimal" => "parameter", "select" or "option" or "text" => "status", "element" or "part" or "sketch" => "part", "tool" => "cam-tool-generic", _ => kind };

    private void UpdateHeader()
    {
        PreviousStep.Visibility = question.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
        LoadMore.Visibility = question.LoadMoreAsync != null ? Visibility.Visible : Visibility.Collapsed;
        QuestionTitle.Text = IsCam ? StudioStrings.Get("Cam.Operations") : question.Title;
        var documentChoices = question.Kind == "select" && question.Choices.Count > 0 &&
            question.Choices.All(c => c.Kind == "document" || c.IconKey == "document" || c.IconKey?.StartsWith("document-", StringComparison.Ordinal) == true);
        var operationChoices = question.Kind == "select" && question.Choices.Count > 0 && question.Choices.All(c => c.Kind == "operation");
        GroupByTool.Visibility = operationChoices ? Visibility.Visible : Visibility.Collapsed;
        QuestionIcon.Source = TopSolidIcons.Get(operationChoices ? "operation" : documentChoices ? "document" : IconKey(question.Kind == "select" ? question.ItemKind : question.Kind));
        QuestionHint.Text = StudioStrings.Get(question.Kind == "select" ? question.Multiple ? "Question.MultipleHint" : "Question.SelectHint" : "Question.InputHint");
        Title = question.IsBrowse ? StudioStrings.Get("List.Title") : StudioStrings.Get("Question.Title");
        if (question.IsBrowse)
        {
            QuestionHint.Text = StudioStrings.Get("List.Hint");
            CancelQuestion.Content = StudioStrings.Get("List.Close");
            ContinueQuestion.Visibility = Visibility.Collapsed;
            ClearSelection.Visibility = IsCam ? Visibility.Visible : Visibility.Collapsed;
            LoadMore.Visibility = question.LoadMoreAsync != null ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            CancelQuestion.Content = StudioStrings.Get("Question.Cancel");
            ContinueQuestion.Visibility = Visibility.Visible;
            ClearSelection.Visibility = Visibility.Visible;
        }
        QuestionContext.Text = question.Context;
        if (IsCam)
        {
            Title = StudioStrings.Get("Cam.Operations");
            QuestionHint.Text = StudioStrings.Get(question.Multiple ? "Cam.DialogMultipleHint" : "Cam.DialogHint");
            QuestionContext.Text = question.HasNavigationContext ? question.Context : "";
        }
        QuestionContext.Visibility = string.IsNullOrWhiteSpace(QuestionContext.Text) ? Visibility.Collapsed : Visibility.Visible;
    }
}
