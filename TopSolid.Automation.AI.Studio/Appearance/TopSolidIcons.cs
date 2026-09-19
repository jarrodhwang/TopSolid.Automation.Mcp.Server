using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.AI.Studio.Appearance;

/// <summary>Unmodified icons from the user-provided TopSolid 7.19 icon collection.</summary>
public static class TopSolidIcons
{
    private static readonly HashSet<string> Keys = new(StringComparer.OrdinalIgnoreCase)
        { "app", "project", "document", "sketch", "save", "delete", "settings", "attachment", "folder", "part", "operation", "parameter", "connect", "status", "window", "developer", "refresh", "refresh-warning", "refresh-error",
          "library", "machine", "edge", "point", "curve", "surface", "shape", "color", "image", "view-camera", "view-fit", "view-orbit", "view-pan", "view-edges",
          "arguments", "cam-tool", "cam-cutting-conditions", "cam-geometry", "cam-strategy", "cam-comment", "cam-multi-axis", "cam-properties",
          "approve", "cancel", "error", "warning", "question", "license", "license-standalone", "license-floating", "license-user" };
    private static readonly ConcurrentDictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> OperationIcons = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> DocumentIcons = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> ToolFunctionIcons = new(StringComparer.Ordinal);

    static TopSolidIcons()
    {
        using var stream = typeof(TopSolidIcons).Assembly.GetManifestResourceStream("TopSolid.Automation.AI.Studio.Assets.TopSolid.provenance.json");
        if (stream == null) return;
        using var reader = new StreamReader(stream);
        foreach (var entry in JArray.Parse(reader.ReadToEnd()).OfType<JObject>())
        {
            if ((string?)entry["ToolFunction"] is { } toolFunction && (string?)entry["Key"] is { } toolKey)
            { ToolFunctionIcons[toolFunction] = toolKey; Keys.Add(toolKey); }
            if ((string?)entry["NativeType"] is { } type && (string?)entry["Key"] is { } key)
            { OperationIcons[type] = key; Keys.Add(key); }
            if ((string?)entry["DocumentExtension"] is { } extension && (string?)entry["Key"] is { } documentKey)
            {
                DocumentIcons[NormalizeDocumentExtension(extension)] = documentKey; Keys.Add(documentKey);
                if ((string?)entry["DocumentType"] is { } documentType) DocumentIcons[documentType.Trim()] = documentKey;
            }
        }
    }

    public static string ToolFunctionKey(JToken? row) => row is JObject obj && (string?)obj["toolFunction"] is { } function &&
        ToolFunctionIcons.TryGetValue(function, out var key) ? key : "cam-tool-generic";

    // Use receipt metadata, never the AI's itemKind or user-assigned filename, to identify document artwork.
    public static string? DocumentKey(JToken? row)
    {
        if (row is not JObject obj) return null;
        foreach (var field in new[] { "extension", "typeFullName", "documentType", "type" })
        {
            if (obj[field]?.Type != JTokenType.String) continue;
            var value = ((string)obj[field]!).Trim();
            if (DocumentIcons.TryGetValue(field == "extension" ? NormalizeDocumentExtension(value) : value, out var key)) return key;
        }
        return obj["extension"]?.Type == JTokenType.String || obj["documentId"] != null || obj["pdmObjectId"] != null ||
            string.Equals((string?)obj["kind"], "document", StringComparison.OrdinalIgnoreCase) ? "document" : null;
    }

    private static string NormalizeDocumentExtension(string value)
    {
        value = value.Trim();
        return value.Length == 0 || value[0] == '.' ? value : "." + value;
    }

    // Only exact types from server receipts select machining icons. Unknown types
    // keep a neutral NC icon even when their user-assigned name says "roughing".
    public static string OperationKey(JToken? row)
    {
        if (row is not JObject obj) return "operation";
        var type = (string?)obj["operationType"] ?? (string?)(obj["operationInfo"] as JObject)?["operationType"] ?? (string?)obj["type"];
        return type != null && OperationIcons.TryGetValue(type, out var key) ? key : "operation";
    }

