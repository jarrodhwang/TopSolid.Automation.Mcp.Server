using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.AI.Studio.Preview;

internal static class PreviewTarget
{
    // Only structured identities from immutable tool receipts/proposals are eligible. Never parse prose,
    // friendly names or arbitrary integer IDs, and never substitute the active document.
    internal static JObject? FromProposal(JObject proposal) => FromValue(proposal["target"]) ?? FromValue(proposal["arguments"]);
    internal static JObject? FromChoice(JObject receipt, string kind)
    {
        if (kind is "project" or "library" or "option") return null;
        if (kind == "tool" || (string?)receipt["sourceTool"] == "topsolid_list_cam_tools")
        {
            // The tool's owner is the machining document, never the tool preview target.
            // Only the referenced library document resolved by the server is eligible.
            return receipt["value"]?["toolPreviewDocumentId"] is JValue { Type: JTokenType.String } document &&
                !string.IsNullOrWhiteSpace((string?)document)
                ? new JObject { ["documentId"] = document.DeepClone() } : null;
        }
        if (kind == "operation" && receipt["value"] is JObject row)
        {
            // Summary rows wrap the operation; basic/scenario lists return the
            // operation itself (ElementId or ElementExId) with display metadata.
            var operation = row["operation"] as JObject ?? row;
            var element = operation?["element"] as JObject ?? operation;
            if (element?["documentId"]?.Type == JTokenType.String && element["id"]?.Type == JTokenType.Integer)
                return new JObject { ["documentId"] = element["documentId"]!.DeepClone(),
                    ["operation"] = new JObject { ["documentId"] = element["documentId"]!.DeepClone(), ["id"] = element["id"]!.DeepClone() } };
        }
        var target = FromValue(receipt["value"]);
        if (target != null) return target;
        if (kind is "document" or "machine" && receipt["value"] is JObject value)
        {
            var pdm = value["pdmObjectId"] ?? value["objectId"];
            if (pdm?.Type == JTokenType.String) return new JObject { ["pdmObjectId"] = pdm.DeepClone() };
        }
        return FromValue(receipt["sourceArguments"]);
    }
    private static JObject? FromValue(JToken? value)
    {
        if (value is not JContainer container) return null;
        var documents = container.DescendantsAndSelf().OfType<JProperty>().Where(p => p.Name == "documentId" && p.Value.Type == JTokenType.String)
            .Select(p => (string)p.Value!).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal).ToArray();
        return documents.Length == 1 ? new JObject { ["documentId"] = documents[0] } : null;
    }
}
