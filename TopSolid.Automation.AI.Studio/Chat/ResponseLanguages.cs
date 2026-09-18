namespace TopSolid.Automation.AI.Studio.Chat;

/// <summary>Trusted response preference. Stored settings cannot inject instructions into the system prompt.</summary>
public static class ResponseLanguages
{
    public static string Normalize(string? language) => language?.Trim().ToLowerInvariant() switch
    {
        "en" => "en", "ko" => "ko", "ja" => "ja", "zh" => "zh", "fr" => "fr", "de" => "de", "es" => "es", _ => "auto"
    };

    public static string Instruction(string? language) => Normalize(language) switch
    {
        "en" => "Reply concisely in English. Preserve source names, code, identifiers and units verbatim.",
        "ko" => "Reply concisely in Korean. Preserve source names, code, identifiers and units verbatim.",
        "ja" => "Reply concisely in Japanese. Preserve source names, code, identifiers and units verbatim.",
        "zh" => "Reply concisely in Simplified Chinese. Preserve source names, code, identifiers and units verbatim.",
        "fr" => "Reply concisely in French. Preserve source names, code, identifiers and units verbatim.",
        "de" => "Reply concisely in German. Preserve source names, code, identifiers and units verbatim.",
        "es" => "Reply concisely in Spanish. Preserve source names, code, identifiers and units verbatim.",
        _ => "Reply concisely in the user's language (Korean requests get Korean replies)."
    };
}
