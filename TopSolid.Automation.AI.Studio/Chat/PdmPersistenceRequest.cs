using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

// Only complete, single-action requests bypass inference. This never bypasses
// the MCP preview, target recheck, or the user's confirmation.
internal sealed record PdmPersistenceRequest(string Operation, string? Project = null)
{
    internal static PdmPersistenceRequest? Parse(string text)
    {
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (Regex.IsMatch(text, @"\A\s*(?:(?:please|then|than)\s+)?save\s+all\s+(?:(?:other|open|modified|dirty)\s+)*(?:docs|documents)\s*[.!]?\s*\z", options))
            return new("save");
        var match = Regex.Match(text, "\\A\\s*(?:please\\s+)?check[ -]?in\\s*,?\\s*(?:the\\s+)?project\\s*(?:\\(\\s*[\"“]?(?<name>[^\"”()\\r\\n]+?)[\"”]?\\s*\\)|[\"“](?<name>[^\"”\\r\\n]+)[\"”])\\s*[.!]?\\s*\\z", options);
        return match.Success ? new("checkIn", match.Groups["name"].Value.Trim()) : null;
    }
    internal async Task<PdmCreateRequest.Plan> Resolve(IMcpClient mcp, Action<ChatTrace> trace, CancellationToken token)
    {
        var tool = Operation == "save" ? "topsolid_save_documents" : "topsolid_check_in_pdm_objects";
        if (!mcp.IsConnected || !mcp.Tools.Any(t => t.Name == tool && t.RequiresConfirmation))
            return new(null, "Connect to an updated MCP server with batch save and native check-in tools. No change was made.");
        var args = new JObject { ["scope"] = "openDirty" };
        if (Operation == "checkIn")
        {
            const string lookup = "topsolid_get_document_creation_context";
            if (!mcp.Tools.Any(t => t.Name == lookup && !t.RequiresConfirmation))
                return new(null, "The connected MCP server cannot resolve the named working project. No check-in was attempted.");
            var input = new JObject { ["projectName"] = Project };
            trace(new("Tool call", lookup + " " + input.ToString(Formatting.None)));
            var result = await mcp.CallToolAsync(lookup, input, token);
            trace(new(result.IsError ? "Tool error" : "Tool result", JsonConvert.SerializeObject(result)));
            var data = PdmInventory.Data(result);
            if (result.IsError || (bool?)data?["complete"] != true || data["projectMatches"] is not JArray rows)
                return new(null, "The working-project lookup was incomplete. No check-in was attempted.");
            var matches = rows.Where(r => string.Equals((string?)r["name"], Project, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1 || string.IsNullOrWhiteSpace((string?)matches[0]["pdmObjectId"]))
                return new(null, matches.Length == 0 ? $"No working project named \"{Project}\" was found. No check-in was attempted." :
                    $"More than one working project matches \"{Project}\". Select its exact PDM ID before checking in.");
            args = new JObject { ["pdmObjectIds"] = new JArray(matches[0]["pdmObjectId"]!.DeepClone()), ["recursive"] = true };
        }
        return new(new AiToolCall { Id = "pdm-persist-" + Guid.NewGuid().ToString("N"), Name = tool, Arguments = args }, null);
    }
    internal static string Render(McpToolResult result)
    {
        var data = PdmInventory.Data(result);
        if (result.IsError || (bool?)data?["complete"] != true)
            return "No complete persistence result was verified. " + ((string?)data?["detail"] ?? (string?)result.Content.FirstOrDefault()?["text"]) +
                (data?["partialChange"] == null ? "" : "\nReview the partial-change receipt before another action: " + data["partialChange"]!.ToString(Formatting.None));
        var rows = (JArray?)data["after"] ?? [];
        if ((string?)data["operation"] == "save")
        {
            if (rows.Count == 0) return "There are no modified open documents to save.";
            return ((bool?)data["saved"] == true ? $"Saved {rows.Count} document(s)." : $"Save completed, but only {data["savedCount"]} of {rows.Count} document(s) are verified clean.") +
                " No check-in was performed.\n" + string.Join("\n", rows.Select(r => $"- {r["name"]}" + ((bool?)r["isDirty"] == true ? " (still modified)" : "")));
        }
        return $"TopSolid completed the check-in call. {data["checkedInCount"]} of {rows.Count} object(s) report CheckedIn.\n" +
            string.Join("\n", rows.Select(r => $"- {r["name"]}: {r["state"]}" + ((bool?)r["isDirty"] == true ? " (still modified)" : "")));
    }
}
