using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.Tests;

/// <summary>Offscreen UI fixtures only: no inference, MCP execution or settings persistence.</summary>
internal static class ApprovalActivityUiTests
{
    public static async Task Run(MainWindow window, Action<Window, string> capture)
    {
        var width = window.Width; var height = window.Height;
        try { await VerifyActivity(window, capture); await VerifyCamApproval(window, capture); await VerifyNativeCamApproval(window, capture); await VerifyFlatSketch(window, capture); await VerifyOperationPicker(window, capture); }
        finally { window.Width = width; window.Height = height; }
    }

    private static async Task VerifyActivity(MainWindow window, Action<Window, string> capture)
    {
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<CancellationToken, Task> work = async token => await finish.Task.WaitAsync(token);
        var running = (Task)Invoke(window, "RunOperation", work)!;
        await Layout(window);
        var panel = Control<Border>(window, "ActivityPanel");
        var progress = Control<ProgressBar>(window, "ActivityProgress");
        var label = Control<TextBlock>(window, "ActivityText");
        Check.True(panel.IsVisible && progress.IsIndeterminate, "Busy request needs visible animated feedback");
        Check.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(label), "Activity changes must be accessible");
        Invoke(window, "RecordTrace", "Model", "Request 1; fixture only");
        await Layout(window);
        Check.Equal(StudioStrings.Get("Activity.Thinking"), label.Text, "AI request did not enter thinking state");
        capture(window, "loading-thinking.png");
        Control<ComboBox>(window, "InterfaceLanguageBox").SelectedValue = "ko";
        await Layout(window);
        Check.Equal("생각하는 중…", label.Text, "A running activity label did not follow the interface language");
        Control<ComboBox>(window, "InterfaceLanguageBox").SelectedValue = "en";
        await Layout(window);
        Invoke(window, "RecordTrace", "Tool call", "topsolid_cam_get_operation_parameters {\"id\":11791}");
        await Layout(window);
        Check.Equal(StudioStrings.Get("Activity.Parameters"), label.Text, "Operation parameter work did not reach activity UI");
        Check.True(!label.Text.Contains("11791") && !label.Text.Contains("topsolid_"), "Activity exposed tool arguments");
        window.Width = 760; window.Height = 600;
        await Layout(window);
        var location = panel.TransformToAncestor(window).Transform(new Point());
        Check.True(location.X >= 0 && location.Y >= 0 && location.X + panel.ActualWidth <= window.ActualWidth &&
            location.Y + panel.ActualHeight <= window.ActualHeight, "Activity panel clips at minimum window size");
        capture(window, "loading-parameters-760x600.png");
        Invoke(window, "SetActivity", "Activity.Approval", "status", true);
        await Layout(window);
        Check.True(panel.IsVisible && !progress.IsIndeterminate && progress.Visibility == Visibility.Collapsed,
            "Approval wait must not imply AI computation is still progressing");
        capture(window, "loading-approval.png");
        Control<Button>(window, "CancelButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check.True(!Control<Button>(window, "CancelButton").IsEnabled, "Cancellation must disable duplicate cancellation");
        await running; await Layout(window);
        Check.True(panel.Visibility == Visibility.Collapsed && !progress.IsIndeterminate && Control<Button>(window, "SendButton").IsEnabled,
            "Cancelled request left stale loading feedback");
        Func<CancellationToken, Task> failing = _ => Task.FromException(new InvalidOperationException("Activity fixture failure"));
        await (Task)Invoke(window, "RunOperation", failing)!; await Layout(window);
        Check.True(panel.Visibility == Visibility.Collapsed && !progress.IsIndeterminate, "Failed request left stale loading feedback");
        Func<CancellationToken, Task> complete = _ => Task.CompletedTask;
        await (Task)Invoke(window, "RunOperation", complete)!; await Layout(window);
        Check.True(panel.Visibility == Visibility.Collapsed && Control<Button>(window, "SendButton").IsVisible,
            "Completed request left stale loading feedback");
        Invoke(window, "RecordTrace", "Timing", "Model request 1: 0.01 s");
    }

