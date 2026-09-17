using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools.System
{
    internal static class StatusTools
    {
        public static ToolDefinition Create(AutomationGateway automation)
        {
            return new ToolDefinition("topsolid_get_status",
                "Check whether the MCP server can connect to an existing TopSolid Automation host and query its version. Does not start TopSolid. Use for questions about connection or availability.",
                new JObject(), arguments => automation.GetStatus(), api: ApiRefs.Kernel("TopSolidHost.Connect", "TopSolidHost.IsConnected", "IApplication.Version", "TopSolidHost.ClientVersion"));
        }
    }
}
