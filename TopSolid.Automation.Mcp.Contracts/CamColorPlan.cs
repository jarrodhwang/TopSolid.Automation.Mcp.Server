using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.Mcp.Contracts
{
    public sealed class StudioContextOptions
    {
        public bool Cad { get; set; }
        public bool Cam { get; set; }
        public StudioContextOptions Snapshot() => new StudioContextOptions { Cad = Cad, Cam = Cam };
    }

    public sealed class CamColorRole
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Hex { get; set; } = "";
        [JsonIgnore] public JObject Rgb => new JObject { ["r"] = Convert.ToInt32(Hex.Substring(1, 2), 16),
            ["g"] = Convert.ToInt32(Hex.Substring(3, 2), 16), ["b"] = Convert.ToInt32(Hex.Substring(5, 2), 16) };
    }

    public sealed class CamColorStandard
    {
        public const string BuiltInId = "studio-cam-colors";
        public string Id { get; set; } = BuiltInId;
        public int Version { get; set; } = 1;
        public string Name { get; set; } = "Studio CAM Colors v1";
        public List<CamColorRole> Roles { get; set; } = new List<CamColorRole>();
        public static CamColorStandard Starter() => new CamColorStandard { Roles = new List<CamColorRole> {
            Role("facing", "Facing region", "#00BFFF"), Role("roughing", "Roughing region", "#FF8000"),
            Role("pocket", "Pocket region", "#0066FF"), Role("hole", "Hole / bore", "#FFFF00"),
            Role("contour", "Contour / boundary", "#00CC66"), Role("finish", "Finish surface", "#AA55FF"),
            Role("keep-out", "Keep-out / protected", "#FF0000"), Role("reference", "Reference geometry", "#FF00FF") } };
        private static CamColorRole Role(string key, string label, string hex) => new CamColorRole { Key = key, Label = label, Hex = hex };
        public CamColorStandard Snapshot() => JObject.FromObject(this).ToObject<CamColorStandard>()!;
        public void Validate()
        {
            if (!Regex.IsMatch(Id ?? "", "^[a-z0-9][a-z0-9-]{0,63}$") || Version < 1 || string.IsNullOrWhiteSpace(Name) || Name.Length > 120 ||
                Roles == null || Roles.Count < 1 || Roles.Count > 32) throw new ArgumentException("Invalid CAM color standard.");
            foreach (var role in Roles)
                if (role == null || !Regex.IsMatch(role.Key ?? "", "^[a-z0-9][a-z0-9-]{0,47}$") ||
                    string.IsNullOrWhiteSpace(role.Label) || role.Label.Length > 80 || !Regex.IsMatch(role.Hex ?? "", "^#[0-9A-Fa-f]{6}$"))
                    throw new ArgumentException("Each role needs a stable lowercase key, label and #RRGGBB color.");
            if (Roles.Select(r => r.Key).Distinct(StringComparer.Ordinal).Count() != Roles.Count ||
                Roles.Select(r => r.Hex).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Roles.Count)
                throw new ArgumentException("CAM role keys and RGB colors must be unique.");
            if (Id == BuiltInId && !JToken.DeepEquals(JObject.FromObject(this), JObject.FromObject(Starter())))
                throw new ArgumentException("The built-in palette is immutable. Save edits as a custom palette.");
        }
    }

    public sealed class CamColorAssignment
    {
        public string TargetKey { get; set; } = "";
        public string RoleKey { get; set; } = "";
        public string Group { get; set; } = "";
        public string Reason { get; set; } = "";
    }

    // Geometry rows are server receipts, never model-produced geometry. Native SDK types stay in the server.
    public sealed class CamColorPlan
    {
        public const int MaximumFaces = 256;
        public const int MaximumElements = 32;
        public string DocumentId { get; set; } = "";
        public string DocumentName { get; set; } = "";
        public JObject? Workpiece { get; set; }
        public JObject? ModelingStage { get; set; }
        [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
        public CamColorStandard Palette { get; set; } = CamColorStandard.Starter();
        public JArray Geometry { get; set; } = new JArray();
        public List<CamColorAssignment> Assignments { get; set; } = new List<CamColorAssignment>();
        public JObject ToArguments()
        {
            Palette.Validate();
            if (string.IsNullOrWhiteSpace(DocumentId) || Assignments.Count == 0) throw new ArgumentException("Select at least one geometry target.");
            if (Assignments.Select(a => a.TargetKey).Distinct(StringComparer.Ordinal).Count() != Assignments.Count)
                throw new ArgumentException("Each exact target, including a whole sketch, may have only one role.");
            var rows = Geometry.OfType<JObject>().ToDictionary(r => (string)r["key"]!, StringComparer.Ordinal);
            var targets = new JArray();
            foreach (var assignment in Assignments)
            {
                if (!rows.TryGetValue(assignment.TargetKey, out var row) || (bool?)row["colorSupported"] != true ||
                    !Palette.Roles.Any(r => r.Key == assignment.RoleKey)) throw new ArgumentException("Unknown or unsupported color target or role.");
                if (assignment.Group.Length > 80 || assignment.Reason.Length > 600) throw new ArgumentException("Color group or reason is too long.");
                targets.Add(new JObject { ["target"] = row["target"]!.DeepClone(), ["fingerprint"] = row["fingerprint"]!.DeepClone(),
                    ["originalColor"] = row["color"]!.DeepClone(), ["roleKey"] = assignment.RoleKey, ["group"] = assignment.Group });
            }
            ValidateCounts(targets);
            var result = new JObject { ["documentId"] = DocumentId, ["palette"] = JObject.FromObject(Palette), ["targets"] = targets };
            if (Workpiece != null) result["workpiece"] = Workpiece.DeepClone();
            if (ModelingStage != null) result["modelingStage"] = ModelingStage.DeepClone();
            return result;
        }
        public static void ValidateCounts(JArray targets)
        {
            var faces = targets.Count(t => t["target"]?["face"] != null);
            if (targets.Count == 0 || faces > MaximumFaces || targets.Count - faces > MaximumElements)
                throw new ArgumentException("Review at most 256 faces and 32 whole entities per batch. No automatic splitting is performed.");
        }
    }
}
