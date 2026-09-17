using System;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class Design3DModelingTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register) => register(ModelingToolFactory.Create(a, "extruded_rectangle"));
    }
}
