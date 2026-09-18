using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Preview;

namespace TopSolid.Automation.AI.Studio;

public partial class QuestionWindow : Window
{
    private sealed record Card(QuestionChoice Choice)
    {
        public string Label => Choice.Label;
        public string Detail => Choice.Detail;
        public ImageSource Icon => TopSolidIcons.Get(IconKey(Choice.Kind));
    }
    private readonly UserQuestion question;
    private readonly HashSet<string> selected = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource lifetime = new();
    private bool filtering, synchronizingColor, loadingImage, closed;
    private ChatAttachment? attachment;
    private readonly GraphicPreviewPane? graphic;
    public QuestionAnswer? Answer { get; private set; }

    public QuestionWindow(UserQuestion question, IGraphicPreviewClient? previewClient = null)
    {
        this.question = question;
        InitializeComponent(); StudioStrings.InitializeResources(this);
        if (previewClient != null && question.Kind == "select" && question.Choices.Any(c => question.PreviewTargetFor(c.Key) != null))
        {
            graphic = new GraphicPreviewPane(previewClient, null);
            var details = (FrameworkElement)Content; Content = null; details.Margin = new Thickness(0);
            var review = PreviewLayout.Wrap(this, details, graphic); review.Margin = new Thickness(24); Content = review;
        }
        if (question.Kind is not ("select" or "image")) { MinHeight = 380; Height = question.Kind == "color" ? 520 : 440; EditorScroll.Visibility = Visibility.Visible; }
        Title = StudioStrings.Get("Question.Title"); Icon = TopSolidIcons.Get("app"); TopSolidTheme.ApplyWindow(this);
        QuestionTitle.Text = question.Title;
        QuestionIcon.Source = TopSolidIcons.Get(IconKey(question.Kind == "select" ? question.ItemKind : question.Kind));
        QuestionHint.Text = StudioStrings.Get(question.Kind == "select" ? question.Multiple ? "Question.MultipleHint" : "Question.SelectHint" : "Question.InputHint");
        SearchPanel.Visibility = ChoiceList.Visibility = question.Kind == "select" ? Visibility.Visible : Visibility.Collapsed;
        ChoiceList.SelectionMode = question.Multiple ? SelectionMode.Multiple : SelectionMode.Single;
        ChoiceList.SelectionChanged += (_, e) =>
        {
            if (filtering) return;
            if (!question.Multiple && e.AddedItems.Count > 0) selected.Clear();
            foreach (Card card in e.RemovedItems) selected.Remove(card.Choice.Key);
            foreach (Card card in e.AddedItems) selected.Add(card.Choice.Key);
            UpdateCount(); Validate();
            graphic?.SetTarget(question.PreviewTargetFor((e.AddedItems.OfType<Card>().LastOrDefault()?.Choice.Key) ?? selected.LastOrDefault()));
        };
        SearchBox.TextChanged += (_, _) => Filter();
        ClearSelection.Click += (_, _) => { selected.Clear(); Filter(); graphic?.SetTarget(null); };
        ValueBox.TextChanged += (_, _) => Validate();
        HexBox.TextChanged += (_, _) => ColorFromHex();
        foreach (var box in new[] { RedBox, GreenBox, BlueBox }) box.TextChanged += (_, _) => ColorFromRgb();
        BrowseImage.Click += async (_, _) => await PickImage();
        ContinueQuestion.Click += (_, _) => { if (Validate(showError: true)) { Answer = BuildAnswer(); DialogResult = true; } };
        CancelQuestion.Click += (_, _) => Close();
        Closed += (_, _) => { closed = true; lifetime.Cancel(); lifetime.Dispose(); };
        if (question.Kind == "select") Filter();
        else if (question.Kind == "color")
        {
            ColorPanel.Visibility = Visibility.Visible;
            foreach (var hex in new[] { "#EF4444", "#F97316", "#FACC15", "#22C55E", "#14B8A6", "#0EA5E9", "#3B82F6", "#8B5CF6", "#EC4899", "#FFFFFF", "#64748B", "#171717" })
            {
                var button = new Button { Width = 38, Height = 38, Margin = new Thickness(0, 0, 9, 9), Padding = new Thickness(0),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)), ToolTip = hex };
                ControlChrome.SetCornerRadius(button, new CornerRadius(9)); AutomationProperties.SetName(button, hex);
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
            var cards = question.Choices.Where(c => c.SearchText.Contains(SearchBox.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)).Select(c => new Card(c)).ToArray();
            ChoiceList.ItemsSource = cards;
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
        SelectionCount.Text = StudioStrings.Get("Question.Count", ChoiceList.Items.Count, question.Choices.Count, selected.Count);
        ClearSelection.IsEnabled = selected.Count > 0;
        SelectionSummary.Visibility = selected.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SelectionSummary.Text = string.Join("; ", question.Choices.Where(c => selected.Contains(c.Key)).Take(8).Select(c => c.Label + (c.Detail.Length > 0 ? " — " + c.Detail : "")));
        SelectionSummary.ToolTip = SelectionSummary.Text;
    }
    private QuestionAnswer BuildAnswer() => question.Answer(question.Kind == "color" ? HexBox.Text : ValueBox.Text, selected, attachment);
    private bool Validate(bool showError = false)
    {
        if (ContinueQuestion == null) return false;
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
    { "camParameter" or "integer" or "decimal" => "parameter", "select" or "option" or "text" => "status", "element" => "part", _ => kind };
}
