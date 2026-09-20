using System.IO;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.AI.Studio.Settings;

/// <summary>Studio shortcuts only; never modifies native TopSolid favorites or the CAM document.</summary>
internal sealed class CamParameterFavorites
{
    private readonly string path;
    internal HashSet<string> Names { get; } = new(StringComparer.Ordinal);
    internal CamParameterFavorites(string? directory = null)
    {
        path = Path.Combine(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TopSolid.Automation.AI.Studio"), "cam-parameter-favorites.json");
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length <= 1024 * 1024)
                foreach (var name in JArray.Parse(File.ReadAllText(path)).Values<string>().OfType<string>()) Names.Add(name);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException) { }
    }
    internal void Set(string name, bool favorite)
    {
        var updated = new HashSet<string>(Names, StringComparer.Ordinal);
        if (favorite) updated.Add(name); else updated.Remove(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, new JArray(updated.Order(StringComparer.Ordinal)).ToString()); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        Names.Clear(); Names.UnionWith(updated);
    }
}
