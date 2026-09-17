using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Cam.NC.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class NcDetailsTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_read_nc_code", "Read up to 100 lines of an existing NC file entity using the API starting-line index. Does not postprocess, export, or transmit NC code.",
                new JObject { ["element"] = Schema.Element(), ["startingLine"] = Schema.Integer("API starting-line index, default 0.", 0, 10000000), ["count"] = Schema.Integer("Maximum lines, default 25.", 1, 100) },
                p => a.Read("cam", () =>
                {
                    var id = a.Element(p); var start = (int?)p["startingLine"] ?? 0; var count = (int?)p["count"] ?? 25;
                    var size = TopSolidCamHost.NCFiles.GetNCCodesSize(id);
                    return new JObject { ["startingLine"] = start, ["total"] = size,
                        ["lines"] = start >= size ? new JArray() : new JArray(TopSolidCamHost.NCFiles.GetNCCodesPartial(id, Math.Min(count, size - start), start)), ["hasMore"] = start + count < size };
                }), "Cam/Nc", new[] { "element" }, true, ApiRefs.Cam("INCFiles.GetNCCodesSize", "INCFiles.GetNCCodesPartial")));
        }
    }
}
