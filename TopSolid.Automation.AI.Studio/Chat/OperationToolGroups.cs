using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.AI.Studio.Chat;

internal static class OperationToolGroups
{
    // Never merge by a display label: different tools may share a pocket/name.
    internal static string? Identity(JToken row)
    {
        if ((bool?)row["hasTool"] == false) return "no-tool";
        var tool = row["tool"] as JObject;
        if (tool?["documentId"]?.Type != JTokenType.String || tool["id"]?.Type != JTokenType.Integer) return null;
        return new JObject { ["documentId"] = tool["documentId"]!.DeepClone(), ["id"] = tool["id"]!.DeepClone(),
            ["part"] = row["part"]?.DeepClone() }.ToString(Formatting.None);
    }

    internal static IReadOnlyDictionary<string, int> Runs(IReadOnlyList<QuestionChoice> choices)
    {
        var result = new Dictionary<string, int>();
        string? previous = null; var run = -1;
        foreach (var choice in choices)
        {
            if (choice.ToolGroupKey == null || choice.ToolGroupKey != previous) run++;
            result.Add(choice.Key, run); previous = choice.ToolGroupKey;
        }
        return result;
    }
}
