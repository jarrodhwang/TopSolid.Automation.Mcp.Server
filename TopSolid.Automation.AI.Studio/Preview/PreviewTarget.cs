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