    private static async Task VerifyCamApproval(MainWindow owner, Action<Window, string> capture)
    {
        const string documentId = "19_zdjluv3akwnezg3eh7ehlclnce&15_0_8";
        var proposal = new JObject
        {
            ["toolName"] = "topsolid_cam_set_operation_parameters", ["confirmationToken"] = "fixture-private-token",
            ["target"] = new JObject { ["documentId"] = documentId, ["id"] = 11791, ["name"] = "Finish pocket" },
            ["effect"] = "Update cutting conditions in Finish pocket. The document remains unsaved.",
            ["arguments"] = new JObject
            {
                ["operation"] = new JObject { ["documentId"] = documentId, ["id"] = 11791 },
                ["changes"] = new JArray(new JObject
                {
                    ["parameterId"] = 7899, ["parameterName"] = "Feed per tooth", ["currentValue"] = 0.08,
                    ["requestedValue"] = 0.12, ["unit"] = "mm/tooth", ["possibleValues"] = new JArray(0.08, 0.10, 0.12)
                })
            }
        };
        var original = proposal.ToString();
        foreach (var dark in new[] { false, true })
        {
            TopSolidTheme.Apply(new(dark, dark ? "TopSolid Dark" : "TopSolid Classic", "Approval UI fixture"));
            var dialog = new ChangeConfirmationWindow(proposal) { Owner = owner, ShowInTaskbar = false, ShowActivated = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -18000, Top = -18000 };
            try
            {
                dialog.Show(); await Layout(dialog);
                var visible = string.Join("\n", Descendants<TextBlock>(dialog).Select(t => t.Text).Concat(Descendants<TextBox>(dialog).Select(t => t.Text)));
                foreach (var hidden in new[] { documentId, "11791", "7899", "fixture-private-token", "\"currentValue\"" })
                    Check.True(!visible.Contains(hidden), "User approval exposed identity/JSON: " + hidden);
                foreach (var required in new[] { "Finish pocket", "Feed per tooth", "0.08", "0.12", "mm/tooth", "unsaved", StudioStrings.Get("Approval.EntireChange") })
                    Check.True(visible.Contains(required), "User approval lost a material fact: " + required);
                Check.True(!Descendants<Border>(dialog).Any(b => b.CornerRadius.TopLeft == 8 && Descendants<Border>(b).Any(c => c.CornerRadius.TopLeft == 8)), "Approval must not nest bordered cards");
                var requestedValue = Descendants<TextBox>(dialog).Single(t => t.Text == "0.12");
                var valueBottom = requestedValue.TransformToAncestor(dialog).Transform(new Point(0, requestedValue.ActualHeight));
                var approvalTop = dialog.ApproveButton.TransformToAncestor(dialog).Transform(new Point());
                Check.True(valueBottom.Y <= approvalTop.Y, "Requested parameter value must be visible before the approval buttons");
                Check.True(!Descendants<CheckBox>(dialog).Any() && !Descendants<ListBox>(dialog).Any(), "Approval must not imply partial selection support");
                Check.True(dialog.RejectButton.IsDefault && !dialog.ApproveButton.IsDefault, "Enter must default to cancellation");
                Check.True(Descendants<TextBox>(dialog).All(t => t.IsReadOnly), "Approval facts must remain immutable");
                capture(dialog, dark ? "approval-cam-numeric-dark.png" : "approval-cam-numeric-light.png");
            }
            finally { dialog.Close(); }
        }
        Check.Equal(original, proposal.ToString(), "Friendly cards changed the executable approval proposal");
    }

