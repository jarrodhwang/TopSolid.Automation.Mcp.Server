using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;
using TopSolid.Cam.NC.Kernel.Automating;
using CamParameters = TopSolid.Cam.NC.Kernel.Automating.IParameters;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    // CAM parameters belong to an operation, not the document's IParameters folder.
    // All value/choice/unit information comes from the matched native interface.
    internal sealed class CamParameterValues
    {
        private readonly CamParameters parameters;
        private readonly Func<ElementExId, string> operationName;
        internal CamParameterValues(CamParameters parameters, Func<ElementExId, string> operationName = null) { this.parameters = parameters; this.operationName = operationName ?? CamNames.OperationName; }
        internal static CamParameterValues Native => new CamParameterValues(TopSolidCamHost.Parameters);
        internal static readonly string[] ReadApi = ApiRefs.Cam("IParameters.GetParameters", "IParameters.GetName", "IParameters.GetFullName", "IParameters.GetLocalizedName", "IParameters.GetCategories", "IParameters.GetType", "IParameters.GetValue", "IParameters.IsReadOnly", "IParameters.ToStringValue", "IParameters.ToInvariantStringValue", "IParameters.GetEnumTypeName", "IParameters.GetParameterEnumValueNames", "IParameters.GetValueBoundValue", "IParameters.GetValueBoundElement", "IParameters.GetValueFeedRateValue", "IParameters.GetValueSpindleRateValue", "ParameterType", "IOperations.GetDescription").Concat(ApiRefs.Kernel("IElements.GetFriendlyName", "IElements.GetName")).ToArray();
        internal static JObject Properties() => new JObject {
            ["element"] = Schema.Element(), ["preparationId"] = Schema.Text("Alternative CAM preparation ID; provide one target only.", 36),
            ["category"] = Schema.Text("Optional exact native category, such as CuttingConditions. Omit to inspect every operation parameter.", 128),
            ["nameContains"] = Schema.Text("Optional case-insensitive match of the native full parameter name; no category filter by default.", 256) };
        internal JObject Page(ElementExId operation, JObject p)
        {
            var all = parameters.GetParameters(operation);
            var category = (string)p["category"]; var search = (string)p["nameContains"];
            var selected = all.Where(id => (string.IsNullOrWhiteSpace(category) || Categories(id.Name).Any(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase))) &&
                (string.IsNullOrWhiteSpace(search) || id.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            var page = BatchRead.Page(selected, p, Identify, id => Read(id, true));
            page["operation"] = AutomationValues.Json(operation); page["totalOperationParameters"] = all.Count;
            page["scope"] = "Parameters inside this operation, including cutting conditions, strategy, tool and other categories. Follow nextOffset until hasMore=false; inspect failed rows separately.";
            page["editGuidance"] = "For editSupported parameters use set_cam_parameter_value with exact name, valueType and typed value. Real values use SI and the exact returned unitType. Integer enums require a listed allowedValues.value. Literal edits replace current formulas/references. Unknown numeric limits are not inferred.";
            Try(page, "operationName", () => operationName(operation));
            return page;
        }
        internal ParameterId Resolve(ElementExId operation, string exactName)
        {
            var matches = parameters.GetParameters(operation).Where(id => id.Name == exactName).ToList();
            if (matches.Count != 1) throw new ArgumentException("Use one exact parameter name, including categories, returned by list_cam_parameters. Partial, ambiguous and unknown names are rejected.");
            return matches[0];
        }
        private static JObject Identify(ParameterId id) => new JObject { ["parameter"] = AutomationValues.Json(id), ["name"] = id.Name };
        internal static string[] Categories(string name)
        {
            var index = (name ?? "").IndexOf('@');
            return index < 0 ? new string[0] : name.Substring(index + 1).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        }
        private static bool Try(JObject row, string field, Func<JToken> read)
        {
            try { row[field] = read(); return true; }
            catch (TimeoutException) { throw; }
            catch (CommunicationException ex) when (!(ex is FaultException)) { throw; }
            catch (Exception ex) { var errors = row["metadataErrors"] as JObject ?? new JObject(); errors[field] = ex.GetType().Name + ": " + ex.Message; row["metadataErrors"] = errors; return false; }
        }
        internal JObject Read(ParameterId id, bool compact = false)
        {
            var row = Identify(id); var type = parameters.GetType(id).ToString(); SmartObject value = null;
            row["valueType"] = type; row["categories"] = new JArray(Categories(id.Name));
            var knowsValue = Try(row, "value", () => { value = parameters.GetValue(id); return AutomationValues.Json(value); }) && value != null;
            Try(row, "fullName", () => parameters.GetFullName(id));
            Try(row, "simpleName", () => parameters.GetName(id));
            Try(row, "localizedName", () => parameters.GetLocalizedName(id));
            row["displayName"] = !string.IsNullOrWhiteSpace((string)row["localizedName"]) ? row["localizedName"].DeepClone() : row["simpleName"]?.DeepClone() ?? new JValue(id.Name.Split('@')[0]);
            row["friendlyName"] = row["displayName"].DeepClone();
            Try(row, "displayValue", () => parameters.ToStringValue(id));
            Try(row, "invariantValue", () => parameters.ToInvariantStringValue(id));
            var knowsReadOnly = Try(row, "readOnly", () => parameters.IsReadOnly(id));
            DescribeValue(row, value);
            var enumKnown = true;
            if (type == "Integer" || type == "Bound" || type == "FeedRate" || type == "SpindleRate") {
                enumKnown = Try(row, "enumTypeName", () => parameters.GetEnumTypeName(id));
                if (enumKnown && !string.IsNullOrWhiteSpace((string)row["enumTypeName"]))
                    enumKnown = Try(row, "allowedValues", () => Choices(parameters.GetParameterEnumValueNames(id, false), parameters.GetParameterEnumValueNames(id, true)));
            }
            if (type == "Boolean") row["allowedValues"] = new JArray(new JObject { ["value"] = false }, new JObject { ["value"] = true });
            if (type == "Bound") {
                Try(row, "boundValue", () => Real(parameters.GetValueBoundValue(id)));
                Try(row, "boundElement", () => CamNames.Named(parameters.GetValueBoundElement(id)));
            }
            if (type == "FeedRate") Try(row, "feedRateValue", () => Real(parameters.GetValueFeedRateValue(id)));
            if (type == "SpindleRate") Try(row, "spindleRateValue", () => Real(parameters.GetValueSpindleRateValue(id)));
            var scalar = type == "Real" || type == "Integer" || type == "Boolean" || type == "Text";
            var unitKnown = type != "Real" || value is SmartReal real && real.UnitType != UnitType.None;
            row["editSupported"] = scalar && knowsValue && knowsReadOnly && !(bool)row["readOnly"] && unitKnown && enumKnown;
            row["editGuidance"] = !scalar ? "The documented CAM SetValue API supports Real, Integer, Boolean and Text only. This composite/reference parameter can be inspected here; use its native TopSolid editor to change it." :
                !knowsValue || !knowsReadOnly || !enumKnown ? "Required native value/edit metadata could not be read. Refresh this parameter before proposing a change." :
                (bool)row["readOnly"] ? "This parameter is computed or read-only in TopSolid." :
                !unitKnown ? "TopSolid did not return a concrete real unit. Do not guess a unit or propose a change." :
                "Use set_cam_parameter_value with this exact name and valueType. Real values use realValueSI and the returned unitType. Integer enums require a listed allowedValues.value. A literal edit replaces any current formula/reference.";
            row["rangeConstraints"] = "Numeric minimum/maximum constraints are not exposed by this CAM IParameters interface; no range has been inferred.";
            if (compact) {
                foreach (var key in new[] { "fullName", "simpleName", "localizedName", "friendlyName", "rangeConstraints", "valueConvention" }) row.Remove(key);
                if (scalar) row.Remove("value");
                if ((bool)row["editSupported"]) row.Remove("editGuidance");
                foreach (var key in new[] { "formula", "source", "unitSymbol" }) if (row[key]?.Type == JTokenType.Null) row.Remove(key);
            }
            return row;
        }
        internal static JObject Real(SmartReal value)
        {
            var row = new JObject(); DescribeValue(row, value); row["value"] = AutomationValues.Json(value); return row;
        }
        private static void DescribeValue(JObject row, SmartObject value)
        {
            if (value is SmartReal real) {
                row["smartType"] = real.Type.ToString(); row["unitType"] = real.UnitType.ToString(); row["unitSymbol"] = real.UnitSymbol;
                row["realValueSI"] = real.Value; row["valueConvention"] = "SI; unitSymbol describes native display units, not the numeric realValueSI. Copy unitType exactly; do not infer it from the parameter name.";
                row["formula"] = real.Formula; row["source"] = AutomationValues.Json(real.ElementId);
            }
            else if (value is SmartInteger integer) { row["smartType"] = integer.Type.ToString(); row["integerValue"] = integer.Value; row["formula"] = integer.Formula; row["source"] = AutomationValues.Json(integer.ElementId); }
            else if (value is SmartBoolean boolean) { row["smartType"] = boolean.Type.ToString(); row["booleanValue"] = boolean.Value; row["formula"] = boolean.Formula; row["source"] = AutomationValues.Json(boolean.ElementId); }
            else if (value is SmartText text) { row["smartType"] = text.Type.ToString(); row["textValue"] = text.Value; row["formula"] = text.Formula; row["source"] = AutomationValues.Json(text.ElementId); }
        }
        internal static JArray Choices(IList<string> names, IList<string> labels)
        {
            if (names == null || labels == null || names.Count != labels.Count) throw new InvalidOperationException("Native enumeration names and labels do not align.");
            var choices = new JArray();
            // Native API returns sparse index-addressed arrays. Never renumber after exclusions.
            for (var index = 0; index < names.Count; index++)
                if (!string.IsNullOrWhiteSpace(names[index]) && names[index].Any(c => c != '_'))
                    choices.Add(new JObject { ["value"] = index, ["name"] = names[index], ["label"] = string.IsNullOrWhiteSpace(labels[index]) ? names[index] : labels[index] });
            return choices;
        }
        internal SmartObject Preflight(ParameterId id, JObject requested, out JObject current)
        {
            current = Read(id);
            return Build(current, requested);
        }
        internal static SmartObject Build(JObject current, JObject requested)
        {
            ScalarInput.ValidateValue(requested);
            if ((string)current["valueType"] != (string)requested["valueType"]) throw new ArgumentException("CAM parameter type changed or does not match valueType. Refresh the parameter.");
            if ((bool?)current["editSupported"] != true) throw new ArgumentException((string)current["editGuidance"] ?? "This CAM parameter cannot be edited.");
            switch ((string)requested["valueType"]) {
                case "Real":
                    if (!Enum.TryParse((string)current["unitType"], out UnitType unit) || unit == UnitType.None || (string)current["unitType"] != (string)requested["unitType"])
                        throw new ArgumentException("The live CAM unitType is " + (string)current["unitType"] + "; the proposal supplied " + (string)requested["unitType"] + ". Copy the live unitType and convert the requested value to SI before approval.");
                    var real = (double)requested["realValueSI"];
                    if (double.IsNaN(real) || double.IsInfinity(real)) throw new ArgumentException("CAM real values must be finite.");
                    return new SmartReal(unit, real);
                case "Integer":
                    var integer = (int)requested["integerValue"];
                    if (!string.IsNullOrWhiteSpace((string)current["enumTypeName"]) && !(current["allowedValues"] as JArray ?? new JArray()).Any(v => (int)v["value"] == integer))
                        throw new ArgumentException("The integer value is not one of this operation parameter's native allowedValues. Refresh the available choices.");
                    return new SmartInteger(integer);
                case "Boolean": return new SmartBoolean((bool)requested["booleanValue"]);
                case "Text": return new SmartText((string)requested["textValue"]);
                default: throw new ArgumentException("Only scalar Real, Integer, Boolean and Text CAM parameters have a documented setter.");
            }
        }
        internal JObject Set(ParameterId id, JObject requested)
        {
            var value = Preflight(id, requested, out var before);
            var changed = parameters.SetValue(id, value);
            var after = Read(id); Verify(after, requested);
            after["changed"] = changed; after["beforeDisplayValue"] = before["displayValue"]?.DeepClone(); after["readBackVerified"] = true;
            return after;
        }
        internal static void Verify(JObject actual, JObject expected)
        {
            var kind = (string)expected["valueType"]; var key = kind == "Real" ? "realValueSI" : kind == "Integer" ? "integerValue" : kind == "Boolean" ? "booleanValue" : "textValue";
            var matches = kind == "Real" ? actual[key]?.Type != JTokenType.Null && actual[key] != null && (string)actual["unitType"] == (string)expected["unitType"] && ParameterValueInput.Close((double)actual[key], (double)expected[key]) : JToken.DeepEquals(actual[key], expected[key]);
            if ((string)actual["valueType"] != kind || (string)actual["smartType"] != "Basic" || !matches)
                throw new InvalidOperationException("The CAM parameter readback differs from the requested literal value or retained a formula/reference. Rolling back this modification.");
        }
    }
}
