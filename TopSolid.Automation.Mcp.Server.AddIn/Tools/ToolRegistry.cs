using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;
using TopSolid.Automation.Mcp.Server.AddIn.Tools.Documents;
using TopSolid.Automation.Mcp.Server.AddIn.Tools.System;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal sealed class ToolRegistry
    {
        private readonly Dictionary<string, ToolDefinition> tools = new Dictionary<string, ToolDefinition>(StringComparer.Ordinal);
        private readonly ConfirmationStore confirmations = new ConfirmationStore();
        private readonly Func<JObject, JObject> preview;
        private readonly Func<string, JObject, CreationNames> resolveCreationNames;
        private readonly Func<JObject, JObject> graphicPreview;
        private readonly Func<JObject> licenseStatus;
        public ToolRegistry(AutomationGateway automation)
        {
            preview = automation.PreviewModeling;
            graphicPreview = automation.GraphicPreview;
            licenseStatus = automation.GetLicenseStatus;
            resolveCreationNames = automation.ResolveCreationNames;
            Register(StatusTools.Create(automation));
            Register(DocumentTools.Active(automation));
            Register(DocumentTools.Info(automation));
            DomainReadTools.Register(automation, Register);
            DocumentBatchReadTools.Register(automation, Register);
            ObjectModelTools.Register(Register);
            DocumentIdentityTools.Register(automation, Register);
            PdmIdentityTools.Register(automation, Register);
            ModelingContextTools.Register(automation, Register);
            PdmLifecycleTools.Register(automation, Register);
            PdmPersistenceTools.Register(automation, Register);
            EntityIdentityTools.Register(automation, Register);
            EntityStructureTools.Register(automation, Register);
            ElementPropertyTools.Register(automation, Register);
            PdmBatchReadTools.Register(automation, Register);
            EntityBatchReadTools.Register(automation, Register);
            ShapeBatchReadTools.Register(automation, Register);
            AssemblyBatchReadTools.Register(automation, Register);
            CamBatchReadTools.Register(automation, Register);
            SketchBatchReadTools.Register(automation, Register);
            EntityBatchActionTools.Register(automation, Register);
            AppearanceTools.Register(automation, Register);
            ParameterBatchTools.Register(automation, Register);
            ParameterReadTools.Register(automation, Register);
            ParameterExpressionTools.Register(automation, Register);
            PointBatchTools.Register(automation, Register);
            SketchBatchActionTools.Register(automation, Register);
            SketchPlanTools.Register(automation, Register);
            HeartSketchTools.Register(automation, Register);
            ShapeBatchActionTools.Register(automation, Register);
            PdmDetailsTools.Register(automation, Register);
            LicenseTools.Register(automation, Register);
            EntityDetailsTools.Register(automation, Register);
            DocumentPropertyTools.Register(automation, Register);
            CamDetailTools.Register(automation, Register);
            NcDetailsTools.Register(automation, Register);
            DraftingDetailsTools.Register(automation, Register);
            CaeDetailsTools.Register(automation, Register);
            DocumentActionTools.Register(automation, Register);
            PdmActionTools.Register(automation, Register);
            PdmCreationContextTools.Register(automation, Register);
            EntityActionTools.Register(automation, Register);
            SketchWorkflowTools.Register(automation, Register);
            ExplicitSectionTools.Register(automation, Register);
            ShapeWorkflowTools.Register(automation, Register);
            CylinderTools.Register(automation, Register);
            ModelingGuideTools.Register(Register);
            AssemblyActionTools.Register(automation, Register);
            CamActionTools.Register(automation, Register);
            CamToolPathTools.Register(automation, Register);
            Sketch2DModelingTools.Register(automation, Register);
            Sketch3DModelingTools.Register(automation, Register);
            Sketch3DCurveTools.Register(automation, Register);
            Design3DModelingTools.Register(automation, Register);
            ReferenceTools.Register(() => tools.Values, Register);
        }
        internal ToolRegistry(IEnumerable<ToolDefinition> definitions, Func<JObject, JObject> preview, Func<string, JObject, CreationNames> resolveCreationNames = null)
        { this.preview = preview; this.resolveCreationNames = resolveCreationNames; foreach (var definition in definitions) Register(definition); }
        private void Register(ToolDefinition tool) { tools.Add(tool.Name, tool); }
        public JArray List()
        {
            var list = new JArray();
            foreach (var tool in tools.Values) list.Add(tool.Definition.DeepClone());
            return list;
        }
        public JObject Prepare(string name, JObject arguments)
        {
            var tool = Validate(name, arguments);
            if (tool.ReadOnly) throw new RpcException(-32602, "This inspection tool does not require confirmation.");
            try { return confirmations.Prepare(tool, arguments, Preview(tool, arguments, out _)); }
            catch (RpcException) { throw; }
            catch (Exception ex) { throw new RpcException(-32011, "Could not prepare this action: " + AutomationGateway.Describe(ex)); }
        }
        public JObject LicenseStatus()
        {
            if (licenseStatus == null) throw new RpcException(-32601, "License inspection is unavailable.");
            // Do not log license user/owner data or credentials. The caller treats any read failure as unverified.
            try { return licenseStatus(); }
            catch { throw new RpcException(-32012, "TopSolid license verification is unavailable. Open TopSolid, wait until it is ready, then restart Studio."); }
        }
        public JObject GraphicPreview(JObject request)
        {
            if (graphicPreview == null) throw new RpcException(-32601, "Graphic previews are unavailable.");
            try { return graphicPreview(request); }
            catch (ArgumentException ex) { throw new RpcException(-32602, ex.Message); }
            catch (Exception ex)
            {
                ServerDiagnosticLog.Write("warning", "preview.unavailable", "The native document graphic preview could not be exported.", ex);
                return new JObject { ["status"] = "unavailable" };
            }
        }
        private JObject Preview(ToolDefinition tool, JObject arguments, out JObject effectiveArguments)
        {
            var names = resolveCreationNames?.Invoke(tool.Name, arguments);
            effectiveArguments = names?.Arguments ?? arguments;
            var target = (tool.Preview ?? preview)(effectiveArguments);
            if (names != null) target["naming"] = names.Receipt.DeepClone();
            return target;
        }
        private ToolDefinition Validate(string name, JObject arguments)
        {
            if (!tools.TryGetValue(name, out var tool)) throw new RpcException(-32602, "Unknown tool: " + name);
            Schema.Validate(arguments, (JObject)tool.Definition["inputSchema"]);
            try { tool.ValidateArguments?.Invoke(arguments); }
            catch (ArgumentException ex) { throw new RpcException(-32602, ex.Message); }
            return tool;
        }
        public JObject Call(string name, JObject arguments, string confirmationToken = null)
        {
            var tool = Validate(name, arguments);
            var approvedTarget = tool.ReadOnly ? null : confirmations.Consume(confirmationToken, name, arguments);
            try
            {
                var effectiveArguments = arguments;
                JObject currentTarget = null;
                if (approvedTarget != null)
                {
                    currentTarget = Preview(tool, arguments, out effectiveArguments);
                    if (!JToken.DeepEquals(approvedTarget, currentTarget))
                        throw new InvalidOperationException("The target state or synchronized document group changed after the preview. No action was executed. Review a new proposal.");
                    // Batch persistence uses this exact rechecked scope instead
                    // of enumerating a potentially larger set a third time.
                    if (tool.ExecutePrepared != null) return Content(tool.ExecutePrepared(effectiveArguments, currentTarget), false);
                }
                var result = tool.Execute(effectiveArguments);
                if (currentTarget?["naming"] != null) result["naming"] = currentTarget["naming"].DeepClone();
                return Content(result, false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Tool " + name + " failed: " + AutomationGateway.Describe(ex));
                return Content(new JObject
                {
                    ["message"] = tool.ReadOnly ? "The TopSolid query failed. Check the document type, IDs, module availability, and TopSolid connection." :
                        "The TopSolid action failed. Undoable document modifications attempt rollback; persistent PDM actions (creation, metadata, deletion, restoration), opening and saving cannot be rolled back by this server. Inspect any partial receipt and TopSolid before another change; do not automatically retry.",
                    ["detail"] = AutomationGateway.Describe(ex),
                    ["partialChange"] = (ex as PartialChangeException)?.Receipt
                }, true);
            }
        }
        private static JObject Content(JObject value, bool isError)
        {
            var text = value.ToString(Formatting.None);
            if (text.Length > 60000)
            {
                text = new JObject { ["message"] = "Result exceeded 60,000 characters. Request a smaller page or narrower query." }.ToString(Formatting.None);
                isError = true;
            }
            return new JObject
            {
                ["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = text }),
                ["isError"] = isError
            };
        }
    }
}
