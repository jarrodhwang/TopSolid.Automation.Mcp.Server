using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

// Only a single explicit, quoted creation request enters this path. Execution
// still goes through ChatSession's existing preview / human-confirmation code.
internal sealed record PdmCreateRequest(string Kind, string Name, string? Project)
{
    private const string Prefix = @"\A\s*(?:please\s+)?(?:(?:can|could|would)\s+(?:you|u)\s+)?(?:please\s+)?(?:create|make)\s+(?:(?:one|a|new)\s+)?";
    private const string Label = "[\"“](?<name>[^\"”\\r\\n]{1,128})[\"”]";
    internal static PdmCreateRequest? Parse(string text)
    {
        var project = Regex.Match(text, Prefix + @"(?:pdm\s+)?project\s+(?:name|named|called)\s+" + Label + @"\s*[.!]?\s*\z", RegexOptions.IgnoreCase);
        if (project.Success) return new("project", project.Groups["name"].Value, null);
        var part = Regex.Match(text, Prefix + @"part(?:\s+(?:document|file))?\s+(?:name|named|called)\s+" + Label + "\\s+(?:in|under)\\s+(?:the\\s+)?project\\s+[\"“](?<project>[^\"”\\r\\n]{1,128})[\"”]\\s*[.!]?\\s*\\z", RegexOptions.IgnoreCase);
        return part.Success ? new("part", part.Groups["name"].Value, part.Groups["project"].Value) : null;
    }

    internal sealed record Plan(AiToolCall? Call, string? Answer);
    internal async Task<Plan> Resolve(IMcpClient mcp, Action<ChatTrace> trace, CancellationToken token)
    {
        var partTool = Kind == "part" && mcp.Tools.Any(t => t.Name == "topsolid_create_part_document" && t.RequiresConfirmation);
        var tool = Kind == "project" ? "topsolid_create_project" : partTool ? "topsolid_create_part_document" : "topsolid_create_document";
        if (!mcp.IsConnected || !mcp.Tools.Any(t => t.Name == tool && t.RequiresConfirmation))
            return new(null, "Connect to an MCP server with confirmed PDM creation tools.");
        var args = new JObject { ["name"] = Name };
        if (Kind == "part")
        {
            List<JObject> projects;
            if (mcp.Tools.Any(t => t.Name == "topsolid_get_document_creation_context" && !t.RequiresConfirmation))
            {
                var input = new JObject { ["projectName"] = Project };
                trace(new("Tool call", "topsolid_get_document_creation_context " + input.ToString(Formatting.None)));
                var result = await mcp.CallToolAsync("topsolid_get_document_creation_context", input, token);
                trace(new(result.IsError ? "Tool error" : "Tool result", JsonConvert.SerializeObject(result)));
                var data = PdmInventory.Data(result);
                if (result.IsError || (bool?)data?["complete"] != true || data?["projectMatches"] is not JArray matchesData)
                    throw new InvalidOperationException("Document creation lookup is incomplete. No creation was attempted.");
                if ((bool?)data["readiness"]?["canCreatePdmObjects"] == false)
                    return new(null, "Finish or cancel the active TopSolid command before creating the part: " +
                        ((string?)data["readiness"]?["activeCommandName"] ?? (string?)data["readiness"]?["activeCommandFullName"] ?? "active command") + ". No creation was attempted.");
                projects = matchesData.OfType<JObject>().ToList();
            }
            else
            {
                projects = await Rows("topsolid_list_projects", new JObject());
            }
            var matches = projects.Where(row => string.Equals((string?)row["name"], Project, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1) return new(null, matches.Length == 0 ? $"No working project named \"{Project}\" was found. No document was created." :
                $"More than one working project matches \"{Project}\". Select its exact PDM ID before creating a document. No change was made.");
            args["ownerId"] = matches[0]["pdmObjectId"]!.DeepClone();
            // .TopPrt is the installation's verified native part extension. The
            // generic fallback also creates an empty part; no loaded sample is needed.
            if (!partTool) { args["extension"] = ".TopPrt"; args["useDefaultTemplate"] = false; }
        }
        return new(new AiToolCall { Id = "pdm-create-" + Guid.NewGuid().ToString("N"), Name = tool, Arguments = args }, null);

        async Task<List<JObject>> Rows(string name, JObject input)
        {
            if (!mcp.Tools.Any(t => t.Name == name && !t.RequiresConfirmation)) throw new InvalidOperationException("The connected MCP server lacks " + name + ". No creation was attempted.");
            var rows = new List<JObject>(); var offset = 0;
            for (var page = 0; page < 12; page++)
            {
                token.ThrowIfCancellationRequested(); input["offset"] = offset; input["limit"] = 100;
                trace(new("Tool call", name + " " + input.ToString(Formatting.None)));
                var result = await mcp.CallToolAsync(name, input, token);
                trace(new(result.IsError ? "Tool error" : "Tool result", JsonConvert.SerializeObject(result)));
                var data = PdmInventory.Data(result);
                if (result.IsError || data?["items"] is not JArray items || (int?)data["failed"] > 0 || items.Any(row => (bool?)row["isError"] == true))
                    throw new InvalidOperationException("PDM creation lookup failed or returned incomplete identities. No creation was attempted.");
                rows.AddRange(items.OfType<JObject>());
                if ((bool?)data["hasMore"] != true) return rows;
                var next = (int?)data["nextOffset"] ?? offset + items.Count;
                if (next <= offset) break;
                offset = next;
            }
            throw new InvalidOperationException("PDM creation lookup exceeded its page limit. Narrow the target; no creation was attempted.");
        }
    }

    internal static string Render(McpToolResult result)
    {
        var data = PdmInventory.Data(result);
        if (!result.IsError && (bool?)data?["created"] == true && (bool?)data["complete"] == true)
            return $"Created \"{(string?)data["name"]}\".\nPDM ID: {(string?)data["pdmObjectId"]}" +
                (data["documentId"] == null ? "" : $"\nDocument ID: {(string?)data["documentId"]}");
        return "Creation result: " + ((string?)data?["message"] ?? (string?)result.Content.FirstOrDefault()?["text"] ?? "No verified completion was returned.") +
            (data?["partialChange"] == null ? "" : "\nReview the partial-change receipt before another action: " + data["partialChange"]!.ToString(Formatting.None));
    }
}
