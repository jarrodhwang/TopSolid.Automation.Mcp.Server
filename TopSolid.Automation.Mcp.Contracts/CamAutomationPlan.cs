using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.Mcp.Contracts
{
    public sealed class CamMethodOptions
    {
        public bool LaunchDeferred { get; set; }
        public bool KeepAssociativity { get; set; } = true;
        public bool ManualExecution { get; set; }
        public bool UseCuttingConditions { get; set; } = true;
        public bool ReuseAnswers { get; set; }
        public bool SilentMode { get; set; }
    }

    public sealed class CamMethodDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string PdmObjectId { get; set; } = "";
        public string MethodDocumentId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Process { get; set; } = "";
        public string Conditions { get; set; } = "";
        // Workpiece scope requires an isolated native workpiece until the host exposes
        // a typed ExecuteMethod selection binding. The server verifies that isolation.
        public string SearchScope { get; set; } = "document";
        public string WorkpieceParameter { get; set; } = "";
        public List<string> AfterMethods { get; set; } = new List<string>();
        public CamColorStandard Colors { get; set; } = new CamColorStandard { Id = "method-colors", Name = "Method colors" };
        public CamMethodOptions Options { get; set; } = new CamMethodOptions();
        public List<CamMethodInput> Inputs { get; set; } = new List<CamMethodInput>();
        public CamMethodDefinition Snapshot() => JObject.FromObject(this).ToObject<CamMethodDefinition>()!;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) || Id.Length > 64 || string.IsNullOrWhiteSpace(PdmObjectId) || PdmObjectId.Length > 256 ||
                string.IsNullOrWhiteSpace(MethodDocumentId) || MethodDocumentId.Length > 256 || string.IsNullOrWhiteSpace(Name) || Name.Length > 120 ||
                string.IsNullOrWhiteSpace(Process) || Process.Length > 80 || Conditions == null || Conditions.Length > 4000 || Options == null || Colors == null ||
                SearchScope != "document" && SearchScope != "workpiece" || AfterMethods == null || Inputs == null || AfterMethods.Count > 32 || AfterMethods.Contains(Id) ||
                AfterMethods.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 64) || AfterMethods.Distinct(StringComparer.Ordinal).Count() != AfterMethods.Count)
                throw new ArgumentException("Invalid CAM method registration.");
            if (!string.IsNullOrEmpty(WorkpieceParameter))
                throw new ArgumentException("Native typed workpiece parameter binding is unavailable on this host. Use an isolated workpiece document.");
            Colors.Validate();
            if (Inputs.Count > 32 || Inputs.Any(i => i == null) || Inputs.Select(i => i.Name).Distinct(StringComparer.Ordinal).Count() != Inputs.Count)
                throw new ArgumentException("Method input names must be unique (maximum 32).");
            foreach (var input in Inputs) input.Validate();
        }
    }

    public sealed class CamMethodInput
    {
        public string Name { get; set; } = "";
        public string Label { get; set; } = "";
        public string ValueType { get; set; } = "text";
        public string UnitType { get; set; } = "";
        public JToken? Value { get; set; }
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name) || Name.Length > 256 || Label == null || Label.Length > 120 ||
                !new[] { "text", "real", "integer", "boolean" }.Contains(ValueType) || UnitType == null || UnitType.Length > 80 || Value?.ToString().Length > 1024)
                throw new ArgumentException("Invalid typed method input.");
            if (Value != null && Value.Type != JTokenType.Null && (ValueType == "text" ? Value.Type != JTokenType.String :
                ValueType == "boolean" ? Value.Type != JTokenType.Boolean : ValueType == "integer" ? Value.Type != JTokenType.Integer :
                Value.Type != JTokenType.Float && Value.Type != JTokenType.Integer)) throw new ArgumentException("Method input value has the wrong type.");
            if (Value != null && Value.Type != JTokenType.Null && (ValueType == "real" && (double.IsNaN((double)Value) || double.IsInfinity((double)Value)) ||
                ValueType == "integer" && ((long)Value < int.MinValue || (long)Value > int.MaxValue))) throw new ArgumentException("Method input is outside the native value range.");
        }
    }

    public sealed class CamAutomationStep
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public bool Included { get; set; } = true;
        public CamMethodDefinition Method { get; set; } = new CamMethodDefinition();
        public CamColorPlan Colors { get; set; } = new CamColorPlan();
        public string Reason { get; set; } = "";
        public string Status { get; set; } = "planned";
        public JObject? Receipt { get; set; }
    }

    public sealed class CamAutomationPlan
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string DocumentId { get; set; } = "";
        public string DocumentName { get; set; } = "";
        public string WorkpieceName { get; set; } = "";
        public string MachiningStageName { get; set; } = "";
        public JObject? Workpiece { get; set; }
        public JObject? MachiningStage { get; set; }
        public JObject? ModelingStage { get; set; }
        public JArray Geometry { get; set; } = new JArray();
        public List<CamAutomationStep> Steps { get; set; } = new List<CamAutomationStep>();
        public string Status { get; set; } = "proposed";
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(DocumentId) || Workpiece == null || MachiningStage == null || ModelingStage == null || Steps == null || Geometry == null || Steps.Count > 32 || Steps.Any(s => s == null) ||
                Steps.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != Steps.Count)
                throw new ArgumentException("Select a CAM document, workpiece and machining stage.");
            var selected = Steps.Where(s => s.Included).ToArray();
            if (selected.Length == 0) throw new ArgumentException("Select at least one process.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var union = new Dictionary<string, JToken>(StringComparer.Ordinal);
            foreach (var step in selected)
            {
                if (!Guid.TryParse(step.Id, out _) || step.Method == null || step.Colors == null || step.Reason == null || step.Reason.Length > 600 ||
                    !new[] { "planned", "colored", "submitting", "calculated", "pending", "deferred", "failedOrUnknown", "colorSubmitting", "colorUnknown" }.Contains(step.Status)) throw new ArgumentException("Invalid CAM process state.");
                step.Method.Validate(); var args = step.Colors.ToArguments();
                foreach (var target in (JArray)args["targets"]!) union[target["target"]!.ToString(Newtonsoft.Json.Formatting.None)] = target;
                if (step.Colors.DocumentId != DocumentId || !JToken.DeepEquals(step.Colors.Workpiece, Workpiece) || !JToken.DeepEquals(step.Colors.ModelingStage, ModelingStage) ||
                    !JToken.DeepEquals(JObject.FromObject(step.Colors.Palette), JObject.FromObject(step.Method.Colors)))
                    throw new ArgumentException("Process geometry or color contract does not match the plan.");
                if (step.Method.AfterMethods.Any(id => !seen.Contains(id))) throw new ArgumentException("Process order violates a registered method prerequisite.");
                seen.Add(step.Method.Id);
            }
            CamColorPlan.ValidateCounts(new JArray(union.Values));
        }
        public CamAutomationPlan Snapshot() => JObject.FromObject(this).ToObject<CamAutomationPlan>()!;
    }

    public sealed class CamMethodExecutionResult
    {
        public string ExecutionId { get; set; } = "";
        public string DocumentId { get; set; } = "";
        public string Status { get; set; } = "";
        public JArray Operations { get; set; } = new JArray();
        public bool Saved { get; set; }
    }
}
