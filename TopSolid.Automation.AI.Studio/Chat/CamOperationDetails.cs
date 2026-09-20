using System.IO;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;

namespace TopSolid.Automation.AI.Studio.Chat;

internal static class CamOperationDetails
{
    internal const string ListTool = "topsolid_list_cam_parameters";
    internal static readonly string[] Pages = ["cam-favorites", "cam-tool", "cam-cutting-conditions", "cam-geometry", "cam-strategy", "cam-comment", "cam-multi-axis", "cam-properties"];
    internal static string Page(JObject row) => TopSolidIcons.CamCategoryKey(row) is var key && Pages.Contains(key) ? key : "cam-properties";
    internal static string Label(string page) => StudioStrings.Get("Cam.Page." + page);
    internal static string Group(JObject row)
    {
        var names = (row["categories"] as JArray)?.Values<string>().OfType<string>() ??
            ((string?)row["name"] ?? "").Split('@').Skip(1).SelectMany(s => s.Split('|'));
        return string.Join(" › ", names.Where(n => n is not ("Global" or "Tool" or "CuttingConditions" or "Geometry" or "Strategy" or "Comments" or "MultiAxis" or "Properties"))
            .Select(n => System.Text.RegularExpressions.Regex.Replace(n, "([a-z])([A-Z])", "$1 $2")));
    }
    internal static string Name(JObject row) => (string?)row["displayName"] ?? ((string?)row["name"] ?? "").Split('@')[0];
    internal static string Value(JObject row) => (string?)(row["allowedValues"] as JArray)?.OfType<JObject>()
        .FirstOrDefault(option => JToken.DeepEquals(option["value"], row["integerValue"]))?["label"] ??
        (string?)row["displayValue"] ?? (string?)row["invariantValue"] ??
        row["realValueSI"]?.ToString() ?? row["integerValue"]?.ToString() ?? row["booleanValue"]?.ToString() ??
        (string?)row["textValue"] ?? StudioStrings.Get("Cam.ValueUnavailable");
    internal static JObject Receipt(JObject element, JObject row) => new() { ["sourceTool"] = ListTool,
        ["sourceArguments"] = new JObject { ["element"] = element.DeepClone() }, ["value"] = row.DeepClone() };

    internal static async Task<IReadOnlyList<JObject>> Load(IMcpClient client, JObject element, CancellationToken token)
    {
        var rows = new List<JObject>(); var identities = new HashSet<string>(StringComparer.Ordinal); var offset = 0; int? total = null;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var result = await client.CallToolAsync(ListTool, new JObject { ["element"] = element.DeepClone(), ["offset"] = offset, ["limit"] = 100 }, token);
            var data = PdmInventory.Data(result);
            if (result.IsError || data?["items"] is not JArray items || data["total"]?.Type != JTokenType.Integer ||
                (int?)data["offset"] != offset || data["hasMore"]?.Type != JTokenType.Boolean ||
                data["operation"] is not JObject operation || !JToken.DeepEquals(operation["element"] ?? operation, element))
                throw new InvalidDataException(StudioStrings.Get("Cam.LoadFailed"));
            total ??= (int)data["total"]!;
            if (total != (int)data["total"]! || total > 20000) throw new InvalidDataException(StudioStrings.Get("Cam.LoadFailed"));
            foreach (var item in items)
            {
                if (item is not JObject row || row["name"]?.Type != JTokenType.String ||
                    !identities.Add(row["name"]!.ToString() + "\n" + row["parameter"]?.ToString(Newtonsoft.Json.Formatting.None)))
                    throw new InvalidDataException(StudioStrings.Get("Cam.LoadFailed"));
                // A failed/oversized batch row remains visible. Never silently omit a parameter.
                rows.Add((JObject)row.DeepClone());
            }
            if ((bool)data["hasMore"]! == false)
            {
                if (rows.Count != total) throw new InvalidDataException(StudioStrings.Get("Cam.LoadFailed"));
                // TopSolid can return the same name on distinct nested native owners.
                // The current setter resolves by name inside the selected operation;
                // retain all rows but never expose an ambiguous write target.
                foreach (var group in rows.GroupBy(r => (string)r["name"]!, StringComparer.Ordinal).Where(g => g.Count() > 1))
                    foreach (var row in group) { row["editSupported"] = false; row["ambiguousName"] = true; }
                return rows;
            }
            if (items.Count == 0 || (int?)data["nextOffset"] != offset + items.Count || rows.Count >= total)
                throw new InvalidDataException(StudioStrings.Get("Cam.LoadFailed"));
            offset += items.Count;
        }
    }
}
