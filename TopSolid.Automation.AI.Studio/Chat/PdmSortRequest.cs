using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Mcp;

namespace TopSolid.Automation.AI.Studio.Chat;

internal static class PdmSortRequest
{
    public static string? Order(string text)
    {
        var value = Regex.Replace(text.Trim().TrimEnd('.', '?').ToLowerInvariant(), @"\s+", " ");
        return value switch
        {
            "order by old one first" or "oldest first" or "sort oldest first" or "order by oldest first" or "오래된 순으로" => "oldestFirst",
            "newest first" or "sort newest first" or "order by newest first" or "최신순으로" => "newestFirst",
            "alphabetically" or "alphabetical order" or "order by alphabetically" or "sort alphabetically" or "sort by name" or "order by name" or "a-z" or "a to z" or "이름순" or "가나다순" => "nameAscending",
            "reverse alphabetical order" or "reverse alphabetically" or "sort by name descending" or "order by name descending" or "z-a" or "z to a" => "nameDescending",
            _ => null
        };
    }
    public static async Task<IReadOnlyList<AiMessage>?> Run(string text, IReadOnlyList<AiMessage>? previous,
        IMcpClient mcp, Action<ChatTrace> trace, CancellationToken token, Func<UserQuestion, CancellationToken, Task>? showList = null)
    {
        var request = PdmListRequest.Parse(text);
        var order = request?.Order ?? Order(text);
        if (request == null && order == null) return null;
        var names = request?.Tools ?? previous?.Where(m => m.Role == "tool" && m.ToolName is "topsolid_list_projects" or "topsolid_list_libraries")
            .Select(m => m.ToolName!).Distinct().ToArray() ?? [];
        if (names.Length == 0) return null;
        var turn = new List<AiMessage> { new() { Role = "user", Content = text } };
        var dates = request?.Dates ?? order is "oldestFirst" or "newestFirst";
        var inventory = new PdmInventory(dates ? "list all creation dates" : "list all names");
        var browser = new ListPresentation();
        string? error = null;
        foreach (var name in names)
        {
            if (!mcp.IsConnected || !mcp.Tools.Any(t => t.Name == name && !t.RequiresConfirmation && (order == null || t.InputSchema["properties"]?["orderBy"] != null)))
            { error = "Connect to the updated MCP server to read these PDM lists."; break; }
            var offset = 0;
            for (var page = 0; page < 12; page++)
            {
                var args = new JObject { ["offset"] = offset, ["limit"] = 100 };
                if (order != null) args["orderBy"] = order;
                if (dates) args["includeCreationDates"] = true;
                var id = "sort-" + Guid.NewGuid().ToString("N");
                turn.Add(new AiMessage { Role = "assistant", ToolCalls = [new AiToolCall { Id = id, Name = name, Arguments = args }] });
                trace(new ChatTrace("Tool call", name + " " + args.ToString(Newtonsoft.Json.Formatting.None)));
                var result = await mcp.CallToolAsync(name, args, token);
                var content = Newtonsoft.Json.JsonConvert.SerializeObject(result);
                trace(new ChatTrace(result.IsError ? "Tool error" : "Tool result", content));
                turn.Add(new AiMessage { Role = "tool", ToolCallId = id, ToolName = name, Content = content });
                var data = PdmInventory.Data(result);
                if (result.IsError || (order != null && (bool?)data?["sortApplied"] != true) || data?["items"] is not JArray)
                { error = (string?)data?["message"] ?? "The MCP server could not verify the requested list or ordering. No complete list was produced."; break; }
                inventory.Capture(name, result);
                browser.Capture(name, args, result);
                var next = inventory.NextPage();
                if ((bool?)data?["hasMore"] == true && (next == null || next.Value.Offset <= offset)) { error = "The server returned an invalid continuation. No complete list was produced."; break; }
                if (next == null) break;
                if (page == 11) { error = "The list exceeded the page limit. No complete list was produced; request a smaller scope."; break; }
                offset = next.Value.Offset;
            }
            if (error != null) break;
        }
        var heading = order switch { "oldestFirst" => "Oldest first (creation date)", "newestFirst" => "Newest first (creation date)",
            "nameAscending" => "Alphabetical order (A–Z)", "nameDescending" => "Reverse alphabetical order (Z–A)", _ => "" };
        var shown = error == null && await browser.ShowAsync(mcp, showList, trace, token);
        turn.Add(new AiMessage { Role = "assistant", Content = shown ? Localization.StudioStrings.Get("List.Shown") : error ?? ((heading.Length == 0 ? "" : heading + "\n\n") + (inventory.Render() ?? "No project or library records were returned.")) });
        trace(new ChatTrace("Inventory", "PDM list handled through MCP; no model inference required."));
        return turn;
    }
}