    private static async Task VerifyNativeCamApproval(MainWindow owner, Action<Window, string> capture)
    {
        const string document = "19_zdjluv3akwnezg3eh7ehlclnce&15_0_8";
        var parameter = new JObject
        {
            ["parameter"] = new JObject { ["documentId"] = document, ["id"] = 11791, ["name"] = "RadialStock@Strategy|Stock" },
            ["name"] = "RadialStock@Strategy|Stock", ["displayName"] = "Radial stock", ["friendlyName"] = "Radial stock",
            ["fullName"] = "RadialStock@Strategy|Stock", ["simpleName"] = "RadialStock", ["localizedName"] = "Radial stock",
            ["valueType"] = "Real", ["unitType"] = "Length", ["unitSymbol"] = "mm", ["realValueSI"] = 0.0002,
            ["displayValue"] = "0.2 mm", ["invariantValue"] = "0.2 mm", ["categories"] = new JArray("Strategy", "Stock"),
            ["smartType"] = "Formula", ["formula"] = "Allowance / 2", ["source"] = JValue.CreateNull(),
            ["readOnly"] = false, ["editSupported"] = true, ["rangeConstraints"] = "Numeric limits are not exposed.",
            ["editGuidance"] = "Real values use SI and literal edits replace current formulas/references.",
            ["value"] = new JObject { ["Type"] = "Formula", ["UnitType"] = "Length", ["UnitSymbol"] = "mm", ["Value"] = 0.0002,
                ["Formula"] = "Allowance / 2", ["ElementId"] = new JObject { ["DocumentId"] = document, ["Id"] = 7899 } }
        };
        var proposal = new JObject
        {
            ["toolName"] = "topsolid_set_cam_parameter_value", ["confirmationToken"] = "fixture-private-token", ["expiresAt"] = "2026-09-18T07:02:00Z",
            ["description"] = "Change one parameter inside an existing CAM operation.", ["inputLengthUnits"] = "Not applicable",
            ["defaults"] = "Only the exact arguments shown are approved.",
            ["effect"] = "Change this scalar CAM parameter and update the document. Recalculation may be required. Does not generate NC code or save.",
            ["target"] = new JObject
            {
                ["documentId"] = document, ["name"] = "Pocket machining", ["type"] = "TopSolid.Cam.NC.MillTurn.Document", ["isDirty"] = true,
                ["affectedDocuments"] = new JArray(new JObject { ["documentId"] = document, ["name"] = "Pocket machining" },
                    new JObject { ["documentId"] = "19_related-document&15_0_9", ["name"] = "Linked machining setup" }),
                ["scopeNote"] = "All synchronized documents listed here are included in this confirmation.",
                ["operationName"] = "Finish pocket", ["parameterName"] = "Radial stock", ["parameter"] = parameter,
                ["operationType"] = "TopSolid.Cam.NC.MillTurn.DB.EndMilling.EndMillingOperation",
                ["currentValue"] = "0.2 mm", ["valueType"] = "Real", ["unitType"] = "Length", ["replacesDefinition"] = true,
                ["proposedValue"] = new JObject { ["Type"] = "Basic", ["UnitType"] = "Length", ["UnitSymbol"] = null, ["Value"] = 0.0003,
                    ["Formula"] = null, ["ElementId"] = new JObject { ["DocumentId"] = document, ["Id"] = 7899 } }
            },
            ["arguments"] = new JObject { ["documentId"] = document, ["element"] = new JObject { ["documentId"] = document, ["id"] = 11791 },
                ["name"] = "RadialStock@Strategy|Stock", ["valueType"] = "Real", ["unitType"] = "Length", ["realValueSI"] = 0.0003 }
        };
        var original = proposal.ToString();
        foreach (var dark in new[] { false, true })
        {
            TopSolidTheme.Apply(new(dark, dark ? "TopSolid Dark" : "TopSolid Classic", "Native CAM approval fixture"));
            var dialog = new ChangeConfirmationWindow(proposal) { Owner = owner, ShowInTaskbar = false, ShowActivated = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -18000, Top = -18000 };
            try
            {
                dialog.Show(); await Layout(dialog);
                var expander = Descendants<Expander>(dialog).Single();
                Check.True(!expander.IsExpanded && expander.Content == null, "Detailed native metadata must not bury initial CAM values");
                var initial = VisibleText(dialog);
                Check.True(Descendants<Image>(dialog).Any(i => ReferenceEquals(i.Source, TopSolidIcons.Get(TopSolidIcons.OperationKey(proposal["target"])))), "CAM approval header lost the native operation icon");
                Check.True(Descendants<Image>(dialog).Any(i => ReferenceEquals(i.Source, TopSolidIcons.Get("cam-strategy"))), "CAM parameter approval lost its native category icon");
                Check.True(!initial.Contains("TopSolid.Cam.NC."), "Native type metadata should choose icons without entering user-facing facts");
                Check.True(Descendants<TextBlock>(dialog).Any(t => t.Text == "Pocket machining"),
                    "The parameter name replaced its containing document in the approval header");
                foreach (var required in new[] { "Finish pocket", "Radial stock", "0.2 mm", "0.0003 m (SI)", "fixed value", "Linked machining setup" })
                    Check.True(initial.Contains(required), "Native CAM summary omitted a material fact: " + required);
                Check.True(!initial.Contains("0.0003 mm"), "Proposed SI value was mislabeled using the current native display unit");
                var newValue = Descendants<TextBox>(dialog).Single(t => t.Text == "0.0003 m (SI)");
                var valueBottom = newValue.TransformToAncestor(dialog).Transform(new Point(0, newValue.ActualHeight));
                Check.True(valueBottom.Y < dialog.ApproveButton.TransformToAncestor(dialog).Transform(new Point()).Y,
                    "Native parameter value is below the initial approval viewport");
                capture(dialog, dark ? "approval-cam-native-dark.png" : "approval-cam-native-light.png");
                expander.IsExpanded = true; await Layout(dialog);
                Check.True(VisibleText(dialog).Contains("Allowance / 2") && Descendants<TextBox>(dialog).All(t => t.IsReadOnly),
                    "Expanded friendly detail lost the native formula or became editable");
                foreach (var hidden in new[] { document, "11791", "7899", "fixture-private-token" })
                    Check.True(!VisibleText(dialog).Contains(hidden), "Native CAM detail exposed an internal identifier: " + hidden);
            }
            finally { dialog.Close(); }
        }
        Check.Equal(original, proposal.ToString(), "Native CAM summary changed the executable proposal");

        foreach (var sample in new[]
        {
            (Unit: "Velocity", Name: "Cutting speed", Native: "120 m/min", CurrentSi: 2.0, Si: 2.5, Expected: "2.5 m/s (SI)", Image: "approval-cam-cutting-speed.png"),
            (Unit: "ToothFeedRate", Name: "Feed per tooth", Native: "0.12 mm/tooth", CurrentSi: 0.00012, Si: 0.00015, Expected: "0.00015 m/tooth (SI)", Image: "approval-cam-tooth-feed.png")
        })
        {
            var variant = (JObject)proposal.DeepClone();
            var target = (JObject)variant["target"]!;
            target["parameterName"] = sample.Name; target["currentValue"] = sample.Native; target["unitType"] = sample.Unit;
            target["replacesDefinition"] = false;
            target["proposedValue"]!["UnitType"] = sample.Unit; target["proposedValue"]!["Value"] = sample.Si;
            var metadata = (JObject)target["parameter"]!;
            metadata["displayName"] = sample.Name; metadata["unitType"] = sample.Unit; metadata["displayValue"] = sample.Native;
            var exactName = sample.Unit == "Velocity" ? "CuttingSpeed@CuttingConditions" : "ToothFeed@CuttingConditions";
            metadata["name"] = metadata["fullName"] = exactName;
            metadata["simpleName"] = exactName.Split('@')[0]; metadata["localizedName"] = metadata["friendlyName"] = sample.Name;
            metadata["parameter"]!["name"] = exactName; metadata["categories"] = new JArray("CuttingConditions");
            metadata["smartType"] = "Basic"; metadata["formula"] = null; metadata["realValueSI"] = sample.CurrentSi;
            metadata["unitSymbol"] = sample.Unit == "Velocity" ? "m/min" : "mm/tooth";
            metadata["invariantValue"] = sample.Native;
            metadata["value"] = target["proposedValue"]!.DeepClone(); metadata["value"]!["Value"] = sample.CurrentSi;
            metadata["value"]!["UnitSymbol"] = metadata["unitSymbol"]!.DeepClone();
            variant["arguments"]!["name"] = exactName; variant["arguments"]!["unitType"] = sample.Unit; variant["arguments"]!["realValueSI"] = sample.Si;
            var dialog = new ChangeConfirmationWindow(variant) { Owner = owner, ShowInTaskbar = false, ShowActivated = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -18000, Top = -18000 };
            try
            {
                dialog.Show(); await Layout(dialog);
                var visible = VisibleText(dialog);
                Check.True(visible.Contains(sample.Name) && visible.Contains(sample.Native) && visible.Contains(sample.Expected),
                    "Scalar cutting-condition approval must preserve native current units and explicit proposed SI units");
                Check.True(!visible.Contains(StudioStrings.Get("Approval.DefinitionChange")), "Basic scalar edit incorrectly claims a formula/reference replacement");
                capture(dialog, sample.Image);
            }
            finally { dialog.Close(); }
        }

        // The same native proposal wrapper must preserve scalar semantics for text, boolean and native enums.
        foreach (var sample in new[]
        {
            (Kind: "Boolean", Scalar: (JToken)new JValue(false), Expected: StudioStrings.Get("Response.No")),
            (Kind: "Text", Scalar: (JToken)new JValue(""), Expected: StudioStrings.Get("Approval.EmptyText")),
            (Kind: "Integer", Scalar: (JToken)new JValue(2), Expected: "Climb milling")
        })
        {
            var variant = (JObject)proposal.DeepClone();
            variant["target"]!["valueType"] = sample.Kind;
            variant["target"]!["proposedValue"]!["Value"] = sample.Scalar.DeepClone();
            variant["target"]!["parameter"]!["allowedValues"] = new JArray(new JObject { ["value"] = 2, ["label"] = "Climb milling" });
            var dialog = new ChangeConfirmationWindow(variant) { Owner = owner, ShowInTaskbar = false, ShowActivated = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -18000, Top = -18000 };
            try { dialog.Show(); await Layout(dialog); Check.True(VisibleText(dialog).Contains(sample.Expected), "Native scalar summary lost " + sample.Kind + " semantics"); }
            finally { dialog.Close(); }
        }
    }

