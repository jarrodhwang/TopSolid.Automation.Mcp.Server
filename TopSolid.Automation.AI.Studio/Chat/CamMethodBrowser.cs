using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;

namespace TopSolid.Automation.AI.Studio.Chat;

internal static class CamMethodBrowser
{
    internal static bool IsMethod(JObject row) => (string?)row["kind"] == "document" &&
        !string.IsNullOrWhiteSpace((string?)row["pdmObjectId"]) &&
        !string.IsNullOrWhiteSpace((string?)row["documentId"]) &&
        (string.Equals((string?)row["extension"], ".TopMillTurnMethod", StringComparison.OrdinalIgnoreCase) ||
        ((string?)row["typeFullName"] ?? "").StartsWith("TopSolid.Cam.NC.", StringComparison.Ordinal) &&
        ((string?)row["typeFullName"] ?? "").Contains("Method", StringComparison.Ordinal));

    internal sealed class Pages(IMcpClient client, string tool, string? parent = null)
    {
        internal List<JObject> Rows { get; } = [];
        internal bool More { get; private set; } = true;
        internal bool Loaded { get; private set; }
        private int? total;
        internal async Task Fetch(CancellationToken token)
        {
            if (!More) return;
            if (Rows.Count >= 12500) throw new InvalidOperationException(StudioStrings.Get("List.Incomplete"));
            var args = new JObject { ["offset"] = Rows.Count, ["limit"] = 50 };
            if (parent != null) args["pdmObjectId"] = parent;
            var result = await client.CallToolAsync(tool, args, token);
            token.ThrowIfCancellationRequested();
            var data = PdmInventory.Data(result);
            if (result.IsError || data == null || data.ToString().Length > 64000 || data["items"] is not JArray items || items.Count > 50 ||
                (int?)data["offset"] != Rows.Count || data["total"]?.Type != JTokenType.Integer || data["hasMore"]?.Type != JTokenType.Boolean || (int?)data["failed"] > 0)
                throw new InvalidOperationException(StudioStrings.Get("List.Incomplete"));
            var count = (int)data["total"]!; var next = Rows.Count + items.Count; var more = (bool)data["hasMore"]!;
            var ids = Rows.Select(r => (string)r["pdmObjectId"]!).ToHashSet(StringComparer.Ordinal);
            if (count < next || total.HasValue && total != count || more != (next < count) || more &&
                (next == Rows.Count || (int?)data["nextOffset"] != next) || items.Any(t => t is not JObject r ||
                (bool?)r["isError"] == true || string.IsNullOrWhiteSpace((string?)r["pdmObjectId"]) || !ids.Add((string)r["pdmObjectId"]!)))
                throw new InvalidOperationException(StudioStrings.Get("List.Incomplete"));
            Rows.AddRange(items.Cast<JObject>().Select(r => (JObject)r.DeepClone())); total = count; More = more; Loaded = true;
        }
    }

    internal static async Task<JObject?> Select(IMcpClient client, JObject receipt,
        Func<JObject, CancellationToken, Task<bool>>? confirm, CancellationToken token)
    {
        if (!IsMethod(receipt)) throw new InvalidOperationException(StudioStrings.Get("Cam.MethodUnavailable"));
        var pdm = (string)receipt["pdmObjectId"]!; var revision = (string)receipt["documentId"]!;
        if ((bool?)receipt["isLoaded"] != true)
        {
            if (client is not IConfirmableMcpClient confirmed || confirm == null) throw new InvalidOperationException(StudioStrings.Get("Cam.MethodUnavailable"));
            const string open = "topsolid_open_document";
            var args = new JObject { ["documentId"] = revision };
            var proposal = await confirmed.PrepareToolAsync(open, args, token);
            if ((string?)proposal["toolName"] != open || !JToken.DeepEquals(proposal["arguments"], args) || proposal["target"] is not JObject ||
                string.IsNullOrWhiteSpace((string?)proposal["confirmationToken"])) throw new InvalidOperationException(StudioStrings.Get("Cam.MethodUnavailable"));
            var visible = (JObject)proposal.DeepClone(); visible.Remove("confirmationToken");
            if (!await confirm(visible, token)) return null;
            token.ThrowIfCancellationRequested();
            var result = await confirmed.CallConfirmedToolAsync(open, args, (string)proposal["confirmationToken"]!, token);
            var data = PdmInventory.Data(result);
            if (result.IsError || (bool?)data?["opened"] != true || (string?)data["originalDocumentId"] != revision ||
                data["pdmObjectId"] != null && (string?)data["pdmObjectId"] != pdm || string.IsNullOrWhiteSpace((string?)data?["documentId"]))
                throw new InvalidOperationException(StudioStrings.Get("Cam.MethodUnavailable"));
            revision = (string)data["documentId"]!;
        }
        var inspected = await client.CallToolAsync("topsolid_inspect_cam_method", new JObject { ["pdmObjectId"] = pdm }, token);
        token.ThrowIfCancellationRequested();
        var verified = PdmInventory.Data(inspected);
        if (inspected.IsError || (bool?)verified?["isDirty"] != false || (string?)verified["pdmObjectId"] != pdm ||
            (string?)verified["methodDocumentId"] != revision) throw new InvalidOperationException(StudioStrings.Get("Cam.MethodUnavailable"));
        return verified;
    }
}
