using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio.Appearance;

/// <summary>Read-only facts in one flat list. Nested data never creates nested cards.</summary>
internal static class ApprovalFactsView
{
    internal sealed record Fact(string Label, string Value, bool Heading = false);

    internal static IEnumerable<Fact> Flatten(JToken value, string path = "", bool friendly = true)
    {
        string Label(string key) => friendly ? FriendlyResponsePresenter.FriendlyLabel(key) : key;
        string At(string key) => path.Length == 0 ? key : path + " / " + key;
        if (value is JObject obj)
        {
            var properties = obj.Properties().ToArray();
            // Coordinates and directions are a single scan-friendly row, with all
            // components and signs intact. This does not convert or infer units.
            if (properties.Length is 2 or 3 && properties.All(p => p.Name is "x" or "y" or "z") &&
                properties.All(p => p.Value.Type is JTokenType.Integer or JTokenType.Float))
            {
                yield return new(path, string.Join("   ·   ", properties.OrderBy(p => p.Name).Select(p => p.Name.ToUpperInvariant() + " " + FriendlyResponsePresenter.Render(p.Value))));
                yield break;
            }
            if (properties.Length == 0) yield return new(path, StudioStrings.Get("Approval.NoItems"));
            foreach (var property in properties)
                foreach (var fact in Flatten(property.Value, At(Label(property.Name)), friendly)) yield return fact;
        }
        else if (value is JArray array && array.Any(t => t is JContainer))
        {
            for (var index = 0; index < array.Count; index++)
            {
                var item = array[index];
                if (item is JObject entry)
                {
                    var nameKey = new[] { "displayName", "parameterName", "operationName", "name" }
                        .FirstOrDefault(key => entry[key]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string?)entry[key]));
                    var name = nameKey == null ? StudioStrings.Get("Approval.ItemNumber", index + 1) : (string)entry[nameKey]!;
                    var kind = entry["kind"]?.Type == JTokenType.String ? Display(entry["kind"]!, "kind", friendly) : null;
                    yield return new(At(name) + (kind == null ? "" : " · " + kind), "", Heading: true);
                    var fields = new JObject(entry.Properties().Where(p => p.Name != nameKey && (kind == null || p.Name != "kind"))
                        .Select(p => new JProperty(p.Name, p.Value.DeepClone())));
                    foreach (var fact in Flatten(fields, friendly: friendly)) yield return fact;
                }
                else foreach (var fact in Flatten(item, At(StudioStrings.Get("Approval.ItemNumber", index + 1)), friendly)) yield return fact;
            }
        }
        else yield return new(path, value is JArray scalars ? scalars.HasValues
            ? string.Join(", ", scalars.Select(FriendlyResponsePresenter.Render)) : StudioStrings.Get("Approval.NoItems")
            : Display(value, path.Split(" / ").LastOrDefault() ?? "", friendly));
    }

    private static string Display(JToken value, string key, bool friendly)
    {
        // Translate known geometry enums only, never user-assigned object names.
        if (friendly && value.Type == JTokenType.String &&
            (key is "kind" or "method" || key == FriendlyResponsePresenter.FriendlyLabel("kind") || key == FriendlyResponsePresenter.FriendlyLabel("method")))
        {
            var symbol = (string)value!;
            if (symbol is "circle" or "rectangle" or "line" or "arc" or "polyline" or "ellipse" or "slot" or "extrude" or "revolve")
                return StudioStrings.Get("Review.Enum." + symbol);
        }
        return FriendlyResponsePresenter.Render(value);
    }

    internal static FrameworkElement Create(JToken values, bool friendly = true)
    {
        var rows = new StackPanel();
        foreach (var fact in Flatten(values, friendly: friendly))
        {
            if (fact.Heading)
            {
                var heading = new TextBlock { Text = fact.Label, FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 14, 0, 4), TextWrapping = TextWrapping.Wrap };
                heading.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush"); rows.Children.Add(heading);
                continue;
            }
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MaxWidth = 210 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            var label = new TextBlock { Text = fact.Label, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 16, 5) };
            label.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
            grid.Children.Add(label);
            var text = new TextBox { Text = fact.Value, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                BorderThickness = new Thickness(0), Background = Brushes.Transparent, FontSize = 13,
                Padding = new Thickness(0, 4, 0, 4), MinWidth = 0 };
            text.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            AutomationProperties.SetName(text, fact.Label);
            Grid.SetColumn(text, 1); grid.Children.Add(text);
            rows.Children.Add(grid);
        }
        return rows;
    }
}