    private static async Task VerifyFlatSketch(MainWindow owner, Action<Window, string> capture)
    {
        var language = StudioStrings.CurrentLanguage;
        var proposal = JObject.Parse("""
        {"toolName":"topsolid_create_sketch_profiles","confirmationToken":"private-sketch-token","inputLengthUnits":"mm",
         "target":{"documentId":"flat-sketch-document","name":"스케치 검토 부품","sketchPlan":{
           "placement":{"origin":{"x":0,"y":0,"z":0},"normal":{"x":0,"y":0,"z":1}},
           "profiles":[{"kind":"circle","center":{"x":0,"y":0},"radius":20}],"createsSections":false}},
         "arguments":{"documentId":"flat-sketch-document","origin":{"x":0,"y":0,"z":0},"units":"mm",
           "profiles":[{"kind":"circle","center":{"x":0,"y":0},"radius":20},{"kind":"rectangle","width":60,"height":30}]},
         "effect":"Create the reviewed profiles in one sketch. The document remains unsaved.",
         "defaults":"Exact prepared geometry and placement are preserved."}
        """);
        var original = proposal.ToString();
        try
        {
            StudioStrings.Apply("ko");
            foreach (var dark in new[] { false, true })
            {
                TopSolidTheme.Apply(new(dark, dark ? "TopSolid Dark" : "TopSolid Classic", "Flat approval fixture"));
                var dialog = new ChangeConfirmationWindow(proposal) { Owner = owner, ShowInTaskbar = false, ShowActivated = false,
                    WindowStartupLocation = WindowStartupLocation.Manual, Left = -18000, Top = -18000 };
                try
                {
                    dialog.Show(); await Layout(dialog);
                    var initial = VisibleText(dialog);
                    foreach (var value in new[] { "스케치 검토 부품", "20", "60", "30", "mm", "X 0", "Y 0", "Z 0" })
                        Check.True(initial.Contains(value), "Flat sketch approval omitted " + value);
                    Check.True(!Descendants<TabControl>(dialog).Any(), "User review should not add a tab frame around a single page");
                    Check.True(Descendants<TextBox>(dialog).All(t => t.IsReadOnly), "Flat approval facts became editable");
                    var expander = Descendants<Expander>(dialog).Single();
                    Check.True(expander.Content == null, "Prepared details must load only when opened");
                    Check.True(!initial.Contains("flat-sketch-document") && !initial.Contains("private-sketch-token"), "Flat approval exposed private identities");
                    capture(dialog, dark ? "approval-sketch-flat-dark.png" : "approval-sketch-flat-light.png");
                    expander.IsExpanded = true; await Layout(dialog);
                    Check.True(VisibleText(dialog).Contains("Exact prepared geometry"), "Flat disclosure lost original prepared facts");
                    Check.True(!Descendants<Border>((DependencyObject)expander.Content!).Any(b => b.CornerRadius.TopLeft > 0 && b.Child is StackPanel), "Detailed sketch facts nested another card");
                    capture(dialog, dark ? "approval-sketch-details-dark.png" : "approval-sketch-details-light.png");
                    dialog.Width = 480; dialog.Height = 500; expander.IsExpanded = false; await Layout(dialog);
                    var button = dialog.ApproveButton.TransformToAncestor(dialog).Transform(new Point());
                    Check.True(button.X >= 0 && button.X + dialog.ApproveButton.ActualWidth <= dialog.ActualWidth && button.Y < dialog.ActualHeight, "Compact review hides the approval action");
                }
                finally { dialog.Close(); }
            }
        }
        finally { StudioStrings.Apply(language); }
        Check.Equal(original, proposal.ToString(), "Flattening changed executable sketch facts");
    }