    public static string CamCategoryKey(JToken? row)
    {
        if (row is not JObject obj) return "parameter";
        var categories = obj["categories"] as JArray ?? (obj["parameter"] as JObject)?["categories"] as JArray;
        var fullName = (string?)obj["fullName"] ?? (string?)obj["name"] ?? "";
        var names = new HashSet<string>(categories?.Values<string>().OfType<string>() ??
            (fullName.IndexOf('@') >= 0 ? fullName[(fullName.IndexOf('@') + 1)..].Split('|') : []), StringComparer.OrdinalIgnoreCase);
        // A pane can be nested under Global or a numbered axis. Resolve specific
        // panes first: CuttingConditions|Tool must keep the cutting-condition icon.
        if (names.Contains("CuttingConditions")) return "cam-cutting-conditions";
        if (names.Overlaps(["Comments", "Comment", "IsoComments"])) return "cam-comment";
        if (names.Overlaps(["MultiAxis", "ToFiveAxisPrimitive"])) return "cam-multi-axis";
        if (names.Contains("Tool") || fullName == "Tool@Global") return "cam-tool";
        if (names.Overlaps(["Geometry", "Topology"])) return "cam-geometry";
        if (names.Contains("Strategy")) return "cam-strategy";
        if (names.Contains("Properties")) return "cam-properties";
        return "parameter";
    }

    public static ImageSource Get(string key)
    {
        key = !string.IsNullOrEmpty(key) && Keys.Contains(key) ? key.ToLowerInvariant() : "app";
        return Cache.GetOrAdd(key, static name =>
        {
            if (name == "view-camera") return CreateCameraIcon();
            try
            {
                var image = new BitmapImage(new Uri($"pack://application:,,,/TopSolid.Automation.AI.Studio;component/Assets/TopSolid/{name}.png", UriKind.Absolute));
                image.Freeze();
                return image;
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException)
            {
                // Keep approval UI operable if a deployment has a missing image resource.
                var drawing = new GeometryDrawing(Brushes.DodgerBlue, new Pen(Brushes.SlateGray, 1), new RectangleGeometry(new System.Windows.Rect(2, 2, 18, 18), 2, 2));
                var fallback = new DrawingImage(drawing); fallback.Freeze(); return fallback;
            }
        });
    }

    private static ImageSource CreateCameraIcon()
    {
        var fill = new SolidColorBrush(Color.FromRgb(205, 226, 250)); fill.Freeze();
        var outline = new Pen(new SolidColorBrush(Color.FromRgb(0, 83, 163)), 1.1); outline.Brush.Freeze(); outline.Freeze();
        var glass = new SolidColorBrush(Color.FromRgb(77, 153, 224)); glass.Freeze();
        var body = Geometry.Parse("M3,7 L13,7 L20,3 L20,21 L13,17 L3,17 Z");
        var lens = new EllipseGeometry(new System.Windows.Point(9, 12), 3.2, 3.2);
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(fill, outline, body));
        group.Children.Add(new GeometryDrawing(glass, outline, lens));
        var image = new DrawingImage(group); image.Freeze();
        return image;
    }

    public static ImageSource ForTool(string toolName)
    {
        var name = (toolName ?? "").ToLowerInvariant();
        if (name.Contains("license")) return Get("license");
        // Target identity takes precedence: a project request should show the project icon.
        if (name.Contains("project")) return Get("project");
        if (name.Contains("folder")) return Get("folder");
        if (name.Contains("parameter")) return Get("parameter");
        if (name.Contains("cam") || name.Contains("operation")) return Get("operation");
        if (name.Contains("sketch") || name.Contains("profile")) return Get("sketch");
        if (name.Contains("part") || name.Contains("cylinder") || name.Contains("extrude") || name.Contains("revolve")) return Get("part");
        if (name.Contains("delete") || name.Contains("remove")) return Get("delete");
        if (name.Contains("save") || name.Contains("checkin")) return Get("save");
        if (name.Contains("import") || name.Contains("attach")) return Get("attachment");
        if (name.Contains("document")) return Get("document");
        return Get("app");
    }
}
