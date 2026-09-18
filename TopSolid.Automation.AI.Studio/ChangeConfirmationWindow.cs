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

        var layout = new DockPanel { Margin = new Thickness(20) };
        var footer = new DockPanel { Margin = new Thickness(0, 16, 0, 0) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = StudioStrings.Get("Common.Cancel"), IsCancel = true, IsDefault = true, MinWidth = 100, Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 8, 0) };
        var approve = new Button { Content = StudioStrings.Get("Common.Approve"), MinWidth = 110, Padding = new Thickness(14, 7, 14, 7) };
        RejectButton = cancel;
        ApproveButton = approve;
        approve.SetResourceReference(BackgroundProperty, "AccentBrush");
        approve.SetResourceReference(ForegroundProperty, "AccentTextBrush");
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
        DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
        header.Children.Add(new Image { Source = TopSolidIcons.ForTool(tool), Width = 36, Height = 36,
            Margin = new Thickness(0, 2, 14, 0), VerticalAlignment = VerticalAlignment.Top });
        var title = new StackPanel();
        title.Children.Add(Text(StudioStrings.CurrentLanguage == "en" ? FriendlyName(tool) : StudioStrings.Get("Approval.Title"), 20, FontWeights.SemiBold));
        var target = preview["target"] as JObject;
        title.Children.Add(Text(TargetName(target), 14, FontWeights.Normal, new Thickness(0, 4, 0, 0)));
        header.Children.Add(title);
        DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header);

        var tabs = new TabControl();
        var summary = new StackPanel { Margin = new Thickness(8) };
        if (!developerMode && tool == "topsolid_set_cam_parameter_value" && target != null && CamSummary(target) is { } camSummary)
        {
            summary.Children.Add(Card(StudioStrings.Get("Approval.Changes"), "parameter", Properties(camSummary, localizeLabels: true, friendly: true)));
            if ((bool?)target["replacesDefinition"] == true)
                summary.Children.Add(Card(StudioStrings.Get("Approval.DefinitionChange"), "status", Text(StudioStrings.Get("Approval.ReplacesDefinition"), 13, FontWeights.SemiBold)));
            if (!string.IsNullOrWhiteSpace((string?)preview["effect"]))
                summary.Children.Add(Text((string)preview["effect"]!, 12, FontWeights.Normal, new Thickness(4, 0, 4, 8)));
            if (target["affectedDocuments"] is JArray affected && affected.Count > 1)
                summary.Children.Add(Card(StudioStrings.Get("Approval.AffectedDocuments"), "document", Text(string.Join(", ", affected.OfType<JObject>().Select(TargetName)), 13, FontWeights.Normal)));
            var details = new Expander { Header = StudioStrings.Get("Approval.AllDetails"), IsExpanded = false, Margin = new Thickness(0, 4, 0, 0) };
            details.Expanded += (_, _) => details.Content ??= Properties(preview, friendly: true);
            summary.Children.Add(details);
        }
        else
        {
        if (!string.IsNullOrWhiteSpace((string?)preview["effect"]))
            summary.Children.Add(Card(StudioStrings.Get("Approval.Effect"), "status", Text((string)preview["effect"]!, 14, FontWeights.Normal)));
        if (target != null && (developerMode || !IsRepeatedTargetName(target)))
            summary.Children.Add(Card(StudioStrings.Get("Approval.Target"), tool, Properties(target, friendly: !developerMode)));
        var facts = new JObject();
        if (developerMode) facts["Tool"] = tool;
        if (preview["inputLengthUnits"] != null) facts["Length units"] = preview["inputLengthUnits"]!.DeepClone();
        if (preview["defaults"] != null) facts["Defaults"] = preview["defaults"]!.DeepClone();
        if (!developerMode)
            foreach (var property in preview.Properties().Where(p => p.Name is not ("target" or "effect" or "inputLengthUnits" or "defaults" or "arguments")))
                facts[property.Name] = property.Value.DeepClone();
        if (preview["arguments"] is JObject arguments)
            summary.Children.Add(Card(StudioStrings.Get(developerMode ? "Approval.Arguments" : "Approval.Changes"), "parameter", Properties(arguments, friendly: !developerMode)));
        if (facts.HasValues)
            summary.Children.Add(Card(StudioStrings.Get("Approval.Details"), "status", Properties(facts, localizeLabels: true, friendly: !developerMode)));
        }
        tabs.Items.Add(new TabItem { Header = StudioStrings.Get("Approval.Review"), Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = summary } });
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
            layout.Children.Add(PreviewLayout.Wrap(this, tabs, graphic));
        }
        else layout.Children.Add(tabs);
        Content = layout;
        Loaded += (_, _) => cancel.Focus();
    }

    private static string TargetName(JObject? target) => (string?)target?["name"] ?? (string?)target?["documentName"] ??
        (string?)target?["projectName"] ?? (string?)target?["ownerName"] ?? StudioStrings.Get("Approval.AffectedObjects");

    private static bool IsRepeatedTargetName(JObject target) => target.Properties().Count() == 1 &&
        target.Properties().First().Name is "name" or "documentName" or "projectName" or "ownerName";

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

    private static Border Card(string heading, string icon, FrameworkElement content)
    {
        var panel = new StackPanel();
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };
        var image = new Image { Source = icon.StartsWith("topsolid_", StringComparison.Ordinal) ? TopSolidIcons.ForTool(icon) : TopSolidIcons.Get(icon),
            Width = 22, Height = 22, Margin = new Thickness(0, 0, 8, 0) };
        DockPanel.SetDock(image, Dock.Left); header.Children.Add(image);
        var title = Text(heading, 14, FontWeights.SemiBold); title.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(title);
        panel.Children.Add(header); panel.Children.Add(content);
        var card = new Border { Child = panel, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
            Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8) };
        card.SetResourceReference(BackgroundProperty, "SurfaceBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        AutomationProperties.SetName(card, heading);
        return card;
    }

    private static TextBlock Text(string value, double size, FontWeight weight, Thickness? margin = null)
    {
        var text = new TextBlock { Text = value, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap,
            Margin = margin ?? new Thickness(0) };
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        return text;
    }

    private static FrameworkElement Properties(JObject values, bool localizeLabels = false, bool friendly = false)
    {
        var rows = new StackPanel();
        foreach (var property in values.Properties())
        {
            var labelText = friendly ? FriendlyResponsePresenter.FriendlyLabel(property.Name) : localizeLabels ? StudioStrings.Text(property.Name) : property.Name;
            if (friendly && property.Name.Equals("changes", StringComparison.OrdinalIgnoreCase) && property.Value is JArray changes && changes.All(t => t is JObject))
            {
                rows.Children.Add(FriendlyValue(changes));
                continue;
            }
            if (friendly && (property.Value is JObject || property.Value is JArray nested && nested.Any(t => t is JObject or JArray)))
            {
                rows.Children.Add(Card(labelText, FactIcon(property.Name), FriendlyValue(property.Value)));
                continue;
            }
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 155 });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            var label = Text(labelText, 12, FontWeights.Normal, new Thickness(0, 5, 10, 5));
            label.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
            row.Children.Add(label);
            var value = friendly ? FriendlyScalar(property.Value) : property.Value.Type == JTokenType.String
                ? (string)property.Value! : property.Value.ToString(Formatting.Indented);
            if (!friendly && value.Length > 4000) value = value[..4000] + "\n" + StudioStrings.Get("Approval.FullValue");
            var content = new TextBox { Text = value, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 1, 0, 1), FontSize = 12 };
            content.SetResourceReference(BackgroundProperty, "SurfaceBrush");
            content.SetResourceReference(ForegroundProperty, "TextBrush");
            AutomationProperties.SetName(content, labelText);
            Grid.SetColumn(content, 1); row.Children.Add(content);
            rows.Children.Add(row);
        }
        return rows;
    }

    private static string FriendlyScalar(JToken value)
    {
        if (value is not JArray choices) return FriendlyResponsePresenter.Render(value);
        return choices.HasValues ? string.Join(", ", choices.Select(choice => FriendlyResponsePresenter.Render(choice))) : StudioStrings.Get("Approval.NoItems");
    }

    private static FrameworkElement FriendlyValue(JToken value)
    {
        if (value is JObject obj) return Properties(obj, friendly: true);
        if (value is JArray array)
        {
            var list = new StackPanel();
            foreach (var item in array)
            {
                if (item is JObject entry)
                {
                    var nameField = new[] { "displayName", "parameterName", "operationName", "name" }.FirstOrDefault(key => entry[key]?.Type == JTokenType.String);
                    var name = nameField == null ? StudioStrings.Get("Approval.ItemNumber", list.Children.Count + 1) : (string)entry[nameField]!;
                    var details = (JObject)entry.DeepClone();
                    if (nameField != null) details.Remove(nameField);
                    list.Children.Add(Card(name, entry["parameterName"] != null ? "parameter" : "operation", Properties(details, friendly: true)));
                }
                else list.Children.Add(FriendlyValue(item));
            }
            if (!array.HasValues) list.Children.Add(Text(StudioStrings.Get("Approval.NoItems"), 12, FontWeights.Normal));
            return list;
        }
        return Text(FriendlyResponsePresenter.Render(value), 13, FontWeights.Normal, new Thickness(0, 3, 0, 3));
    }

    private static string FactIcon(string name)
    {
        if (name.Contains("parameter", StringComparison.OrdinalIgnoreCase) || name.Contains("value", StringComparison.OrdinalIgnoreCase)) return "parameter";
        if (name.Contains("operation", StringComparison.OrdinalIgnoreCase) || name.Contains("change", StringComparison.OrdinalIgnoreCase)) return "operation";
        if (name.Contains("document", StringComparison.OrdinalIgnoreCase)) return "document";
        if (name.Contains("part", StringComparison.OrdinalIgnoreCase)) return "part";
        return "status";
    }

}
