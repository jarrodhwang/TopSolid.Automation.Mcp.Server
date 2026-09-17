using TopSolid.Automation.AI.Studio.AI;

namespace TopSolid.Automation.AI.Studio.Chat;

internal static class ModelHistory
{
    // Keep the full transcript/receipts in diagnostics. A previous full inventory
    // should not be sent twice (JSON plus prose) to every later model request.
    public static IEnumerable<AiMessage> ForTurn(IReadOnlyList<AiMessage> turn)
    {
        var calls = turn.SelectMany(m => m.ToolCalls).ToArray();
        if (calls.Length == 0 || calls.Any(c => c.Name is not ("topsolid_list_projects" or "topsolid_list_libraries")) ||
            turn.Any(m => m.Role == "tool" && m.ToolName is not ("topsolid_list_projects" or "topsolid_list_libraries")))
            return turn;
        var user = turn.FirstOrDefault(m => m.Role == "user");
        if (user == null) return turn;
        return [user, new AiMessage { Role = "assistant", Content =
            "The previous PDM inventory response and its receipts are in the displayed transcript and diagnostics. " +
            "Large name lists are omitted from model context. For the current request, query the relevant live list/search/context tool again; do not invent or recall names or IDs." }];
    }
}
