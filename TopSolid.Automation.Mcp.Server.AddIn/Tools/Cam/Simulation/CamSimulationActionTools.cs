using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Cam.NC.Kernel.Automating;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class CamSimulationActionTools
    {
        private const int DefaultVerificationIncrement = 10;

        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            var simulation = DocumentActionTools.Target();
            simulation["element"] = Schema.Element();
            simulation["animationSpeed"] = Schema.Integer("Optional animation speed on the native [0;100] scale. Omit to retain TopSolid's current speed.", 0, 100);
            register(new ToolDefinition("topsolid_simulate_cam_operation",
                "Open TopSolid's CAM simulation for one existing operation and start its animation. Requires confirmation because the documented workflow marks the target document dirty. This does not generate NC code, run a machine or prove collision safety.",
                simulation,
                p => a.Modify(p, "simulate CAM operation", "cam", (doc, current) =>
                {
                    var operation = RequireOperation(a.Element(current));
                    TopSolidCamHost.Simulation.Open(operation);
                    try
                    {
                        if (current["animationSpeed"] != null)
                            TopSolidCamHost.Simulation.SetAnimationSpeed((int)current["animationSpeed"]);
                        TopSolidCamHost.Simulation.StartAnimation();
                        var elapsed = CamAnimationLifecycle.WaitForCompletion(TopSolidCamHost.Simulation.IsSimulationComplete);
                        return new JObject
                        {
                            ["mode"] = "simulation",
                            ["started"] = true,
                            ["completed"] = true,
                            ["operation"] = AutomationValues.Json(operation),
                            ["operationName"] = CamNames.OperationName(new ElementExId(operation)),
                            ["animationSpeed"] = TopSolidCamHost.Simulation.GetAnimationSpeed(),
                            ["simulationComplete"] = true,
                            ["animationElapsedMilliseconds"] = elapsed.TotalMilliseconds,
                            ["ncGenerated"] = false,
                            ["machineStarted"] = false,
                            ["collisionSafetyVerified"] = false
                        };
                    }
                    catch (Exception failure)
                    {
                        try { TopSolidCamHost.Simulation.Close(); }
                        catch (Exception closeFailure)
                        {
                            throw new AggregateException("CAM simulation failed and could not be closed before the document rollback.", failure, closeFailure);
                        }
                        throw;
                    }
                }),
                "Cam/Simulation", new[] { "documentId", "element" }, false,
                ApiRefs.Cam("ISimulation.Open", "ISimulation.SetAnimationSpeed", "ISimulation.StartAnimation", "ISimulation.Close", "ISimulation.GetAnimationSpeed", "ISimulation.IsSimulationComplete", "IOperations.IsOperation")
                    .Concat(ApiRefs.Kernel("IApplication.StartModification", "IApplication.EndModification", "IDocuments.EnsureIsDirty"))
                    .ToArray(),
                MutationReferences.Validate,
                p => PreviewOperation(a, p),
                "Open and animate the selected existing CAM operation, waiting for native animation completion before committing the modification. The target document is made dirty by the documented TopSolid workflow; do not save automatically."));

            var verifyOne = DocumentActionTools.Target();
            verifyOne["element"] = Schema.Element();
            verifyOne["increment"] = IncrementSchema();
            register(new ToolDefinition("topsolid_verify_cam_operation",
                "Open TopSolid's CAM verification for one existing operation and start its animation. The optional increment is a native [0;100] movement factor and defaults to 10. This is not an NC export, machine run or collision-safety certification.",
                verifyOne,
                p => a.Modify(p, "verify CAM operation", "cam", (doc, current) =>
                {
                    var operation = RequireOperation(a.Element(current));
                    var increment = (int?)current["increment"] ?? DefaultVerificationIncrement;
                    TopSolidCamHost.Verify.OpenOne(operation);
                    TopSolidCamHost.Verify.SetIncrement(increment);
                    TopSolidCamHost.Verify.StartAnimation();
                    return new JObject
                    {
                        ["mode"] = "verification",
                        ["started"] = true,
                        ["operation"] = AutomationValues.Json(operation),
                        ["operationName"] = CamNames.OperationName(new ElementExId(operation)),
                        ["increment"] = increment,
                        ["ncGenerated"] = false,
                        ["machineStarted"] = false,
                        ["collisionSafetyVerified"] = false
                    };
                }),
                "Cam/Simulation", new[] { "documentId", "element" }, false,
                ApiRefs.Cam("IVerify.OpenOne", "IVerify.SetIncrement", "IVerify.StartAnimation", "IOperations.IsOperation")
                    .Concat(ApiRefs.Kernel("IApplication.StartModification", "IApplication.EndModification", "IDocuments.EnsureIsDirty"))
                    .ToArray(),
                MutationReferences.Validate,
                p => PreviewOperation(a, p),
                "Open and animate verification for the selected existing CAM operation. The target document is made dirty by the documented TopSolid workflow; do not save automatically."));

            var verifyAll = DocumentActionTools.Target();
            verifyAll["increment"] = IncrementSchema();
            register(new ToolDefinition("topsolid_verify_all_cam_operations",
                "Open TopSolid's CAM verification for every operation in the target document and start its animation. The optional increment is a native [0;100] movement factor and defaults to 10. This is not an NC export, machine run or collision-safety certification.",
                verifyAll,
                p => a.Modify(p, "verify all CAM operations", "cam", (doc, current) =>
                {
                    var increment = (int?)current["increment"] ?? DefaultVerificationIncrement;
                    var operations = TopSolidCamHost.Operations.GetOperations(doc);
                    TopSolidCamHost.Verify.OpenAll(doc);
                    TopSolidCamHost.Verify.SetIncrement(increment);
                    TopSolidCamHost.Verify.StartAnimation();
                    return new JObject
                    {
                        ["mode"] = "verification",
                        ["started"] = true,
                        ["scope"] = "allOperations",
                        ["operationCount"] = operations.Count,
                        ["increment"] = increment,
                        ["ncGenerated"] = false,
                        ["machineStarted"] = false,
                        ["collisionSafetyVerified"] = false
                    };
                }),
                "Cam/Simulation", new[] { "documentId" }, false,
                ApiRefs.Cam("IVerify.OpenAll", "IVerify.SetIncrement", "IVerify.StartAnimation", "IOperations.GetOperations")
                    .Concat(ApiRefs.Kernel("IApplication.StartModification", "IApplication.EndModification", "IDocuments.EnsureIsDirty"))
                    .ToArray(),
                MutationReferences.Validate,
                p => PreviewAll(a, p),
                "Open and animate verification for all CAM operations in the target document. The target document is made dirty by the documented TopSolid workflow; do not save automatically."));
        }

        private static JObject IncrementSchema()
        {
            var schema = Schema.Integer("Movement increment factor used by IVerify.SetIncrement; defaults to 10.", 0, 100);
            schema["default"] = DefaultVerificationIncrement;
            return schema;
        }

        private static ElementId RequireOperation(ElementId id)
        {
            if (!TopSolidCamHost.Operations.IsOperation(new ElementExId(id)))
                throw new ArgumentException("Select an existing CAM operation.");
            return id;
        }

        private static JObject PreviewOperation(AutomationGateway a, JObject arguments)
        {
            var preview = a.PreviewDocument(arguments, "cam");
            var operation = RequireOperation(a.Element(arguments));
            preview["operation"] = AutomationValues.Json(operation);
            preview["operationName"] = CamNames.OperationName(new ElementExId(operation));
            preview["scope"] = "oneOperation";
            return preview;
        }

        private static JObject PreviewAll(AutomationGateway a, JObject arguments)
        {
            var preview = a.PreviewDocument(arguments, "cam");
            preview["scope"] = "allOperations";
            return preview;
        }
    }
}
