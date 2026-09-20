using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.Tests;

internal static class QuestionUiTests
{
    internal static async Task Run(Window owner, Action<Window, string> render)
    {
        var language = StudioStrings.CurrentLanguage;
        try
        {
            StudioStrings.Apply("ko");
            foreach (var dark in new[] { false, true })
            {
                TopSolidTheme.Apply(new(dark, dark ? "Dark" : "Light", "CAM selection fixture"));
                var source = new QuestionSources();
                var rows = new[] { CamSelectionTests.Row(1), CamSelectionTests.Row(2), CamSelectionTests.Row(3) };
                foreach (var row in rows.Skip(1)) row["tool"] = new JObject { ["documentId"] = "cam-test", ["id"] = 42 };
                source.Capture("grouped-cam", CamSelectionRequest.Tool, new JObject(), new TopSolid.Automation.Mcp.Contracts.McpToolResult
                    { StructuredContent = new JObject { ["items"] = new JArray(rows) } });
                var cam = Window(owner, source.Create(new JObject { ["question"] = StudioStrings.Get("Cam.Select"), ["kind"] = "select", ["itemKind"] = "operation", ["multiple"] = true,
                    ["sources"] = new JArray(new JObject { ["toolCallId"] = "grouped-cam", ["path"] = "/items" }) }));
                try
                {
                    cam.Show(); await Layout(cam);
                    var visible = string.Join("\n", Descendants<TextBlock>(cam).Select(t => t.Text));
                    Check.True(visible.Contains("T 2") && visible.Contains("페이스밀") && visible.Contains("환경 활성") &&
                        visible.Contains("Face Mill D40 A90 L3 SD41") && !visible.Contains("공구 기능") && !visible.Contains("TopSolid.Cam"), "CAM cards leaked native names or omitted number/spec/type");
                    render(cam, $"question-cam-number-type-ko-{(dark ? "dark" : "light")}.png");
                    var list = Find<ListBox>(cam, "ChoiceList"); list.SelectedIndex = 1;
                    var group = Find<ToggleButton>(cam, "GroupByTool"); Check.True(group.IsChecked != true,"Tool grouping became the default");
                    group.IsChecked = true; await Layout(cam);
                    Check.True(list.SelectedItems.Count == 1 && list.Items.Groups?.Count == 2,"Grouping lost the selection or contiguous tool headers");
                    render(cam, $"question-cam-grouped-ko-{(dark ? "dark" : "light")}.png");
                }
                finally { cam.Close(); }
            }
            foreach (var dark in new[] { false, true })
            {
                TopSolidTheme.Apply(new(dark, "Parameter editor", "fixture"));
                var real = CamWorkflowImprovementTests.Receipt();
                var boolean = CamWorkflowImprovementTests.Receipt("Automatic safety block", "Boolean");
                boolean["value"]!["booleanValue"] = true; boolean["value"]!["displayValue"] = "True";
                boolean["value"]!["allowedValues"] = new JArray(new JObject { ["value"] = false }, new JObject { ["value"] = true });
                var enumeration = CamWorkflowImprovementTests.Receipt("Action at machine travel limit", "Integer");
                enumeration["value"]!["integerValue"] = 0; enumeration["value"]!["displayValue"] = "Return to start";
                enumeration["value"]!["allowedValues"] = new JArray(new JObject { ["value"] = 0, ["label"] = "Return to start" }, new JObject { ["value"] = 1, ["label"] = "Return to end" });
                var editor = new CamParameterEditorWindow([real, boolean, enumeration]) { Owner = owner, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
                try
                {
                    editor.Show(); await Layout(editor);
                    Check.True(Descendants<ComboBox>(editor).All(c => c.SelectedItem is ComboBoxItem { Content: not null }), "Native parameter choices have no selected value");
                    render(editor, $"cam-parameter-editor-{(dark ? "dark" : "light")}.png");
                }
                finally { editor.Close(); }
            }
            foreach (var dark in new[] { false, true })
            foreach (var locale in new[] { "en", "ko" })
            {
                StudioStrings.Apply(locale); TopSolidTheme.Apply(new(dark, dark ? "Dark" : "Light", "Question UI fixture"));
                var q = UserQuestionTests.ProjectQuestion();
                var dialog = Window(owner, q);
                try
                {
                    dialog.Show(); await Layout(dialog);
                    var list = Find<ListBox>(dialog, "ChoiceList"); var search = Find<TextBox>(dialog, "SearchBox");
                    var confirm = Find<Button>(dialog, "ContinueQuestion");
                    Check.True(!confirm.IsEnabled, "Question silently preselected a target");
                    list.SelectedIndex = 0; Check.True(confirm.IsEnabled, "Selected object cannot be submitted");
                    search.Text = "Training"; Check.Equal(1, list.Items.Count, "Search ignored location context");
                    list.SelectedIndex = 0; Check.True(confirm.IsEnabled, "Replacing a filtered single selection retained hidden target");
                    search.Clear(); await Layout(dialog);
                    Check.Equal(1, list.SelectedIndex, "Filtering changed selected object identity");
                    var visible = string.Join("\n", Descendants<TextBlock>(dialog).Select(t => t.Text));
                    Check.True(!visible.Contains("fad512f5") && visible.Contains("Gear Design"), "Question UI omitted names or displayed IDs");
                    render(dialog, $"question-project-{locale}-{(dark ? "dark" : "light")}.png");
                    search.Text = "does not exist";
                    Check.True(Find<TextBlock>(dialog, "EmptySearch").Visibility == Visibility.Visible, "Empty search has no feedback");
                    Check.True(confirm.IsEnabled, "Filtering silently cleared a prior selection");
                    Check.True(Find<TextBlock>(dialog, "SelectionSummary").Text.Contains("Training"), "Hidden selection lacks visible target context");
                    Find<Button>(dialog, "ClearSelection").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check.True(!confirm.IsEnabled, "Clearing selection left an executable answer");
                    Check.True(list.SelectedIndex == -1 && list.SelectedItems.Count == 0, "Cleared target still appears selected");
                    search.Clear();
                    list.SelectedIndex = 0;
                    Find<Button>(dialog, "ClearSelection").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check.Equal(-1, list.SelectedIndex, "Equal choice records retained a visible cleared target");
                    list.SelectedIndex = 0;
                    Check.True(confirm.IsEnabled, "The same target cannot be reselected after clearing");
                }
                finally { dialog.Close(); }
            }
            StudioStrings.Apply("en"); TopSolidTheme.Apply(new(false, "Light", "Question UI fixture"));
            var documents = Window(owner, UserQuestionTests.DocumentQuestion());
            try
            {
                documents.Show(); await Layout(documents);
                Check.True(ReferenceEquals(Find<Image>(documents, "QuestionIcon").Source, TopSolidIcons.Get("document")), "Document picker heading used an information icon");
                var images = Descendants<Image>(Find<ListBox>(documents, "ChoiceList")).Select(i => i.Source).ToList();
                Check.True(images.Contains(TopSolidIcons.Get("document-part")) && images.Contains(TopSolidIcons.Get("document-millturn")), "Document cards lost native part/CAM icons");
                render(documents, "question-document-types.png");
            }
            finally { documents.Close(); }
            var multiple = Window(owner, UserQuestionTests.ProjectQuestion(multiple: true));
            try
            {
                multiple.Show(); await Layout(multiple); var list = Find<ListBox>(multiple, "ChoiceList");
                list.SelectedItems.Add(list.Items[0]); list.SelectedItems.Add(list.Items[2]);
                var search = Find<TextBox>(multiple, "SearchBox"); search.Text = "Training"; search.Clear();
                Check.Equal(2, list.SelectedItems.Count, "Multi-selection was lost while filtering");
            }
            finally { multiple.Close(); }
            foreach (var kind in new[] { "integer", "decimal", "text", "color" })
            {
                var input = new JObject { ["question"] = kind == "color" ? "Choose the shape color" : "Enter the cutting feed", ["kind"] = kind };
                if (kind is "integer" or "decimal") { input["unit"] = "mm/min"; input["minimum"] = 0; input["maximum"] = 1000; }
                var question = new QuestionSources().Create(input); var dialog = Window(owner, question);
                try
                {
                    dialog.Show(); await Layout(dialog);
                    var button = Find<Button>(dialog, "ContinueQuestion");
                    Check.True(!button.IsEnabled, "Empty typed question enabled Continue");
                    if (kind == "color")
                    {
                        Find<TextBox>(dialog, "HexBox").Text = "#3789BC"; Check.True(button.IsEnabled, "Valid hex color rejected");
                        Check.Equal("55", Find<TextBox>(dialog, "RedBox").Text, "Hex-to-RGB synchronization failed");
                        Find<TextBox>(dialog, "RedBox").Text = "999"; Check.True(!button.IsEnabled, "Invalid RGB accepted");
                        Find<TextBox>(dialog, "RedBox").Text = "255";
                        Check.Equal("#FF89BC", Find<TextBox>(dialog, "HexBox").Text, "RGB-to-hex synchronization failed");
                    }
                    else
                    {
                        var box = Find<TextBox>(dialog, "ValueBox"); box.Text = kind == "text" ? "Fixture name" : "250";
                        Check.True(button.IsEnabled, "Valid typed input rejected");
                        if (kind != "text") { box.Text = "1001"; Check.True(!button.IsEnabled, "Out-of-range input enabled Continue"); box.Text = "250"; }
                    }
                    await Layout(dialog); render(dialog, "question-" + kind + ".png");
                    // Native modal submission verifies the value actually leaves the window.
                }
                finally { dialog.Close(); }
            }
            var modal = Window(owner, UserQuestionTests.ProjectQuestion());
            var imageQuestion = Window(owner, new QuestionSources().Create(new JObject { ["question"] = "Choose a reference image", ["kind"] = "image" }));
            try
            {
                imageQuestion.Show(); await Layout(imageQuestion);
                Check.True(!Find<Button>(imageQuestion, "ContinueQuestion").IsEnabled, "Image question accepted an empty attachment");
                imageQuestion.SetImage(await ChatAttachments.LoadAsync(Path.GetFullPath("TopSolid.Automation.AI.Studio/Assets/TopSolid/project.png"), CancellationToken.None));
                Check.True(Find<Image>(imageQuestion, "ImagePreview").Source != null && Find<Button>(imageQuestion, "ContinueQuestion").IsEnabled, "Image preview or submit failed");
                await Layout(imageQuestion); render(imageQuestion, "question-image.png");
            }
            finally { imageQuestion.Close(); }
            _ = modal.Dispatcher.BeginInvoke(() =>
            {
                Find<ListBox>(modal, "ChoiceList").SelectedIndex = 1;
                Find<Button>(modal, "ContinueQuestion").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }, DispatcherPriority.Loaded);
            Check.True(modal.ShowDialog() == true && modal.Answer != null, "Question modal did not return an answer");
            Check.Equal("fad512f5-5bf7-4500-9b72-f4ea8669ce30", (string?)modal.Answer!.Data["selected"]![0]!["value"]!["pdmObjectId"], "Modal returned wrong project");

            // Exercise MainWindow's real question callback, including cancellation and loading feedback.
            var method = typeof(MainWindow).GetMethod("AskUser", BindingFlags.Instance | BindingFlags.NonPublic)!;
            using var cancellation = new CancellationTokenSource();
            _ = owner.Dispatcher.BeginInvoke(() =>
            {
                var active = owner.OwnedWindows.OfType<QuestionWindow>().Single();
                var progress = (ProgressBar)owner.FindName("ActivityProgress");
                Check.True(progress.Visibility == Visibility.Collapsed && !progress.IsIndeterminate, "Question wait still showed AI activity");
                cancellation.Cancel();
            }, DispatcherPriority.Loaded);
            var answerTask = (Task<QuestionAnswer?>)method.Invoke(owner, [UserQuestionTests.ProjectQuestion(), cancellation.Token])!;
            Check.True(await answerTask == null, "Cancelling the turn did not close question dialog");
            Console.WriteLine("Question UI: names, search, exact selection, multi-select, validation, RGB/hex, modal answers, cancellation, light/dark and Korean checked.");
        }
        finally { StudioStrings.Apply(language); }
    }

    private static QuestionWindow Window(Window owner, UserQuestion question) => new(question) { Owner = owner, ShowActivated = false,
        WindowStartupLocation = WindowStartupLocation.Manual, Left = -18000, Top = -18000 };
    private static T Find<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
    private static async Task Layout(Window window) { window.UpdateLayout(); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render); window.UpdateLayout(); }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T value) yield return value; foreach (var descendant in Descendants<T>(child)) yield return descendant; } }
}
