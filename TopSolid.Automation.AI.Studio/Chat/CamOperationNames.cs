using System.Collections.Frozen;
using System.IO;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio.Chat;

// Offline, reviewed labels. Matching is exact, including the module namespace.
// The embedded source records retain official help URLs and installed type evidence.
internal static class CamOperationNames
{
    internal sealed record Entry(string NativeType, string EnglishName, string KoreanName,
        string Category, string HelpUrl, string OfficialHelpTitle);

    internal static IReadOnlyDictionary<string, Entry> Entries { get; } = Load();

    internal static string? Find(string? nativeType) => nativeType != null && Entries.TryGetValue(nativeType, out var entry)
        ? StudioStrings.CurrentLanguage == "ko" ? entry.KoreanName : entry.EnglishName : null;

    private static FrozenDictionary<string, Entry> Load()
    {
        using var stream = typeof(CamOperationNames).Assembly.GetManifestResourceStream("TopSolid.Automation.AI.Studio.CamOperationNames.json");
        if (stream == null) return FrozenDictionary<string, Entry>.Empty;
        using var reader = new StreamReader(stream);
        var entries = JObject.Parse(reader.ReadToEnd())["entries"]?.ToObject<Entry[]>() ?? [];
        return entries.ToFrozenDictionary(entry => entry.NativeType, StringComparer.Ordinal);
    }
}
