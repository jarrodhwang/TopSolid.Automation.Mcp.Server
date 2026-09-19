using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

internal sealed class ActiveModelingTarget(bool enabled)
{
    private string? documentId;
    private string? documentType;

    internal void Capture(string tool, McpToolResult result)
    {
        if (!enabled || result.IsError) return;
        var data = PdmInventory.Data(result);
        if (tool == ActiveDocumentContext.Tool)
        {
            documentId = (string?)data?["document"]?["documentId"];
            documentType = (string?)data?["document"]?["typeFullName"] ?? (string?)data?["document"]?["type"];
        }
        else if (data?["originalDocumentId"] != null && (string?)data["originalDocumentId"] == documentId && data["documentId"]?.Type == JTokenType.String)
            documentId = (string?)data["documentId"];
    }

    internal void Answered(QuestionAnswer answer)
    {
        if (!enabled || answer.Data["selected"] is not JArray { Count: 1 } selected || selected[0]?["value"] is not JObject row || row["id"] != null) return;
        if (row["documentId"]?.Type == JTokenType.String)
        {
            documentId = (string?)row["documentId"];
            documentType = (string?)row["typeFullName"] ?? (string?)row["type"];
        }
    }

    internal string? Validate(AiToolCall call, bool changesDocument)
    {
        if (!enabled || !changesDocument || call.Arguments["documentId"] == null) return null;
        if (documentId == null) return "No active or user-selected document was verified in this turn. Read the active document before preparing a change.";
        if ((string?)call.Arguments["documentId"] != documentId)
            return "The requested active/selected document has the exact documentId " + documentId + ". Copy this ID unchanged; do not substitute another document or a historical revision.";
        if (call.Name == "topsolid_create_cylinder" && documentType != null && !documentType.EndsWith(".PartDocument", StringComparison.Ordinal))
            return "The active document is not a part. Ask the user to choose and activate a part before cylinder creation; no geometry was prepared or changed.";
        return null;
    }
}
