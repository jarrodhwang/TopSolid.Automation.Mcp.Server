using System;
using System.Linq;
using System.ServiceModel;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ParameterExpressionTools
    {
        internal static readonly string[] ReadApi = ApiRefs.Kernel("IParameters.GetSmartRealParameterCreation", "IParameters.GetSmartIntegerParameterCreation", "IParameters.GetSmartBooleanParameterCreation", "IParameters.GetSmartTextParameterCreation", "IElements.GetParent", "IElements.IsModifiable", "IElements.IsInvalid", "IOperations.IsOperation", "IParameters.GetParameterType", "IParameters.GetRelayType", "IParameters.GetRealUnit", "SmartReal", "SmartInteger", "SmartBoolean", "SmartText");
        internal static readonly string[] WriteApi = ReadApi.Concat(ParameterValues.ReadApi).Concat(ApiRefs.Kernel("IParameters.CreateSmartRealParameter", "IParameters.CreateSmartIntegerParameter", "IParameters.CreateSmartBooleanParameter", "IParameters.CreateSmartTextParameter", "IParameters.SetSmartRealParameterCreation", "IParameters.SetSmartIntegerParameterCreation", "IParameters.SetSmartBooleanParameterCreation", "IParameters.SetSmartTextParameterCreation", "IElements.SetName", "IElements.SearchByName")).ToArray();
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            foreach (var create in new[] { true, false }) {
                var isCreate = create;
                var entry = new JObject {
                    [create ? "name" : "element"] = create ? Schema.Text("Requested new parameter name. Collisions receive _1, _2, etc.; use the returned name/handle. Formula text is not rewritten.", 128) : Schema.Element(),
                    ["valueType"] = Schema.Choice("Native smart parameter type.", "Real", "Integer", "Boolean", "Text"),
                    ["mode"] = Schema.Choice("Formula, associative reference, or explicit literal definition. A literal replaces any previous formula/reference after confirmation.", "formula", "reference", "literal"),
                    ["formula"] = Schema.Text("TopSolid native formula, exactly as intended. Only for mode=formula. No alternate scripting language.", 1024),
                    ["source"] = Schema.Element(), ["unitType"] = Schema.Text("Required for Real; exact UnitType. Reference source and target must match.", 128),
                    ["realValueSI"] = Schema.Number("Real literal only, SI.", -1e12, 1e12), ["integerValue"] = Schema.Integer("Integer literal only.", int.MinValue),
                    ["booleanValue"] = Schema.Boolean("Boolean literal only."), ["textValue"] = Schema.TextValue("Text literal only, including empty.", 512)
                };
                var props = DocumentActionTools.Target(); props["parameters"] = Schema.Array(Schema.Object(entry, create ? "name" : "element", "valueType", "mode"), 1, 32);
                register(new ToolDefinition(create ? "topsolid_create_parameter_expressions" : "topsolid_set_parameter_expressions",
                    (create ? "Create named native smart parameters" : "Edit the verified parent creation operations of smart parameters") + " with formula, associative reference or explicit literal definitions, up to 32 per confirmed transaction. Pass parameter entity handles, not operation IDs. Same-document references only. No save; literal mode explicitly replaces the prior definition.", props,
                    p => a.Modify(p, isCreate ? "create parameter expressions" : "edit parameter expressions", "kernel", (doc, current) => {
                        Preflight(a, current, isCreate); var rows = new JArray();
                        foreach (JObject value in current["parameters"]) {
                            var id = isCreate ? Create(doc, value) : a.Element(value); AutomationGateway.RequireValid(id, "smart parameter entity");
                            if (isCreate) CreationNames.SetAndVerify(TopSolidHost.Elements, id, (string)value["name"]);
                            else Set(TopSolidHost.Elements.GetParent(id), value);
                            var definition = Definition(id); Verify(definition, value);
                            if (TopSolidHost.Elements.IsInvalid(id) || TopSolidHost.Elements.IsInvalid(TopSolidHost.Elements.GetParent(id))) throw new InvalidOperationException("TopSolid reports an invalid smart parameter/operation; rolling back.");
                            var row = ParameterValues.Native.Read(id, true); row["definition"] = definition; row["definitionReadBackVerified"] = true;
                            if (isCreate && (string)row["name"] != (string)value["name"]) throw new InvalidOperationException("Smart parameter name readback differs; rolling back.");
                            rows.Add(row);
                        }
                        return new JObject { ["items"] = rows, ["changed"] = rows.Count, ["nativeSmartDefinitions"] = true };
                    }), "Parameters", new[] { "documentId", "parameters" }, false, WriteApi,
                    p => Validate(p, isCreate), p => { var preview = a.PreviewDocument(p); preview["parameters"] = Preflight(a, p, isCreate); return preview; }));
            }
        }
        internal static void Validate(JObject p, bool create)
        {
            MutationReferences.Validate(p); if (!create) BatchInput.Unique(((JArray)p["parameters"]).Select(v => v["element"]), "parameter");
            foreach (JObject v in p["parameters"]) {
                if (create) ParameterValueInput.ValidateName((string)v["name"]);
                if (((string)v["mode"] == "formula") != (v["formula"] != null) || ((string)v["mode"] == "reference") != (v["source"] != null)) throw new ArgumentException("Provide only formula for formula mode, or source for reference mode.");
                if ((string)v["mode"] == "literal") ScalarInput.ValidateValue(v);
                else if (new[] { "realValueSI", "integerValue", "booleanValue", "textValue" }.Any(key => v[key] != null)) throw new ArgumentException("Literal value fields are only accepted with mode=literal.");
                if ((v["unitType"] != null) != ((string)v["valueType"] == "Real")) throw new ArgumentException("unitType is required only for Real expressions.");
                if (v["unitType"] != null) ParameterValueInput.Unit(v);
                if (!create && v["source"] != null && JToken.DeepEquals(v["element"], v["source"])) throw new ArgumentException("A parameter cannot reference itself.");
            }
        }
        private static JArray Preflight(AutomationGateway a, JObject p, bool create)
        {
            var doc = a.Document(p); var rows = new JArray();
            foreach (JObject v in p["parameters"]) {
                var row = new JObject();
                if (create) {
                    if (!TopSolidHost.Elements.SearchByName(doc, (string)v["name"]).IsEmpty) throw new ArgumentException("Parameter name is already in use: " + (string)v["name"]);
                } else {
                    var id = a.Element(v);
                    if (TopSolidHost.Parameters.GetParameterType(id).ToString() != (string)v["valueType"]) throw new ArgumentException("Parameter type differs from valueType.");
                    if (TopSolidHost.Parameters.GetRelayType(id) != ParameterRelayType.None) throw new ArgumentException("Edit the verified relay source, not this relay entity.");
                    var parent = TopSolidHost.Elements.GetParent(id);
                    if (parent.IsEmpty || !TopSolidHost.Elements.IsModifiable(parent)) throw new ArgumentException("This parameter has no modifiable smart creation operation. Use set_parameter_values for a literal parameter.");
                    // Native getter verifies the operation kind; never infer it from a display name or GUID.
                    row["currentDefinition"] = Definition(id); row["currentParameter"] = ParameterValues.Native.Read(id, true);
                    CheckUnit(id, v);
                }
                if (v["source"] != null) {
                    var source = a.Element(new JObject { ["element"] = v["source"].DeepClone() });
                    if (TopSolidHost.Parameters.GetParameterType(source).ToString() != (string)v["valueType"]) throw new ArgumentException("Reference source must have the same native parameter type.");
                    CheckUnit(source, v); row["source"] = ParameterValues.Native.Read(source, true);
                }
                rows.Add(row);
            }
            return rows;
        }
        private static void CheckUnit(ElementId id, JObject v) { if ((string)v["valueType"] == "Real") { TopSolidHost.Parameters.GetRealUnit(id, out var unit, out _); if (unit != ParameterValueInput.Unit(v)) throw new ArgumentException("Real expression unit does not match its parameter/source."); } }
        private static ElementId Source(JObject v) => new ElementId(new DocumentId((string)v["source"]["documentId"]), (int)v["source"]["id"]);
        internal static SmartObject Build(JObject v)
        {
            if ((string)v["mode"] == "literal") {
                switch ((string)v["valueType"]) {
                    case "Real": return new SmartReal(SmartRealType.Basic, ParameterValueInput.Unit(v), null, (double)v["realValueSI"], ElementId.Empty, ItemLabel.Empty, null);
                    case "Integer": return new SmartInteger(SmartIntegerType.Basic, (int)v["integerValue"], ElementId.Empty, ItemLabel.Empty, null);
                    case "Boolean": return new SmartBoolean(SmartBooleanType.Basic, (bool)v["booleanValue"], ElementId.Empty, ItemLabel.Empty, null);
                    case "Text": return new SmartText(SmartTextType.Basic, (string)v["textValue"], ElementId.Empty, ItemLabel.Empty, null);
                }
            }
            var reference = (string)v["mode"] == "reference"; var formula = (string)v["formula"];
            switch ((string)v["valueType"]) {
                // In 7.20.400.107 SmartInteger(string) produces Type=Item. Explicit constructors
                // avoid that mismatch and make the requested native definition kind testable.
                case "Real": return new SmartReal(reference ? SmartRealType.Element : SmartRealType.Formula, ParameterValueInput.Unit(v), null, null, reference ? Source(v) : ElementId.Empty, ItemLabel.Empty, formula);
                case "Integer": return new SmartInteger(reference ? SmartIntegerType.Element : SmartIntegerType.Formula, null, reference ? Source(v) : ElementId.Empty, ItemLabel.Empty, formula);
                case "Boolean": return new SmartBoolean(reference ? SmartBooleanType.Element : SmartBooleanType.Formula, null, reference ? Source(v) : ElementId.Empty, ItemLabel.Empty, formula);
                case "Text": return new SmartText(reference ? SmartTextType.Element : SmartTextType.Formula, null, reference ? Source(v) : ElementId.Empty, ItemLabel.Empty, formula);
                default: throw new ArgumentException("Unsupported smart parameter type.");
            }
        }
        private static ElementId Create(DocumentId doc, JObject v)
        {
            var smart = Build(v);
            switch (smart) {
                case SmartReal real: return TopSolidHost.Parameters.CreateSmartRealParameter(doc, real);
                case SmartInteger integer: return TopSolidHost.Parameters.CreateSmartIntegerParameter(doc, integer);
                case SmartBoolean boolean: return TopSolidHost.Parameters.CreateSmartBooleanParameter(doc, boolean);
                case SmartText text: return TopSolidHost.Parameters.CreateSmartTextParameter(doc, text);
                default: throw new ArgumentException("Unsupported smart type.");
            }
        }
        private static void Set(ElementId operation, JObject v)
        {
            switch (Build(v)) {
                case SmartReal real: TopSolidHost.Parameters.GetSmartRealParameterCreation(operation, out var oldReal); Preserve(real, oldReal); TopSolidHost.Parameters.SetSmartRealParameterCreation(operation, real); break;
                case SmartInteger integer: TopSolidHost.Parameters.GetSmartIntegerParameterCreation(operation, out var oldInteger); Preserve(integer, oldInteger); TopSolidHost.Parameters.SetSmartIntegerParameterCreation(operation, integer); break;
                case SmartBoolean boolean: TopSolidHost.Parameters.GetSmartBooleanParameterCreation(operation, out var oldBoolean); Preserve(boolean, oldBoolean); TopSolidHost.Parameters.SetSmartBooleanParameterCreation(operation, boolean); break;
                case SmartText text: TopSolidHost.Parameters.GetSmartTextParameterCreation(operation, out var oldText); Preserve(text, oldText); TopSolidHost.Parameters.SetSmartTextParameterCreation(operation, text); break;
            }
        }
        internal static void Preserve(SmartObject next, SmartObject previous)
        {
            // Replacing the expression must not silently remove tolerance or change its script dialect.
            if (next is SmartReal real && previous is SmartReal oldReal) { real.Tolerance = oldReal.Tolerance; real.UnitSymbol = oldReal.UnitSymbol; if (real.Type == SmartRealType.Formula) real.ScriptType = oldReal.ScriptType; }
            if (next is SmartInteger integer && previous is SmartInteger oldInteger && integer.Type == SmartIntegerType.Formula) integer.ScriptType = oldInteger.ScriptType;
            if (next is SmartBoolean boolean && previous is SmartBoolean oldBoolean && boolean.Type == SmartBooleanType.Formula) boolean.ScriptType = oldBoolean.ScriptType;
            if (next is SmartText text && previous is SmartText oldText && text.Type == SmartTextType.Formula) text.ScriptType = oldText.ScriptType;
        }
        internal static JObject Inspect(ElementId parameter)
        {
            if (TopSolidHost.Elements.GetParent(parameter).IsEmpty) return new JObject { ["supported"] = false, ["reason"] = "No parent creation operation." };
            try { return Definition(parameter); }
            catch (Exception ex) when (ex is FaultException || ex is ArgumentException || ex is NotSupportedException) { return new JObject { ["supported"] = false, ["reason"] = "The parent is not a supported smart scalar creation operation: " + ex.Message }; }
        }
        private static JObject Definition(ElementId parameter)
        {
            var parent = TopSolidHost.Elements.GetParent(parameter);
            if (parent.IsEmpty || !TopSolidHost.Operations.IsOperation(parent)) throw new ArgumentException("Parameter has no native parent operation.");
            SmartObject smart;
            switch (TopSolidHost.Parameters.GetParameterType(parameter)) {
                case ParameterType.Real: TopSolidHost.Parameters.GetSmartRealParameterCreation(parent, out var real); smart = real; break;
                case ParameterType.Integer: TopSolidHost.Parameters.GetSmartIntegerParameterCreation(parent, out var integer); smart = integer; break;
                case ParameterType.Boolean: TopSolidHost.Parameters.GetSmartBooleanParameterCreation(parent, out var boolean); smart = boolean; break;
                case ParameterType.Text: TopSolidHost.Parameters.GetSmartTextParameterCreation(parent, out var text); smart = text; break;
                default: throw new ArgumentException("Only Real, Integer, Boolean and Text smart creation definitions are supported.");
            }
            var row = Describe(smart); row["operation"] = AutomationValues.Json(parent); row["supported"] = true; return row;
        }
        internal static JObject Describe(SmartObject smart)
        {
            JObject Row(string type, ElementId element, ItemLabel label, string formula, object value, string script) => new JObject {
                ["mode"] = type, ["source"] = AutomationValues.Json(element), ["itemLabel"] = AutomationValues.Json(label), ["formula"] = formula, ["value"] = AutomationValues.Json(value), ["scriptType"] = script };
            switch (smart) {
                case SmartReal real: var row = Row(real.Type.ToString(), real.ElementId, real.ItemLabel, real.Formula, real.Value, real.ScriptType.ToString()); row["unitType"] = real.UnitType.ToString(); row["unitSymbol"] = real.UnitSymbol; row["tolerance"] = real.Tolerance; return row;
                case SmartInteger integer: return Row(integer.Type.ToString(), integer.ElementId, integer.ItemLabel, integer.Formula, integer.Value, integer.ScriptType.ToString());
                case SmartBoolean boolean: return Row(boolean.Type.ToString(), boolean.ElementId, boolean.ItemLabel, boolean.Formula, boolean.Value, boolean.ScriptType.ToString());
                case SmartText text: return Row(text.Type.ToString(), text.ElementId, text.ItemLabel, text.Formula, text.Value, text.ScriptType.ToString());
                default: throw new ArgumentException("Native smart definition is missing or unsupported.");
            }
        }
        internal static void Verify(JObject definition, JObject requested)
        {
            if ((string)requested["mode"] == "literal") {
                var row = new JObject { ["hasValue"] = true, ["type"] = requested["valueType"].DeepClone(), ["value"] = definition["value"]?.DeepClone(), ["unitType"] = definition["unitType"]?.DeepClone() };
                if ((string)definition["mode"] != "Basic") throw new InvalidOperationException("Native smart definition did not retain literal mode; rolling back.");
                ParameterValues.Verify(row, requested); return;
            }
            var reference = (string)requested["mode"] == "reference";
            if ((string)definition["mode"] != (reference ? "Element" : "Formula") ||
                (reference ? !JToken.DeepEquals(definition["source"], requested["source"]) : (string)definition["formula"] != (string)requested["formula"]) ||
                !reference && (string)requested["valueType"] == "Real" && (string)definition["unitType"] != (string)requested["unitType"])
                throw new InvalidOperationException("Native smart definition differs from the requested formula/reference; rolling back.");
        }
    }
}
