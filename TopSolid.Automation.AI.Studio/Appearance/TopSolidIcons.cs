using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TopSolid.Automation.AI.Studio.Appearance;

/// <summary>Unmodified icons from the user-provided TopSolid 7.19 icon collection.</summary>
public static class TopSolidIcons
{
    private static readonly HashSet<string> Keys = new(StringComparer.OrdinalIgnoreCase)
        { "app", "project", "document", "sketch", "save", "delete", "settings", "attachment", "folder", "part", "operation", "parameter", "connect", "status", "window", "developer", "refresh", "refresh-warning", "refresh-error",
          "library", "machine", "edge", "point", "curve", "surface", "shape", "color", "image", "view-fit", "view-orbit", "view-pan", "view-edges" };
    private static readonly ConcurrentDictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);

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
        if (name.Contains("part")) return Get("part");
        if (name.Contains("delete") || name.Contains("remove")) return Get("delete");
        if (name.Contains("save") || name.Contains("checkin")) return Get("save");
        if (name.Contains("import") || name.Contains("attach")) return Get("attachment");
        if (name.Contains("document")) return Get("document");
        return Get("app");
    }
}
