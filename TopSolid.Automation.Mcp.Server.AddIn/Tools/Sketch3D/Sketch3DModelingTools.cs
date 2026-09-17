using System;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class Sketch3DModelingTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register) => register(ModelingToolFactory.Create(a, "polyline3d"));
    }
}