    private static async Task VerifyOperationPicker(MainWindow owner, Action<Window, string> capture)
    {
        var language = StudioStrings.CurrentLanguage; StudioStrings.Apply("ko");
        var question = UserQuestionTests.CamQuestion();
        var dialog = new QuestionWindow(question) { Owner = owner, ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -18000, Top = -18000 };
        try
        {
            dialog.Show(); await Layout(dialog);
            Check.True(VisibleText(dialog).Contains("[2: 볼 동시가공") && VisibleText(dialog).Contains("[3: 동시가공"), "Actual CAM names are missing from rendered cards");
            var icons = Descendants<Image>(dialog).Select(i => i.Source).ToArray();
            Check.True(icons.Contains(TopSolidIcons.Get(question.Choices[0].IconKey!)) && icons.Contains(TopSolidIcons.Get(question.Choices[2].IconKey!)), "Operation cards did not bind their native type icons");
            // Check every packaged class mapping can load an actual image, not the
            // generic missing-resource fallback; shared icons may have multiple types.
            using var stream = typeof(TopSolidIcons).Assembly.GetManifestResourceStream("TopSolid.Automation.AI.Studio.Assets.TopSolid.provenance.json")!;
            using var reader = new StreamReader(stream);
            foreach (var icon in JArray.Parse(reader.ReadToEnd()).OfType<JObject>())
                Check.True(TopSolidIcons.Get((string)icon["Key"]!) is System.Windows.Media.Imaging.BitmapSource, "Missing packaged TopSolid icon: " + icon["Key"]);
            capture(dialog, "question-cam-native-names-icons.png");
        }
        finally { dialog.Close(); StudioStrings.Apply(language); }
    }

    private static string VisibleText(DependencyObject root) => string.Join("\n", Descendants<TextBlock>(root).Select(t => t.Text).Concat(Descendants<TextBox>(root).Select(t => t.Text)));

    private static object? Invoke(object target, string name, params object?[] arguments) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, arguments);
    private static T Control<T>(Window window, string name) where T : FrameworkElement => (T)window.FindName(name);
    private static async Task Layout(Window window)
    {
        window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typed) yield return typed;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
