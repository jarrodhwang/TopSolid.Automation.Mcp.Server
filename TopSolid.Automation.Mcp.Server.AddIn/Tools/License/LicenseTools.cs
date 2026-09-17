using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class LicenseTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            register(new ToolDefinition("topsolid_list_active_licenses", "List active TopSolid license modules and status. Does not change license allocation.", Schema.Page(new JObject()),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Licenses.GetActveLicenses(), p,
                    license => new JObject { ["name"] = license.Name, ["module"] = license.Module, ["version"] = license.Version,
                        ["type"] = license.LicenseType.ToString(), ["active"] = license.IsActive, ["status"] = license.Status })),
                "License", api: ApiRefs.Kernel("ILicenses.GetActveLicenses", "License")));
        }
    }
}
