// Generated from the reviewed allowlist in scripts/ApiReference/generate_read_tools.py.
// Each call is compiled against the matched 7.20 SDK. No generic API invocation.
using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;
using TopSolid.Cad.Design.Automating;
using TopSolid.Cad.Drafting.Automating;
using TopSolid.Cad.Electrode.Automating;
using TopSolid.Cam.NC.Kernel.Automating;
using TopSolid.Cae.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class DomainReadTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            DocumentsReadTools.Register(a, register);
            PdmReadTools.Register(a, register);
            EntitiesReadTools.Register(a, register);
            Sketch2DReadTools.Register(a, register);
            Design2DReadTools.Register(a, register);
            Sketch3DReadTools.Register(a, register);
            Design3DReadTools.Register(a, register);
            AssemblyReadTools.Register(a, register);
            ToolingReadTools.Register(a, register);
            DraftingReadTools.Register(a, register);
            ElectrodeReadTools.Register(a, register);
            CamPartSetupReadTools.Register(a, register);
            CamMachineReadTools.Register(a, register);
            CamOperationReadTools.Register(a, register);
            CamCuttingConditionsReadTools.Register(a, register);
            CamPostprocessorReadTools.Register(a, register);
            CamNcReadTools.Register(a, register);
            CamSimulationReadTools.Register(a, register);
            CaeReadTools.Register(a, register);
        }
    }
}
