using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Chat;

internal static class CamAutomationGeometry
{
    internal static async Task<CamAutomationPlan?> Inspect(string request, Func<string, JObject, Task<JObject>> read,
        Func<JArray, string, string, Task<JObject?>> choose)
    {
        var args = new JObject();
        if (!Regex.IsMatch(request, @"\b(this|current|active)\b|현재|활성|이\s*(?:부품|파트|문서)", RegexOptions.IgnoreCase))
        {
            var docs = new JArray(); int offset = 0;
            while (true)
            {
                var page = await read("topsolid_list_document_summaries", new JObject { ["scope"] = "open", ["offset"] = offset, ["limit"] = 100 });
                if (page["items"] is not JArray items) throw Incomplete();
                foreach (var row in items.OfType<JObject>().Where(r => ((string?)r["type"] ?? "").StartsWith("TopSolid.Cam.NC.", StringComparison.Ordinal) &&
                    !((string?)r["type"] ?? "").Contains("Method", StringComparison.Ordinal))) docs.Add(row.DeepClone());
                if ((bool?)page["hasMore"] == false) break;
                if ((int?)page["nextOffset"] is not int next || next <= offset || next > 2000) throw Incomplete(); offset = next;
            }
            var selected = await choose(docs, "document", "Cam.ChooseDocument"); if (selected == null) return null; args["documentId"] = selected["documentId"];
        }
        var first = await read(CamColorPreparation.Inspect, args);
        var plan = new CamAutomationPlan { DocumentId = (string?)first["documentId"] ?? "", DocumentName = (string?)first["name"] ?? "" };
        if (plan.DocumentId.Length == 0) throw Incomplete(); args["documentId"] = plan.DocumentId;
        var stages = await read("topsolid_get_cam_stages", new JObject { ["documentId"] = plan.DocumentId });
        if ((bool?)stages["isCam"] != true || stages["modelingStage"] is not JObject modeling) throw new InvalidOperationException(StudioStrings.Get("Cam.SelectCamDocument"));
        plan.ModelingStage = (JObject)modeling.DeepClone();
        var machining = new JArray((stages["stages"] as JArray ?? []).OfType<JObject>().Where(s => (bool?)s["machining"] == true));
        var stage = machining.Count == 1 ? machining[0] as JObject : await choose(machining, "stage", "Cam.ChooseStage");
        if (stage?["element"] is not JObject nativeStage) return null; plan.MachiningStage = (JObject)nativeStage.DeepClone();
        plan.MachiningStageName = (string?)stage["name"] ?? "";
        if ((bool?)first["needsWorkpiece"] == true)
        {
            var parts = first["workpieces"] as JArray ?? [];
            var selected = parts.Count == 1 ? parts[0] as JObject : await choose(parts, "part", "Color.ChooseWorkpiece");
            if (selected?["element"] is not JObject part) return null;
            args["workpiece"] = part.DeepClone(); plan.Workpiece = (JObject)part.DeepClone(); first = await read(CamColorPreparation.Inspect, args);
            plan.WorkpieceName = (string?)selected["name"] ?? "";
        }
        if (plan.Workpiece == null) throw Incomplete();
        int? total = null; int cursor = 0;
        while (true)
        {
            var page = cursor == 0 ? first : await read(CamColorPreparation.Inspect, args);
            if ((string?)page["documentId"] != plan.DocumentId || page["items"] is not JArray rows || (int?)page["offset"] != cursor || page["total"]?.Type != JTokenType.Integer || page["hasMore"]?.Type != JTokenType.Boolean) throw Incomplete();
            int count = (int)page["total"]!; total ??= count;
            if (count != total || count > 512) throw new InvalidOperationException(StudioStrings.Get("Color.NarrowScope"));
            foreach (var row in rows)
            {
                if (row is not JObject value || value["target"] is not JObject || value["key"]?.Type != JTokenType.String || value["fingerprint"]?.Type != JTokenType.String ||
                    value["geometryFingerprint"]?.Type != JTokenType.String || plan.Geometry.Any(g => (string?)g["key"] == (string?)row["key"])) throw Incomplete();
                plan.Geometry.Add(row.DeepClone());
            }
            if ((bool)page["hasMore"]! == false) { if (plan.Geometry.Count != count) throw Incomplete(); break; }
            if (rows.Count == 0 || (int?)page["nextOffset"] != cursor + rows.Count) throw Incomplete(); cursor += rows.Count; args["offset"] = cursor;
        }
        return plan;
    }
    internal static async Task Refresh(CamAutomationPlan plan, CamAutomationStep step, Func<string, JObject, Task<JObject>> read, bool colorsMayHaveChanged)
    {
        var current = new JArray();
        foreach (var batch in step.Colors.Geometry.OfType<JObject>().Chunk(20))
        {
            var page = await read(CamColorPreparation.Inspect, new JObject { ["documentId"] = plan.DocumentId, ["workpiece"] = plan.Workpiece?.DeepClone(), ["targets"] = new JArray(batch.Select(r => r["target"]!.DeepClone())) });
            if ((string?)page["documentId"] != plan.DocumentId || page["items"] is not JArray rows || rows.Count != batch.Length) throw Incomplete();
            for (int i = 0; i < rows.Count; i++)
            {
                var prior = batch[i]; var now = rows[i];
                if (!JToken.DeepEquals(prior["target"], now["target"]) || (bool?)now["colorSupported"] != true || prior["geometryFingerprint"]?.Type != JTokenType.String ||
                    !JToken.DeepEquals(prior["geometryFingerprint"], now["geometryFingerprint"]) || (!colorsMayHaveChanged && !JToken.DeepEquals(prior["color"], now["color"])))
                    throw new InvalidOperationException(StudioStrings.Get("Cam.Stale"));
                current.Add(now.DeepClone());
            }
        }
        step.Colors.Geometry = current;
    }
    internal static void Rebase(CamAutomationPlan plan, string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId)) throw Incomplete();
        var original = plan.DocumentId; plan.DocumentId = documentId;
        void Handles(JContainer value) {
            foreach (var obj in value.DescendantsAndSelf().OfType<JObject>()) if ((string?)obj["documentId"] == original) obj["documentId"] = documentId;
        }
        if (plan.Workpiece != null) Handles(plan.Workpiece); if (plan.MachiningStage != null) Handles(plan.MachiningStage); if (plan.ModelingStage != null) Handles(plan.ModelingStage);
        Handles(plan.Geometry);
        foreach (JObject row in plan.Geometry) row["key"] = row["target"]!.ToString(Formatting.None);
        foreach (var step in plan.Steps)
        {
            step.Colors.DocumentId = documentId; step.Colors.Workpiece = (JObject?)plan.Workpiece?.DeepClone();
            step.Colors.ModelingStage = (JObject?)plan.ModelingStage?.DeepClone();
            Handles(step.Colors.Geometry);
            foreach (JObject row in step.Colors.Geometry)
            {
                var old = (string)row["key"]!; var key = row["target"]!.ToString(Formatting.None);
                foreach (var assignment in step.Colors.Assignments.Where(a => a.TargetKey == old)) assignment.TargetKey = key;
                row["key"] = key;
            }
        }
    }
    internal static bool IsApplied(CamAutomationStep step) => step.Colors.Assignments.All(a => {
        var row = step.Colors.Geometry.OfType<JObject>().Single(r => (string?)r["key"] == a.TargetKey);
        return JToken.DeepEquals(row["color"], step.Method.Colors.Roles.Single(r => r.Key == a.RoleKey).Rgb);
    });
    internal static bool UnsafeRecolor(CamAutomationPlan plan, CamAutomationStep next)
    {
        var changed = next.Colors.Assignments.Where(a => !JToken.DeepEquals(next.Colors.Geometry.Single(r => (string?)r["key"] == a.TargetKey)["color"], next.Method.Colors.Roles.Single(r => r.Key == a.RoleKey).Rgb)).ToArray();
        if (changed.Length == 0) return false;
        // Color lookup can gain as well as lose targets. Until a native method proves
        // fixed selection, any recolor in its lookup document can affect associativity.
        return plan.Steps.TakeWhile(s => s.Id != next.Id).Any(s => s.Included && (s.Status is "calculated" or "pending" or "deferred") &&
            (s.Method.Options.KeepAssociativity || s.Method.Options.LaunchDeferred || s.Status != "calculated"));
    }
    private static InvalidOperationException Incomplete() => new(StudioStrings.Get("Color.Incomplete"));
}
