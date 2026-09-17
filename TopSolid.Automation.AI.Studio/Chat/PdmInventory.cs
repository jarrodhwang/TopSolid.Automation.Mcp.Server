using System.Text;
using System.IO;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

// Render inventory receipts, never a model's lossy restatement of them.
internal sealed class PdmInventory(string request)
{
    internal static JObject? Data(McpToolResult result)
    {
        if (result.StructuredContent != null) return result.StructuredContent;
        try {
            using var reader = new Newtonsoft.Json.JsonTextReader(new StringReader((string?)result.Content.OfType<JObject>().FirstOrDefault(c => (string?)c["type"] == "text")?["text"] ?? "{}")) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None };
            return JObject.Load(reader);
        } catch (Newtonsoft.Json.JsonException) { return null; }
    }
    private readonly Dictionary<string, SortedDictionary<int, JObject>> rows = new();
    private readonly Dictionary<string, int> totals = new();
    private readonly Dictionary<string, int?> next = new();
    public bool HasCategory(string tool) => rows.ContainsKey(tool);
    public (string Tool, int Offset)? NextPage() => next.Where(p => p.Value.HasValue).Select(p => ((string Tool, int Offset)?)(p.Key, p.Value!.Value)).FirstOrDefault();
    private readonly bool enabled = (request.Contains("all", StringComparison.OrdinalIgnoreCase) || request.Contains("전체") || request.Contains("모든")) &&
        (request.Contains("list", StringComparison.OrdinalIgnoreCase) || request.Contains("목록") || request.Contains("이름"));
    public void Capture(string tool, McpToolResult result)
    {
        if (!enabled || result.IsError || tool is not ("topsolid_list_projects" or "topsolid_list_libraries")) return;
        var data = Data(result);
        if (data == null) return;
        if (data["items"] is not JArray items || data["total"]?.Type != JTokenType.Integer) return;
        if (!rows.TryGetValue(tool, out var page)) rows[tool] = page = new();
        totals[tool] = (int)data["total"]!;
        var offset = (int?)data["offset"] ?? 0;
        var following = (int?)data["nextOffset"] ?? offset + items.Count;
        next[tool] = (bool?)data["hasMore"] == true && following > offset ? following : null;
        for (var i = 0; i < items.Count; i++) if (items[i] is JObject row) page[offset + i] = (JObject)row.DeepClone();
    }
    public string? Render()
    {
        if (rows.Count == 0) return null;
        var output = new StringBuilder();
        foreach (var (tool, page) in rows)
        {
            var valid = page.Where(p => p.Value["name"]?.Type == JTokenType.String && p.Value["pdmObjectId"]?.Type == JTokenType.String).ToArray();
            var complete = valid.Length == totals[tool] && valid.Select(p => p.Key).SequenceEqual(Enumerable.Range(0, totals[tool]));
            output.AppendLine($"{(tool.EndsWith("projects") ? "Projects" : "Libraries")}: {valid.Length} of {totals[tool]}{(complete ? " (complete)" : " (partial — additional records are not shown)")}").AppendLine();
            foreach (var (index, row) in valid)
            {
                output.Append(index + 1).Append(". ").Append((string)row["name"]!);
                if (request.Contains("date", StringComparison.OrdinalIgnoreCase) || request.Contains("날짜") || request.Contains("생성일"))
                    output.Append(" — Creation date: ").Append((string?)row["creationDate"] ?? "unavailable through the connected Automation service");
                output.AppendLine();
            }
            output.AppendLine();
        }
        return output.ToString().TrimEnd();
    }
}
