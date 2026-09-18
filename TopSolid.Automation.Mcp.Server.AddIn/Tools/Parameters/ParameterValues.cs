using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    // Typed native adapters; injectable interfaces allow contract tests without CAD mutations.
    internal sealed class ParameterValues
    {
        private readonly IParameters parameters;
        private readonly IElements elements;
        private readonly Func<UnitType, string> unitSymbol;
        private readonly Dictionary<UnitType, string> symbols = new Dictionary<UnitType, string>();
        internal ParameterValues(IParameters parameters, IElements elements, Func<UnitType, string> unitSymbol = null) { this.parameters = parameters; this.elements = elements; this.unitSymbol = unitSymbol; }
        internal static ParameterValues Native => new ParameterValues(TopSolidHost.Parameters, TopSolidHost.Elements, unit => TopSolidHost.Units.GetUnitSymbol(unit, 0));
        private string Symbol(JObject value) { var unit = ParameterValueInput.Unit(value); if (!symbols.TryGetValue(unit, out var symbol)) { symbol = unitSymbol?.Invoke(unit) ?? throw new InvalidOperationException("Native unit-symbol lookup is unavailable."); symbols.Add(unit, symbol); } return symbol; }
        internal static readonly string[] ReadApi = ApiRefs.Kernel("ParameterType", "ParameterRelayType", "IParameters.GetParameterType", "IParameters.HasValue", "IParameters.GetRealValue", "IParameters.GetRealUnit", "IParameters.GetIntegerValue", "IParameters.GetBooleanValue", "IParameters.GetTextValue", "IParameters.IsTextLocalized", "IParameters.GetTextLocalizedValue", "IParameters.IsTextParameterized", "IParameters.GetTextParameterizedValue", "IParameters.GetDateTimeValue", "IParameters.GetColorValue", "Color", "IParameters.GetCodeValue", "IParameters.GetFamilyValue", "IParameters.GetEnumerationValue", "IParameters.GetEnumerationText", "IParameters.GetEnumerationDefinition", "IParameters.GetUserEnumerationValue", "IParameters.GetUserEnumerationText", "IParameters.GetUserEnumerationDefinition", "IParameters.GetToleranceUnit", "IParameters.GetToleranceUpperDeviation", "IParameters.GetToleranceLowerDeviation", "IParameters.GetToleranceDefinition", "IParameters.GetToleranceClass", "Real", "IParameters.GetRelayType", "IParameters.GetRelayedParameter", "IElements.GetParent", "IElements.IsModifiable", "IElements.GetName", "IElements.GetFriendlyName");
        internal static readonly string[] ChoicesApi = ApiRefs.Kernel("IParameters.GetEnumerationValues", "IParameters.GetUserEnumerationValues", "IParameters.HasUserEnumerationPossibleValues", "IParameters.GetUserEnumerationPossibleValues", "IParameters.HasRealParameterPossibleValues", "IParameters.GetRealParameterPossibleValues", "IParameters.AreRealParameterPossibleValuesStrict", "IParameters.HasColorParameterPossibleValues", "IParameters.GetColorParameterPossibleValues", "IParameters.AreColorParameterPossibleValuesStrict", "IParameters.HasCodePossibleValues", "IParameters.GetCodePossibleValues", "CodeProperty", "UserEnumerationProperty", "RealProperty", "ColorWithDescriptionProperty");
        internal static readonly string[] ConstraintApi = ApiRefs.Kernel("IParameters.HasRealParameterConstraints", "IParameters.GetRealParameterConstraintsMode", "IParameters.GetRealParameterConstraintsMinimumLimit", "IParameters.GetRealParameterConstraintsMaximumLimit", "IParameters.GetRealParameterConstraintsDiscretizationMode", "IParameters.GetRealParameterConstraintsDiscretizationOrigin", "IParameters.GetRealParameterConstraintsDiscretizationStep");
        internal static JObject Color(Color color) => color.IsEmpty ? new JObject { ["empty"] = true } : new JObject { ["r"] = color.R, ["g"] = color.G, ["b"] = color.B };
        internal static JObject Real(Real value) => new JObject { ["valueSI"] = value.Value, ["unitType"] = value.UnitType.ToString(), ["unitSymbol"] = value.UnitSymbol };
        internal JObject Read(ElementId id, bool context = false, bool constraints = false)
        {
            var type = parameters.GetParameterType(id);
            var row = new JObject { ["element"] = AutomationValues.Json(id), ["type"] = type.ToString(), ["supported"] = ParameterValueInput.SetTypes.Contains(type.ToString()) };
            if (type == ParameterType.None || type == ParameterType.Unclassified) { row["message"] = "Not a supported parameter entity. Element properties and operation/CAM parameters use separate interfaces."; return row; }
            var hasValue = parameters.HasValue(id); row["hasValue"] = hasValue;
            if (type == ParameterType.Real) { parameters.GetRealUnit(id, out var unit, out var symbol); row["unitType"] = unit.ToString(); row["unitSymbol"] = symbol; row["valueConvention"] = "SI"; }
            if (hasValue) switch (type) {
                case ParameterType.Real: row["value"] = parameters.GetRealValue(id); break;
                case ParameterType.Integer: row["value"] = parameters.GetIntegerValue(id); break;
                case ParameterType.Boolean: row["value"] = parameters.GetBooleanValue(id); break;
                case ParameterType.Text:
                    row["value"] = parameters.GetTextValue(id); row["localized"] = parameters.IsTextLocalized(id); row["parameterized"] = parameters.IsTextParameterized(id);
                    if ((bool)row["localized"]) row["displayValue"] = parameters.GetTextLocalizedValue(id);
                    if ((bool)row["parameterized"]) row["parameterizedText"] = parameters.GetTextParameterizedValue(id);
                    break;
                case ParameterType.DateTime: var date = parameters.GetDateTimeValue(id); row["value"] = date.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture); row["dateTimeKind"] = date.Kind.ToString(); break;
                case ParameterType.Color: row["value"] = Color(parameters.GetColorValue(id)); break;
                case ParameterType.Code: row["value"] = parameters.GetCodeValue(id); break;
                case ParameterType.Family: row["value"] = AutomationValues.Json(parameters.GetFamilyValue(id)); row["valueIdKind"] = "DocumentId"; break;
                case ParameterType.Enumeration: row["value"] = parameters.GetEnumerationValue(id); row["displayValue"] = parameters.GetEnumerationText(id); break;
                case ParameterType.UserEnumeration: row["value"] = parameters.GetUserEnumerationValue(id); row["displayValue"] = parameters.GetUserEnumerationText(id); break;
                case ParameterType.Tolerance: row["unitType"] = parameters.GetToleranceUnit(id).ToString(); row["value"] = new JObject { ["lower"] = Real(parameters.GetToleranceLowerDeviation(id)), ["upper"] = Real(parameters.GetToleranceUpperDeviation(id)) }; break;
            }
            else row["value"] = JValue.CreateNull();
            if (context) {
                row["name"] = elements.GetName(id); row["friendlyName"] = elements.GetFriendlyName(id);
                var parent = elements.GetParent(id); var relay = parameters.GetRelayType(id); var modifiable = elements.IsModifiable(id);
                row["parentOperation"] = AutomationValues.Json(parent); row["modifiable"] = modifiable; row["relayType"] = relay.ToString();
                if (relay != ParameterRelayType.None) row["relayedParameter"] = AutomationValues.Json(parameters.GetRelayedParameter(id));
                row["literalEditCandidate"] = modifiable && parent.IsEmpty && relay == ParameterRelayType.None && (bool?)row["localized"] != true && (bool?)row["parameterized"] != true;
                row["editGuidance"] = relay != ParameterRelayType.None ? "Edit the verified source parameter to retain this relay." : !parent.IsEmpty ? "Inspect the parent creation definition; use set_parameter_expressions for supported smart parameter operations." : "Use set_parameter_values after reviewing types, units and constraints.";
                if (type == ParameterType.Enumeration) row["enumerationDefinitionGuid"] = parameters.GetEnumerationDefinition(id).ToString("D");
                if (type == ParameterType.UserEnumeration) row["definitionDocumentId"] = AutomationValues.Json(parameters.GetUserEnumerationDefinition(id));
                if (type == ParameterType.Tolerance) { var definition = parameters.GetToleranceDefinition(id); row["definitionDocumentId"] = AutomationValues.Json(definition); if (!definition.IsEmpty) { row["toleranceClass"] = parameters.GetToleranceClass(id); row["literalEditCandidate"] = false; row["editGuidance"] = "Tolerance is linked to a definition document/class; do not replace it with literal deviations."; } }
            }
            if (constraints && type == ParameterType.Real) row["constraints"] = Constraints(id);
            return row;
        }
        internal JObject Constraints(ElementId id)
        {
            var row = new JObject { ["hasConstraints"] = parameters.HasRealParameterConstraints(id) };
            if ((bool)row["hasConstraints"]) {
                row["mode"] = parameters.GetRealParameterConstraintsMode(id).ToString();
                row["minimum"] = RealProperty(parameters.GetRealParameterConstraintsMinimumLimit(id)); row["maximum"] = RealProperty(parameters.GetRealParameterConstraintsMaximumLimit(id));
                row["discretizationMode"] = parameters.GetRealParameterConstraintsDiscretizationMode(id).ToString();
                row["discretizationOrigin"] = RealProperty(parameters.GetRealParameterConstraintsDiscretizationOrigin(id)); row["discretizationStep"] = RealProperty(parameters.GetRealParameterConstraintsDiscretizationStep(id));
            }
            return row;
        }
        private static JToken RealProperty(RealProperty value) => value == null ? JValue.CreateNull() : (JToken)Real(value.Value);
        internal JObject Choices(ElementId id, JObject paging)
        {
            var type = parameters.GetParameterType(id); var rows = new List<JObject>(); var strict = false;
            if (type == ParameterType.Enumeration || type == ParameterType.UserEnumeration) {
                List<int> values; List<string> texts;
                if (type == ParameterType.Enumeration) parameters.GetEnumerationValues(parameters.GetEnumerationDefinition(id), out values, out texts);
                else parameters.GetUserEnumerationValues(parameters.GetUserEnumerationDefinition(id), out values, out texts);
                if (values.Count != texts.Count) throw new InvalidOperationException("TopSolid returned mismatched enumeration values and texts.");
                var allowed = type == ParameterType.UserEnumeration && parameters.HasUserEnumerationPossibleValues(id) ? parameters.GetUserEnumerationPossibleValues(id).Select(v => v.Value).ToList() : null;
                for (var i = 0; i < values.Count; i++) if (allowed == null || allowed.Contains(values[i])) rows.Add(new JObject { ["value"] = values[i], ["text"] = texts[i] });
                strict = true;
            } else if (type == ParameterType.Real && parameters.HasRealParameterPossibleValues(id)) {
                rows.AddRange(parameters.GetRealParameterPossibleValues(id).Select(v => Real(v.Value))); strict = parameters.AreRealParameterPossibleValuesStrict(id);
            } else if (type == ParameterType.Color && parameters.HasColorParameterPossibleValues(id)) {
                rows.AddRange(parameters.GetColorParameterPossibleValues(id).Select(v => new JObject { ["value"] = Color(v.Value), ["text"] = v.Description })); strict = parameters.AreColorParameterPossibleValuesStrict(id);
            } else if (type == ParameterType.Code && parameters.HasCodePossibleValues(id)) { rows.AddRange(parameters.GetCodePossibleValues(id).Select(v => new JObject { ["value"] = v.Value })); strict = true; }
            var page = BatchRead.Page(rows, paging, _ => new JObject(), row => row); page["element"] = AutomationValues.Json(id); page["type"] = type.ToString(); page["strict"] = strict;
            page["scope"] = "Documented possible values; an empty non-strict list does not mean the parameter has no valid values."; return page;
        }
        internal static void ValidateLiteralTarget(bool modifiable, ParameterRelayType relay, ElementId parent, bool specialText)
        {
            if (!modifiable) throw new ArgumentException("TopSolid reports this parameter is not modifiable.");
            if (relay != ParameterRelayType.None) throw new ArgumentException("This parameter relays another parameter. Edit its verified source; do not replace the link with a literal.");
            if (!parent.IsEmpty) throw new ArgumentException("This parameter is driven by a parent operation. Use inspect_parameters(includeDefinition=true), then set_parameter_expressions for a supported smart creation operation.");
            if (specialText) throw new ArgumentException("Localized/parameterized text requires its native definition editor; a literal edit would lose that definition.");
        }
        internal void Preflight(ElementId id, JObject value)
        {
            var type = parameters.GetParameterType(id);
            if (type.ToString() != (string)value["valueType"]) throw new ArgumentException("Parameter type does not match valueType.");
            ValidateLiteralTarget(elements.IsModifiable(id), parameters.GetRelayType(id), elements.GetParent(id), type == ParameterType.Text && (parameters.IsTextLocalized(id) || parameters.IsTextParameterized(id)));
            if (type == ParameterType.Real) { parameters.GetRealUnit(id, out var unit, out _); if (unit != ParameterValueInput.Unit(value)) throw new ArgumentException("Parameter unit type does not match; this tool does not change units."); }
            if (type == ParameterType.Tolerance && parameters.GetToleranceUnit(id) != ParameterValueInput.Unit(value)) throw new ArgumentException("Tolerance unit type does not match.");
            if (type == ParameterType.Tolerance && !parameters.GetToleranceDefinition(id).IsEmpty) throw new ArgumentException("This tolerance is defined by a tolerance document/class. A literal edit would replace that definition.");
            if (type == ParameterType.Enumeration || type == ParameterType.UserEnumeration) {
                // Validate using the complete native lists, never a paginated subset.
                List<int> values; List<string> texts;
                if (type == ParameterType.Enumeration) parameters.GetEnumerationValues(parameters.GetEnumerationDefinition(id), out values, out texts);
                else parameters.GetUserEnumerationValues(parameters.GetUserEnumerationDefinition(id), out values, out texts);
                if (!values.Contains((int)value["enumerationValue"])) throw new ArgumentException("Enumeration key does not belong to the native definition.");
                if (type == ParameterType.UserEnumeration && parameters.HasUserEnumerationPossibleValues(id) && !parameters.GetUserEnumerationPossibleValues(id).Any(v => v.Value == (int)value["enumerationValue"])) throw new ArgumentException("Enumeration key is excluded by this parameter's possible values.");
            }
            if (type == ParameterType.Real && parameters.HasRealParameterPossibleValues(id) && parameters.AreRealParameterPossibleValuesStrict(id) && !parameters.GetRealParameterPossibleValues(id).Any(v => v.Value.UnitType == ParameterValueInput.Unit(value) && ParameterValueInput.Close(v.Value.Value, (double)value["realValueSI"]))) throw new ArgumentException("Value is outside this parameter's strict possible values.");
            if (type == ParameterType.Color && parameters.HasColorParameterPossibleValues(id) && parameters.AreColorParameterPossibleValuesStrict(id) && !parameters.GetColorParameterPossibleValues(id).Any(v => v.Value.Equals(ParameterValueInput.Color(value["colorValue"])))) throw new ArgumentException("Color is outside this parameter's strict possible values.");
            if (type == ParameterType.Code && parameters.HasCodePossibleValues(id) && !parameters.GetCodePossibleValues(id).Any(v => v.Value == (string)value["codeValue"])) throw new ArgumentException("Code is not in the family's possible codes.");
        }
        internal ElementId Create(DocumentId doc, JObject value)
        {
            switch ((string)value["valueType"]) {
                case "Real": return parameters.CreateRealParameter(doc, ParameterValueInput.Unit(value), (double)value["realValueSI"]);
                case "Integer": return parameters.CreateIntegerParameter(doc, (int)value["integerValue"]);
                case "Boolean": return parameters.CreateBooleanParameter(doc, (bool)value["booleanValue"]);
                case "Text": return parameters.CreateTextParameter(doc, (string)value["textValue"]);
                case "Color": return parameters.CreateColorParameter(doc, ParameterValueInput.Color(value["colorValue"]));
                case "DateTime": return parameters.CreateDateTimeParameter(doc, ParameterValueInput.Date(value["dateTimeValue"]));
                case "Tolerance": return parameters.CreateToleranceParameter(doc, ParameterValueInput.Unit(value), (string)value["name"], ParameterValueInput.Deviation(value, "upperDeviationSI", Symbol(value)), ParameterValueInput.Deviation(value, "lowerDeviationSI", Symbol(value)));
                case "UserEnumeration": var id = parameters.CreateUserEnumParameter(doc, new DocumentId((string)value["definitionDocumentId"])); if (id.IsEmpty || elements.IsInvalid(id)) throw new InvalidOperationException("User enumeration parameter creation returned an empty or invalid entity."); parameters.SetUserEnumerationValue(id, (int)value["enumerationValue"]); return id;
                default: throw new ArgumentException("No verified creation method for this type.");
            }
        }
        internal void Set(ElementId id, JObject value)
        {
            switch ((string)value["valueType"]) {
                case "Real": parameters.SetRealValue(id, (double)value["realValueSI"]); break;
                case "Integer": parameters.SetIntegerValue(id, (int)value["integerValue"]); break;
                case "Boolean": parameters.SetBooleanValue(id, (bool)value["booleanValue"]); break;
                case "Text": parameters.SetTextValue(id, (string)value["textValue"]); break;
                case "DateTime": parameters.SetDateTimeValue(id, ParameterValueInput.Date(value["dateTimeValue"])); break;
                case "Color": parameters.SetColorValue(id, ParameterValueInput.Color(value["colorValue"])); break;
                case "Code": parameters.SetCodeValue(id, (string)value["codeValue"]); break;
                case "Family": parameters.SetFamilyValue(id, new DocumentId((string)value["familyDocumentId"])); break;
                case "Enumeration": parameters.SetEnumerationValue(id, (int)value["enumerationValue"]); break;
                case "UserEnumeration": parameters.SetUserEnumerationValue(id, (int)value["enumerationValue"]); break;
                case "Tolerance": parameters.SetTolerance(id, ParameterValueInput.Unit(value), ParameterValueInput.Deviation(value, "upperDeviationSI", Symbol(value)), ParameterValueInput.Deviation(value, "lowerDeviationSI", Symbol(value))); break;
                default: throw new ArgumentException("No verified value setter for this type.");
            }
        }
        internal static void Verify(JObject actual, JObject expected)
        {
            if ((bool?)actual["hasValue"] != true || (string)actual["type"] != (string)expected["valueType"]) throw new InvalidOperationException("Parameter type/value presence readback differs; rolling back.");
            bool matches; var v = actual["value"];
            switch ((string)expected["valueType"]) {
                case "Real": matches = (string)actual["unitType"] == (string)expected["unitType"] && ParameterValueInput.Close((double)v, (double)expected["realValueSI"]); break;
                case "DateTime": matches = ParameterValueInput.Date(v) == ParameterValueInput.Date(expected["dateTimeValue"]); break;
                case "Color": matches = JToken.DeepEquals(v, expected["colorValue"]); break;
                case "Tolerance": matches = (string)actual["unitType"] == (string)expected["unitType"] && ParameterValueInput.Close((double)v["lower"]["valueSI"], (double)expected["lowerDeviationSI"]) && ParameterValueInput.Close((double)v["upper"]["valueSI"], (double)expected["upperDeviationSI"]); break;
                default: var key = (string)expected["valueType"] == "Integer" ? "integerValue" : (string)expected["valueType"] == "Boolean" ? "booleanValue" : (string)expected["valueType"] == "Text" ? "textValue" : (string)expected["valueType"] == "Family" ? "familyDocumentId" : (string)expected["valueType"] == "Code" ? "codeValue" : "enumerationValue"; matches = JToken.DeepEquals(v, expected[key]); break;
            }
            if (!matches) throw new InvalidOperationException(expected["valueType"] + " parameter readback differs from the requested value; rolling back the batch.");
            actual["readBackVerified"] = true;
        }
    }
}
