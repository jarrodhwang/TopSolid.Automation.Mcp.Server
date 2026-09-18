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
          "library", "machine", "edge", "point", "curve", "surface", "shape", "color", "image", "view-fit", "view-orbit", "view-pan", "view-edges",
          "arguments", "cam-tool", "cam-cutting-conditions", "cam-geometry", "cam-strategy", "cam-comment", "cam-multi-axis", "cam-properties" };
    private static readonly ConcurrentDictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> OperationIcons = new(StringComparer.Ordinal);

    static TopSolidIcons()
    {
        using var stream = typeof(TopSolidIcons).Assembly.GetManifestResourceStream("TopSolid.Automation.AI.Studio.Assets.TopSolid.provenance.json");
        if (stream == null) return;
        using var reader = new StreamReader(stream);
        foreach (var entry in JArray.Parse(reader.ReadToEnd()).OfType<JObject>())
            if ((string?)entry["NativeType"] is { } type && (string?)entry["Key"] is { } key)
            { OperationIcons[type] = key; Keys.Add(key); }
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

    public static ImageSource ForTool(string toolName)
    {
        var name = (toolName ?? "").ToLowerInvariant();
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
