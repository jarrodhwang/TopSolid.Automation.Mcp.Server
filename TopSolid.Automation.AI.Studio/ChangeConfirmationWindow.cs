using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Preview;

namespace TopSolid.Automation.AI.Studio;

/// <summary>Shows immutable server facts. Model-generated prose never becomes approval authority.</summary>
public sealed class ChangeConfirmationWindow : Window
{
    public TextBox ProposalBox { get; }
    public Button ApproveButton { get; }
    public Button RejectButton { get; }

    public ChangeConfirmationWindow(JObject proposal, bool developerMode = false, FriendlyResponsePresenter? presenter = null, IGraphicPreviewClient? previewClient = null)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var preview = (JObject)proposal.DeepClone();
        preview.Remove("confirmationToken");
        var tool = (string?)preview["toolName"] ?? "TopSolid change";
        if (!developerMode)
        {
            presenter ??= new FriendlyResponsePresenter();
            presenter.Observe(preview);
            preview = presenter.Review(preview);
        }
        StudioStrings.InitializeResources(this);
        Title = StudioStrings.Get("Approval.Title");
        Width = 700; Height = 660; MinWidth = 480; MinHeight = 390;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Icon = TopSolidIcons.Get("app");
        SetResourceReference(BackgroundProperty, "WindowBrush");
        SetResourceReference(ForegroundProperty, "TextBrush");
        TopSolidTheme.ApplyWindow(this);

