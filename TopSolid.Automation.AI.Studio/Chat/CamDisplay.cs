using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio.Chat;

// Presentation only: native type codes and receipt identities remain unchanged.
internal static class CamDisplay
{
    internal static string Operation(string? type) => CamOperationNames.Find(type) ?? StudioStrings.Get("Cam.Operation");

    internal static string ToolType(string? type) => StudioStrings.Get(type switch
    {
        "FaceMill" => "Cam.FaceMill", "BallNoseMill" => "Cam.BallNoseMill",
        "EndMill" => "Cam.EndMill", "Drill" => "Cam.Drill",
        _ => "Cam.Tool"
    });

    internal static string? ToolText(JToken row)
    {
        var number = (string?)row["toolNumber"] ?? (string?)row["toolPocket"];
        var name = (string?)row["toolDefinitionName"];
        if (string.IsNullOrWhiteSpace(number) && string.IsNullOrWhiteSpace(name))
            return (string?)row["toolDisplayName"];
        return string.Join("\n", new[] { number, name, ToolType((string?)row["toolFunction"]) }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    internal static string Text(string text)
    {
        text = Regex.Replace(text, @"\bTopSolid\.Cam\.[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)*(?:`\d+)?", m => Operation(m.Value));
        return Regex.Replace(text, @"\b(?:FaceMill|BallNoseMill|EndMill)\b", m => ToolType(m.Value));
    }
}
