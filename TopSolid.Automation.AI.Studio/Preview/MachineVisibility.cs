using System.IO;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.AI.Studio.Preview;

internal sealed record MachineElement(string Name, IReadOnlyList<int> Geometry, IReadOnlyList<MachineElement> Children);

/// <summary>Export-scoped display choices. Geometry remains immutable and is never sent back to TopSolid.</summary>
internal sealed class MachineVisibility
{
    internal IReadOnlyList<MachineElement> Roots { get; }
    private readonly IReadOnlyDictionary<int, PreviewScene> components;
    private readonly HashSet<int> machineIds;
    internal HashSet<int> Hidden { get; } = [];
    internal MachineVisibility(IReadOnlyList<MachineElement> roots, IReadOnlyDictionary<int, PreviewScene> components, IEnumerable<int> machineIds)
    { Roots = roots; this.components = components; this.machineIds = machineIds.ToHashSet(); }
    internal PreviewScene Compose(PreviewScene work)
    {
        var scenes = new[] { work }.Concat(components.Where(p => !machineIds.Contains(p.Key) || !Hidden.Contains(p.Key)).Select(p => p.Value)).Where(s => s.Triangles > 0).ToArray();
        return scenes.Length == 0 ? work : PreviewScene.Combine(scenes);
    }
    internal void Apply(IEnumerable<int> hidden) { Hidden.Clear(); Hidden.UnionWith(hidden.Where(machineIds.Contains)); }

    internal static MachineVisibility FromExport(JArray nodes, int machineRoot, IReadOnlyDictionary<int, PreviewScene> components)
    {
        var machine = Descendants(machineRoot).Where(components.ContainsKey).ToHashSet();
        var used = new HashSet<int>();
        var traversed = new HashSet<int>();
        var aliases = machine.GroupBy(Key).ToDictionary(g => g.Key, g => g.ToArray());
        var nativeRoots = nodes.OfType<JObject>().Where(n => (string?)n["name"] == "CamMachine").ToArray();
        var roots = new List<MachineElement>();
        if (nativeRoots.Length == 1)
            foreach (var child in nativeRoots[0]["children"] as JArray ?? [])
                if (Alias((int)child, 0) is { } item) roots.Add(item);
        // Preserve every rendered component if an older exporter has no matching native group.
        foreach (var id in machine.Where(i => !used.Contains(i))) roots.Add(new MachineElement(Name(id), [id], []));
        return new MachineVisibility(roots, components, machine);

        string Key(int id) => ((int?)nodes[id]["mesh"]) + "\n" + (string?)nodes[id]["name"];
        string Name(int id) => (string?)nodes[id]["name"] ?? "Component " + id;
        MachineElement? Alias(int id, int depth)
        {
            if (depth > 64 || id < 0 || id >= nodes.Count || !traversed.Add(id)) throw new InvalidDataException("Invalid machine display hierarchy.");
            var children = (nodes[id]["children"] as JArray ?? []).Select(c => Alias((int)c, depth + 1)).OfType<MachineElement>().ToArray();
            var own = aliases.TryGetValue(Key(id), out var matches) && matches.Length == 1 && used.Add(matches[0]) ? matches : [];
            var geometry = own.Concat(children.SelectMany(c => c.Geometry)).Distinct().ToArray();
            return geometry.Length == 0 ? null : new MachineElement(Name(id), geometry, children);
        }
        IEnumerable<int> Descendants(int root)
        {
            var seen = new HashSet<int>(); var queue = new Queue<int>(); queue.Enqueue(root);
            while (queue.TryDequeue(out var id))
            {
                if (id < 0 || id >= nodes.Count || !seen.Add(id)) throw new InvalidDataException("Invalid machine display hierarchy.");
                yield return id;
                foreach (var child in nodes[id]["children"] as JArray ?? []) queue.Enqueue((int)child);
            }
        }
    }
}
