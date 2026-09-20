using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Settings;

internal sealed class CamMethodCatalog(string directory)
{
    private string FilePath => Path.Combine(directory, "cam-methods.json");
    internal List<CamMethodDefinition> Load()
    {
        if (!File.Exists(FilePath)) return [];
        if (new FileInfo(FilePath).Length > 512 * 1024) throw new InvalidDataException("CAM method catalog is too large.");
        using var reader = new JsonTextReader(new StringReader(File.ReadAllText(FilePath))) { MaxDepth = 12 };
        var data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if ((int?)data["version"] != 1 || reader.Read()) throw new InvalidDataException("Unsupported CAM method catalog.");
        var methods = data["methods"]?.ToObject<List<CamMethodDefinition>>() ?? throw new InvalidDataException("Missing methods.");
        Validate(methods); return methods;
    }
    internal void Save(IReadOnlyList<CamMethodDefinition> methods)
    {
        Validate(methods);
        AtomicWrite(FilePath, new JObject { ["version"] = 1, ["methods"] = JArray.FromObject(methods) }, 512 * 1024);
    }
    internal static void Validate(IReadOnlyList<CamMethodDefinition> methods)
    {
        if (methods.Count > 32 || methods.Any(m => m == null) || methods.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() != methods.Count)
            throw new ArgumentException("Register at most 32 distinct CAM methods.");
        foreach (var method in methods) method.Validate();
        var remaining = methods.ToList(); var done = new HashSet<string>(StringComparer.Ordinal);
        while (remaining.Count > 0)
        {
            var ready = remaining.Where(m => m.AfterMethods.All(done.Contains)).ToArray();
            if (ready.Length == 0) throw new ArgumentException("Method prerequisites contain a cycle or an unregistered method.");
            foreach (var method in ready) { done.Add(method.Id); remaining.Remove(method); }
        }
    }
    internal static void AtomicWrite(string path, JObject data, int limit)
    {
        var bytes = Encoding.UTF8.GetBytes(data.ToString(Formatting.Indented));
        if (bytes.Length > limit) throw new InvalidDataException("CAM data is too large.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) { file.Write(bytes); file.Flush(true); }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
