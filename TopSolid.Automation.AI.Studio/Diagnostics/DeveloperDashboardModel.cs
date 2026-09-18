using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.AI.Studio.Diagnostics;

/// <summary>Reads measured durations already emitted by ChatSession; never estimates an active phase.</summary>
internal sealed record DeveloperTurnMetrics(long StartSequence, double? ModelSeconds, double? ToolSeconds,
    double? ConfirmationSeconds, int ModelRequests, int TimedToolCalls)
{
    private static readonly Regex ModelTiming = new(@"^Model request \d+: (?<seconds>\d+(?:[.,]\d+)?) s$", RegexOptions.CultureInvariant);
    private static readonly Regex ToolTiming = new(@"^Tool .+: (?<seconds>\d+(?:[.,]\d+)?) s; user confirmation: (?<confirmation>\d+(?:[.,]\d+)?) s$", RegexOptions.CultureInvariant);

    public static DeveloperTurnMetrics From(SessionLogSnapshot snapshot)
    {
        var start = snapshot.Chat.LastOrDefault(entry => entry.Role.Equals("You", StringComparison.OrdinalIgnoreCase) ||
            entry.Role.Equals("User", StringComparison.OrdinalIgnoreCase))?.Sequence ?? 0;
        double? model = null, tool = null, confirmation = null;
        var requests = 0;
        var calls = 0;
        if (start == 0) return new(0, null, null, null, 0, 0);
        foreach (var entry in snapshot.Trace)
        {
            if (entry.Sequence <= start || !entry.Kind.Equals("Timing", StringComparison.OrdinalIgnoreCase)) continue;
            var match = ModelTiming.Match(entry.Text);
            if (match.Success && Seconds(match.Groups["seconds"].Value) is { } modelSeconds)
            {
                model = (model ?? 0) + modelSeconds;
                requests++;
                continue;
            }
            match = ToolTiming.Match(entry.Text);
            if (match.Success && Seconds(match.Groups["seconds"].Value) is { } toolSeconds &&
                Seconds(match.Groups["confirmation"].Value) is { } confirmationSeconds)
            {
                tool = (tool ?? 0) + toolSeconds;
                confirmation = (confirmation ?? 0) + confirmationSeconds;
                calls++;
            }
        }
        return new(start, model, tool, confirmation, requests, calls);
    }

    private static double? Seconds(string value) =>
        double.TryParse(value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) &&
        double.IsFinite(number) && number >= 0 ? number : null;
}

internal static class DeveloperDetailFormatter
{
    internal const int MaximumDetailCharacters = 120_000;

    public static string Format(string text)
    {
        if (string.IsNullOrEmpty(text)) return "(empty)";
        // The original text is already redacted by DiagnosticLog. Parse only the selected bounded detail.
        // JsonTextReader avoids date coercion so copying JSON preserves source date strings.
        var body = text.Trim();
        var prefix = "";
        var jsonStart = body.IndexOfAny(['{', '[']);
        if (jsonStart > 0 && body[..jsonStart].IndexOfAny(['\r', '\n']) < 0)
        {
            prefix = body[..jsonStart].TrimEnd() + Environment.NewLine;
            body = body[jsonStart..];
        }
        if (body.Length <= MaximumDetailCharacters && (body.StartsWith('{') || body.StartsWith('[')))
        {
            try
            {
                using var reader = new JsonTextReader(new StringReader(body)) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 };
                var token = JToken.ReadFrom(reader);
                if (!reader.Read()) return Limit(prefix + token.ToString(Formatting.Indented));
            }
            catch (JsonException) { /* Ordinary text and incomplete JSON stay readable as received. */ }
        }
        return Limit(text);
    }

    private static string Limit(string text) => text.Length <= MaximumDetailCharacters ? text :
        text[..MaximumDetailCharacters] + Environment.NewLine + "… Display truncated. Export the log for the retained complete entry.";
}
