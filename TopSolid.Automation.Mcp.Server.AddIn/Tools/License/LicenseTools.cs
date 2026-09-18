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
            register(new ToolDefinition("topsolid_list_active_licenses", "List active TopSolid licenses with validity, expiration date, active state, type, user, status and version. Does not change license allocation. A missing expiration date means the API supplied no date.", Schema.Page(new JObject()),
                p => a.Read("kernel", () => AutomationValues.Page(TopSolidHost.Licenses.GetActveLicenses(), p,
                    license => JObject.FromObject(AutomationGateway.LicenseInfo(license,
                        AutomationGateway.LicenseValidity(license.Module, TopSolidHost.Application.IsLicenseValid))))),
                "License", api: ApiRefs.Kernel("ILicenses.GetActveLicenses", "IApplication.IsLicenseValid", "License")));
        }
    }
}
