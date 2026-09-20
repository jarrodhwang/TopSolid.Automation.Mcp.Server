using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Preview;

/// <summary>Per-review display cache. Keys come exclusively from verified native face identities.</summary>
internal sealed class CamFaceGeometry
{
    private readonly Dictionary<string, PreviewScene> faces = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> owners = new(StringComparer.Ordinal);
    private readonly HashSet<string> explicitFaceColors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> unavailable = new(StringComparer.Ordinal);
    internal int MappedFaceCount => faces.Count;
    internal string? UnavailableReason(JObject row) => unavailable.GetValueOrDefault((string?)row["key"] ?? "");
    internal bool HasTarget(JObject row) => (string?)row["kind"] == "face" ? faces.ContainsKey((string)row["key"]!) :
        (string?)row["kind"] == "shape" && (int?)row["geometry"]?["faceCount"] > 0 && owners.Values.Count(v => v == row["target"]!.ToString(Formatting.None)) == (int?)row["geometry"]?["faceCount"];
    internal static async Task<CamFaceGeometry> Load(IGraphicPreviewClient client, CamAutomationPlan plan, CancellationToken token)
    {
        var result = new CamFaceGeometry(); var rows = plan.Geometry.OfType<JObject>().Where(r => r["target"]?["face"] is JObject).ToArray();
        if (rows.Length == 0 || rows.Length > 512) throw new InvalidDataException("A complete native face preview requires 1–512 inspected faces.");
        var triangles = 0;
        foreach (var batch in rows.Chunk(32))
        {
            var transfer = await client.GetGraphicPreviewDataAsync(new JObject { ["camFaceGeometry"] = true, ["documentId"] = plan.DocumentId,
                ["workpiece"] = plan.Workpiece?.DeepClone(), ["faces"] = new JArray(batch.Select(r => r["target"]!["face"]!.DeepClone())) }, token);
            if ((string?)transfer.Metadata["status"] != "ready" || (string?)transfer.Metadata["format"] != "cam-faces-v1" ||
                (string?)transfer.Metadata["documentId"] != plan.DocumentId || transfer.Bytes == null || transfer.Bytes.Length > 64 * 1024 * 1024)
                throw new InvalidDataException((string?)transfer.Metadata["reason"] ?? "Verified native face preview is unavailable.");
            var decoded = await Task.Run(() => Decode(transfer.Bytes, plan.DocumentId, batch, token), token);
            foreach (var face in decoded)
            {
                if (face.Scene == null) { result.unavailable.Add(face.Key, face.Reason ?? "Native display mapping unavailable."); continue; }
                triangles = checked(triangles + face.Scene.Triangles);
                if (triangles > PreviewScene.MaximumTriangles) throw new InvalidDataException("Reduce the geometry preview scope.");
                result.faces.Add(face.Key, face.Scene); result.owners.Add(face.Key, face.Owner);
                if (face.HasExplicitColor) result.explicitFaceColors.Add(face.Key);
            }
        }
        return result;
    }
    internal sealed record FaceScene(string Key, string Owner, PreviewScene? Scene, bool HasExplicitColor, string? Reason = null);
    internal static IReadOnlyList<FaceScene> Decode(byte[] bytes, string document, IReadOnlyList<JObject> expected, CancellationToken token)
    {
        using var reader = new JsonTextReader(new StringReader(Encoding.UTF8.GetString(bytes))) { MaxDepth = 16, DateParseHandling = DateParseHandling.None };
        var data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (reader.Read() || (int?)data["version"] != 1 || (string?)data["documentId"] != document || (string?)data["units"] != "m" || data["items"] is not JArray items || items.Count != expected.Count)
            throw new InvalidDataException("Native face preview protocol mismatch.");
        var result = new List<FaceScene>(); int triangles = 0;
        for (int n = 0; n < items.Count; n++)
        {
            token.ThrowIfCancellationRequested(); var item = items[n]; var row = expected[n];
            if ((string?)item["key"] != (string?)row["key"] || (string?)item["fingerprint"] != (string?)row["fingerprint"] ||
                !JToken.DeepEquals(item["face"], row["target"]?["face"]) || !JToken.DeepEquals(item["color"], row["color"]))
                throw new InvalidDataException("Stale or unverified native face mapping.");
            var owner = new JObject { ["element"] = item["face"]!["element"]!.DeepClone() }.ToString(Formatting.None);
            if ((string?)item["status"] == "unavailable") {
                var reason = (string?)item["reason"];
                if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) throw new InvalidDataException("Missing unavailable-face reason.");
                result.Add(new FaceScene((string)row["key"]!, owner, null, false, reason)); continue;
            }
            if (item["positions"] is not JArray p || p.Count < 9 || p.Count % 3 != 0 || p.Count > 2250000 ||
                item["indices"] is not JArray indices || indices.Count == 0 || indices.Count % 3 != 0 || indices.Count > 750000)
                throw new InvalidDataException("Stale or unverified native face mapping.");
            triangles += indices.Count / 3; if (triangles > 250000) throw new InvalidDataException("Oversized native face batch.");
            var points = new Point3D[p.Count / 3];
            if (p.Any(v => v.Type is not (JTokenType.Integer or JTokenType.Float) || !double.IsFinite((double)v)) ||
                indices.Any(i => i.Type != JTokenType.Integer || (long)i < 0 || (long)i >= points.Length)) throw new InvalidDataException("Invalid native preview coordinates or triangle indices.");
            for (int i = 0; i < points.Length; i++) points[i] = new Point3D((double)p[i * 3] * 1000, (double)p[i * 3 + 1] * 1000, (double)p[i * 3 + 2] * 1000);
            var color = item["displayColor"] ?? item["color"];
            var rgb = color?["r"] != null ? Color.FromRgb((byte)color["r"]!, (byte)color["g"]!, (byte)color["b"]!) : TopSolidPreviewPalette.Surface;
            var scene = PreviewScene.Build([new PreviewMesh(points, indices.Select(i => (int)i).ToArray(), rgb)], token);
            result.Add(new FaceScene((string)row["key"]!, owner, scene, row["color"]?["r"] != null));
        }
        return result;
    }
    internal PreviewScene Scene(CamColorPlan? colors, bool original)
    {
        var assignments = original || colors == null ? new Dictionary<string, string>() : colors.Assignments.ToDictionary(a => a.TargetKey, a => a.RoleKey);
        var palette = colors?.Palette.Roles.ToDictionary(r => r.Key, r => (Color)ColorConverter.ConvertFromString(r.Hex));
        return PreviewScene.Combine(faces.Select(pair => {
            if (assignments.TryGetValue(pair.Key, out var role) || !explicitFaceColors.Contains(pair.Key) && assignments.TryGetValue(owners[pair.Key], out role)) return pair.Value.Recolor(palette![role]);
            return pair.Value;
        }).ToArray());
    }
}
