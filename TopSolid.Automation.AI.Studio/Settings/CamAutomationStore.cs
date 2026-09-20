using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Settings;

internal sealed class CamAutomationStore(string directory)
{
    private string FilePath => Path.Combine(directory, "cam-prepared-plan.json");
    internal CamAutomationPlan? Load()
    {
        if (!File.Exists(FilePath)) return null;
        if (new FileInfo(FilePath).Length > 8 * 1024 * 1024) throw new InvalidDataException("CAM plan is too large.");
        using var reader = new JsonTextReader(new StringReader(File.ReadAllText(FilePath))) { MaxDepth = 24, DateParseHandling = DateParseHandling.None };
        var data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if ((int?)data["version"] != 1 || reader.Read()) throw new InvalidDataException("Unsupported CAM plan.");
        var plan = data["plan"]?.ToObject<CamAutomationPlan>() ?? throw new InvalidDataException("Missing CAM plan.");
        plan.Validate(); return plan;
    }
    internal void Save(CamAutomationPlan plan) => CamMethodCatalog.AtomicWrite(FilePath, new JObject { ["version"] = 1, ["plan"] = JObject.FromObject(plan) }, 8 * 1024 * 1024);
}
