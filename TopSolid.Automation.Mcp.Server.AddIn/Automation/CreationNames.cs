using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    // Resolve names once for the whole proposal. The registry rechecks the same
    // plan before execution; model arguments and the confirmation token stay intact.
    internal sealed class CreationNames
    {
        private static readonly HashSet<string> Single = new HashSet<string>(StringComparer.Ordinal) {
            "topsolid_create_cylinder", "topsolid_create_rectangle2d", "topsolid_create_circle2d", "topsolid_create_polyline3d",
            "topsolid_create_extruded_rectangle", "topsolid_create_contour2d", "topsolid_create_sketch_profiles",
            "topsolid_create_heart_sketch", "topsolid_create_sketch3d_curves", "topsolid_extrude_sketch", "topsolid_revolve_sketch", "topsolid_loft_sketch" };
        private static readonly Dictionary<string, string> Batches = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["topsolid_create_sketches2d"] = "sketches", ["topsolid_extrude_sections"] = "features", ["topsolid_revolve_sections"] = "features",
            ["topsolid_create_points2d"] = "points", ["topsolid_create_points3d"] = "points", ["topsolid_create_parameters"] = "parameters",
            ["topsolid_create_parameter_expressions"] = "parameters", ["topsolid_create_entity_folders"] = "folders" };
        internal static readonly string[] Api = ApiRefs.Kernel("IElements.GetElements", "IElements.GetName", "IElements.GetFriendlyName", "IElements.SetName");
        internal const string Description = "Omit unless the user requests a name; TopSolid then assigns its normal display name. Requested names are made unique with _1, _2, etc. The confirmation and receipt show the assigned name.";
        internal JObject Arguments { get; private set; }
        internal JObject Receipt { get; private set; }
        internal static bool Supports(string tool) => Single.Contains(tool) || Batches.ContainsKey(tool);

        internal static CreationNames Resolve(string tool, JObject arguments, Func<IEnumerable<string>> loadNames)
        {
            if (!Supports(tool)) return null;
            var copy = (JObject)arguments.DeepClone();
            var entries = Single.Contains(tool) ? new[] { copy } : ((JArray)copy[Batches[tool]]).Cast<JObject>().ToArray();
            var named = entries.Where(p => p["name"] != null).ToArray();
            if (named.Length == 0) return null; // No native name reads or writes for automatic naming.
            foreach (var p in named) Validate((string)p["name"]);
            // Include all internal names, not just SearchByName results: that API
            // excludes elements whose names are not natively required to be unique.
            var used = new HashSet<string>(loadNames().Where(n => !string.IsNullOrEmpty(n)), StringComparer.OrdinalIgnoreCase);
            var rows = new JArray();
            foreach (var p in named) {
                var requested = (string)p["name"];
                var allocated = Allocate(requested, used, tool == "topsolid_create_cylinder" ? 100 : 128);
                var path = p["name"].Path;
                p["name"] = allocated;
                rows.Add(new JObject { ["argument"] = path, ["requestedName"] = requested, ["assignedName"] = allocated, ["adjusted"] = requested != allocated });
            }
            return new CreationNames { Arguments = copy, Receipt = new JObject {
                ["policy"] = "Unnamed geometry keeps TopSolid automatic names. Explicit new names are reserved uniquely across the document and this batch; existing elements are never renamed or reused.",
                ["assignments"] = rows } };
        }
        private static void Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value[0] == '$' || value.Any(char.IsControl))
                throw new ArgumentException("Use a non-blank user element name of at most 128 characters, without control characters or the reserved $ prefix; omit name for automatic naming.");
        }
        internal static string Allocate(string requested, HashSet<string> used, int maximumLength = 128)
        {
            Validate(requested);
            if (used.Add(requested)) return requested;
            var stem = requested; long suffix = 1;
            var split = requested.LastIndexOf('_');
            if (split > 0 && long.TryParse(requested.Substring(split + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0 && number < long.MaxValue) {
                stem = requested.Substring(0, split); suffix = number + 1;
            }
            // At most used.Count+1 candidates can all be occupied. Bound to the
            // snapshot size, not a fixed small suffix limit or a network retry loop.
            var attempts = used.Count + 1;
            for (var i = 0; i < attempts; i++) {
                var tail = "_" + suffix.ToString(CultureInfo.InvariantCulture);
                var length = Math.Min(stem.Length, maximumLength - tail.Length);
                if (length > 0 && char.IsHighSurrogate(stem[length - 1])) length--;
                var candidate = stem.Substring(0, length) + tail;
                if (used.Add(candidate)) return candidate;
                if (suffix == long.MaxValue) { stem = requested; suffix = 1; } else suffix++;
            }
            throw new InvalidOperationException("Unable to reserve a unique element name.");
        }
        internal static void SetAndVerify(IElements api, ElementId id, string name)
        {
            if (name == null) return;
            api.SetName(id, name);
            if (!string.Equals(api.GetName(id), name, StringComparison.Ordinal))
                throw new InvalidOperationException("The created element name differs from the confirmed name; rolling back.");
        }
    }

    internal sealed partial class AutomationGateway
    {
        internal CreationNames ResolveCreationNames(string tool, JObject arguments) => CreationNames.Resolve(tool, arguments, () => {
            ConnectModule("kernel");
            var document = Document(arguments);
            return TopSolidHost.Elements.GetElements(document).Select(TopSolidHost.Elements.GetName).ToArray();
        });
    }
}