        var layout = new DockPanel();
        var footer = new DockPanel();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = StudioStrings.Get("Common.Cancel"), IsCancel = true, IsDefault = true, MinWidth = 100, Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 8, 0) };
        var approve = new Button { Content = StudioStrings.Get("Common.Approve"), MinWidth = 110, Padding = new Thickness(14, 7, 14, 7) };
        RejectButton = cancel;
        ApproveButton = approve;
        DialogLayout.Command(cancel, "cancel"); DialogLayout.Command(approve, "approve");
        cancel.Click += (_, _) => DialogResult = false;
        approve.Click += (_, _) => DialogResult = true;
        actions.Children.Add(cancel); actions.Children.Add(approve);
        DockPanel.SetDock(actions, Dock.Right); footer.Children.Add(actions);
        if (!developerMode)
        {
            var scope = Text(StudioStrings.Get("Approval.EntireChange"), 12, FontWeights.Normal, new Thickness(0, 0, 12, 0));
            scope.VerticalAlignment = VerticalAlignment.Center;
            scope.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
            footer.Children.Add(scope);
        }
        var footerBar = DialogLayout.Footer(footer);
        DockPanel.SetDock(footerBar, Dock.Bottom); layout.Children.Add(footerBar);

        var header = new DockPanel();
        var headerIcon = tool.Contains("cam", StringComparison.OrdinalIgnoreCase) ? TopSolidIcons.Get(TopSolidIcons.OperationKey(proposal["target"])) : TopSolidIcons.ForTool(tool);
        header.Children.Add(new Image { Source = headerIcon, Width = 28, Height = 28,
            Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
        var title = new StackPanel();
        title.Children.Add(Text(StudioStrings.CurrentLanguage == "en" ? FriendlyName(tool) : StudioStrings.Get("Approval.Title"), 16, FontWeights.SemiBold));
        var target = preview["target"] as JObject;
        title.Children.Add(Text(TargetName(target), 14, FontWeights.Normal, new Thickness(0, 4, 0, 0)));
        header.Children.Add(title);
        var headerBar = DialogLayout.Toolbar(header);
        DockPanel.SetDock(headerBar, Dock.Top); layout.Children.Add(headerBar);

        var tabs = new TabControl();
        var summary = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        if (!developerMode && tool == "topsolid_set_cam_parameter_value" && target != null && CamSummary(target) is { } camSummary)
        {
            summary.Children.Add(Section(StudioStrings.Get("Approval.Changes"), TopSolidIcons.CamCategoryKey(proposal["target"]?["parameter"]), Properties(camSummary, localizeLabels: true, friendly: true)));
            if ((bool?)target["replacesDefinition"] == true)
                summary.Children.Add(Section(StudioStrings.Get("Approval.DefinitionChange"), "warning", Text(StudioStrings.Get("Approval.ReplacesDefinition"), 13, FontWeights.SemiBold)));
            if (!string.IsNullOrWhiteSpace((string?)preview["effect"]))
                summary.Children.Add(Text((string)preview["effect"]!, 12, FontWeights.Normal, new Thickness(4, 0, 4, 8)));
            if (target["affectedDocuments"] is JArray affected && affected.Count > 1)
                summary.Children.Add(Section(StudioStrings.Get("Approval.AffectedDocuments"), "document", Text(string.Join(", ", affected.OfType<JObject>().Select(TargetName)), 13, FontWeights.Normal)));
        }
        else
        {
        if (preview["arguments"] is JObject arguments)
        {
            var changes = (JObject)arguments.DeepClone();
            if (!developerMode && target != null)
                foreach (var property in changes.Properties().Where(p => p.Name == FriendlyResponsePresenter.FriendlyLabel("document") &&
                    p.Value.Type == JTokenType.String && (string?)p.Value == TargetName(target)).ToArray()) property.Remove();
            if (changes["units"] == null && preview["inputLengthUnits"] != null) changes["inputLengthUnits"] = preview["inputLengthUnits"]!.DeepClone();
            summary.Children.Add(Section(StudioStrings.Get(developerMode ? "Approval.Arguments" : "Approval.Changes"), "arguments", Properties(changes, friendly: !developerMode)));
        }
        var highlights = PreparedHighlights(target, preview["arguments"] as JObject);
        if (highlights.HasValues)
            summary.Children.Add(Section(StudioStrings.Get("Approval.Target"), tool, Properties(highlights, friendly: !developerMode)));
        if (!string.IsNullOrWhiteSpace((string?)preview["effect"]))
            summary.Children.Add(Text((string)preview["effect"]!, 12, FontWeights.Normal, new Thickness(0, 6, 0, 12)));
        }
        var details = new Expander { Header = StudioStrings.Get("Approval.AllDetails"), IsExpanded = false, Margin = new Thickness(0, 8, 0, 0) };
        details.Expanded += (_, _) => details.Content ??= Properties(preview, friendly: !developerMode);
        summary.Children.Add(details);
        var review = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = summary };
        FrameworkElement reviewContent = review;
        if (developerMode)
        {
            tabs.Items.Add(new TabItem { Header = StudioStrings.Get("Approval.Review"), Content = review });
            reviewContent = tabs;
        }
        var raw = new TextBox { Text = preview.ToString(Formatting.Indented), IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), FontSize = 12,
            Padding = new Thickness(12), BorderThickness = new Thickness(0) };
        raw.SetResourceReference(BackgroundProperty, "CodeBrush");
        ProposalBox = raw;
        raw.SetResourceReference(ForegroundProperty, "TextBrush");
        if (developerMode) tabs.Items.Add(new TabItem { Header = StudioStrings.Get("Approval.Json"), Content = raw });
        var graphicTarget = PreviewTarget.FromProposal(proposal);
        if (ProposalGeometry.Supports(proposal) || previewClient != null && graphicTarget != null)
        {
            var graphic = new GraphicPreviewPane(previewClient, graphicTarget, proposal);
            layout.Children.Add(DialogLayout.Body(PreviewLayout.Wrap(this, reviewContent, graphic)));
        }
        else layout.Children.Add(DialogLayout.Body(reviewContent));
        Content = layout;
        Loaded += (_, _) => cancel.Focus();
    }

    private static string TargetName(JObject? target) => (string?)target?["name"] ?? (string?)target?["documentName"] ??
        (string?)target?["projectName"] ?? (string?)target?["ownerName"] ?? StudioStrings.Get("Approval.AffectedObjects");

    private static JObject PreparedHighlights(JObject? target, JObject? arguments)
    {
        var facts = new JObject();
        if (target == null) return facts;
        // Material scope, replacement and approximation facts remain above the
        // disclosure. Duplicate schema/placement metadata stays in the full review.
        foreach (var key in new[] { "affectedDocuments", "scopeNote", "replacesDefinition", "replaceShapes", "warnings", "warning", "approximations", "currentValue" })
            if (target[key] is { Type: not JTokenType.Null } value && arguments?[key] == null &&
                (value is not JArray array || array.Count > (key == "affectedDocuments" ? 1 : 0))) facts[key] = value.DeepClone();
        foreach (var key in new[] { "height", "length", "angle" })
            if (arguments?[key + "Parameter"] != null && target[key] != null) facts[key] = target[key]!.DeepClone();
        if (target["sketchPlan"]?["approximations"] is JArray { Count: > 0 } approximations) facts["approximations"] = approximations.DeepClone();
        return facts;
    }

    // Prepared native CAM facts, never model prose or requested arguments, define the key-value summary.
    // The native display symbol belongs to the current value. A proposed SmartReal.Value is always SI.
    private static JObject? CamSummary(JObject target)
    {
        if (target["proposedValue"] is not JObject proposed ||
            !proposed.TryGetValue("Value", StringComparison.OrdinalIgnoreCase, out var scalar) || scalar is not JValue ||
            target["parameterName"]?.Type != JTokenType.String) return null;
        var type = (string?)target["valueType"];
        if (type is not ("Real" or "Integer" or "Boolean" or "Text")) return null;
        var value = FriendlyResponsePresenter.Render(scalar);
        if (type == "Real")
        {
            var unit = (string?)target["unitType"];
            if (string.IsNullOrWhiteSpace(unit)) return null;
            value = StudioStrings.Get("Approval.SiValue", value, SiUnitLabel(unit));
        }
        else if (type == "Text" && value.Length == 0) value = StudioStrings.Get("Approval.EmptyText");
        else if (type == "Integer" && target["parameter"]?["allowedValues"] is JArray choices)
        {
            var choice = choices.OfType<JObject>().FirstOrDefault(row => JToken.DeepEquals(row["value"], scalar));
            if (choice?["label"]?.Type == JTokenType.String) value = (string)choice["label"]!;
        }
        return new JObject
        {
            [StudioStrings.Get("Approval.Operation")] = target["operationName"]?.DeepClone() ?? new JValue(StudioStrings.Get("Response.UnnamedItem")),
            [StudioStrings.Get("Approval.Parameter")] = target["parameterName"]!.DeepClone(),
            [StudioStrings.Get("Approval.CurrentValue")] = target["currentValue"] is { Type: not JTokenType.Null } current
                ? current.DeepClone() : new JValue(StudioStrings.Get("Approval.ValueUnavailable")),
            [StudioStrings.Get("Approval.NewValue")] = value
        };
    }

    private static string SiUnitLabel(string unit) => unit switch
    {
        "Length" => "m", "Angle" => "rad", "Time" => "s", "Mass" => "kg", "Velocity" => "m/s", "AngularVelocity" => "rad/s",
        "AngularFeedRate" => "m/rad", "ToothFeedRate" => "m/tooth", "Pressure" => "Pa", "Factor" => StudioStrings.Get("Approval.Dimensionless"),
        _ => FriendlyResponsePresenter.FriendlyLabel(unit)
    };

    private static string FriendlyName(string tool)
    {
        var words = tool.StartsWith("topsolid_", StringComparison.Ordinal) ? tool[9..] : tool;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words.Replace('_', ' '));
    }

    private static FrameworkElement Section(string heading, string icon, FrameworkElement content)
    {
        var panel = new StackPanel();
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };
        var image = new Image { Source = icon.StartsWith("topsolid_", StringComparison.Ordinal) ? TopSolidIcons.ForTool(icon) : TopSolidIcons.Get(icon),
            Width = 22, Height = 22, Margin = new Thickness(0, 0, 8, 0) };
        DockPanel.SetDock(image, Dock.Left); header.Children.Add(image);
        var title = Text(heading, 14, FontWeights.SemiBold); title.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(title);
        panel.Children.Add(header); panel.Children.Add(content);
        panel.Margin = new Thickness(0, 0, 0, 14);
        AutomationProperties.SetName(panel, heading);
        return panel;
    }

    private static TextBlock Text(string value, double size, FontWeight weight, Thickness? margin = null)
    {
        var text = new TextBlock { Text = value, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap,
            Margin = margin ?? new Thickness(0) };
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        return text;
    }

    private static FrameworkElement Properties(JObject values, bool localizeLabels = false, bool friendly = false)
        => ApprovalFactsView.Create(values, friendly || localizeLabels);

}
