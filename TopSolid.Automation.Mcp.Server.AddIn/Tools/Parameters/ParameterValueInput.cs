using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class ParameterValueInput
    {
        internal static readonly string[] CreateTypes = { "Real", "Integer", "Boolean", "Text", "DateTime", "Color", "Tolerance", "UserEnumeration" };
        internal static readonly string[] SetTypes = CreateTypes.Concat(new[] { "Enumeration", "Code", "Family" }).ToArray();
        private static readonly string[] Fields = { "realValueSI", "integerValue", "booleanValue", "textValue", "dateTimeValue", "colorValue", "lowerDeviationSI", "upperDeviationSI", "enumerationValue", "codeValue", "familyDocumentId", "unitType", "definitionDocumentId" };
        internal static JObject Properties(bool create)
        {
            var p = new JObject {
                ["valueType"] = Schema.Choice("Exact native parameter type; supply only its matching value fields.", create ? CreateTypes : SetTypes),
                ["realValueSI"] = Schema.Number("Real value in SI (metres/radians).", -1e12, 1e12),
                ["unitType"] = Schema.Text("Exact UnitType, required for Real/Tolerance. E.g. Length, Angle, Factor.", 128),
                ["integerValue"] = Schema.Integer("Integer value.", int.MinValue), ["booleanValue"] = Schema.Boolean("Boolean value."),
                ["textValue"] = Schema.TextValue("Literal text, including empty. Use expression tools for formulas or references.", 512),
                ["dateTimeValue"] = Schema.Text("TopSolid calendar date/time: YYYY-MM-DD or YYYY-MM-DDTHH:mm:ss, optional fractional seconds. No timezone conversion.", 40),
                ["colorValue"] = ColorSchema(), ["enumerationValue"] = Schema.Integer("Native enumeration key from get_parameter_choices; NOT the list position.", int.MinValue),
                ["lowerDeviationSI"] = Schema.Number("Tolerance lower deviation in SI.", -1e12, 1e12),
                ["upperDeviationSI"] = Schema.Number("Tolerance upper deviation in SI.", -1e12, 1e12)
            };
            if (create) p["definitionDocumentId"] = Schema.Text("Existing user enumeration definition DocumentId; required only for UserEnumeration.");
            else { p["codeValue"] = Schema.Text("Existing family code.", 512); p["familyDocumentId"] = Schema.Text("Existing family definition DocumentId, not a PDM ID."); }
            return p;
        }
        internal static JObject ColorSchema() => Schema.Object(new JObject {
            ["r"] = Schema.Integer("Red byte.", 0, 255), ["g"] = Schema.Integer("Green byte.", 0, 255), ["b"] = Schema.Integer("Blue byte.", 0, 255) }, "r", "g", "b");
        internal static Color Color(JToken value) => new Color((byte)(int)value["r"], (byte)(int)value["g"], (byte)(int)value["b"]);
        internal static UnitType Unit(JObject value)
        {
            var text = (string)value["unitType"];
            if (!Enum.GetNames(typeof(UnitType)).Contains(text) || text == "None") throw new ArgumentException("Use an exact documented UnitType, e.g. Length, Angle or Factor; None is not a value unit.");
            return (UnitType)Enum.Parse(typeof(UnitType), text);
        }
        internal static DateTime Date(JToken value)
        {
            if (!DateTime.TryParseExact((string)value, new[] { "yyyy-MM-dd", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                throw new ArgumentException("Use an ISO calendar date/time without a timezone, e.g. 2026-09-17T12:30:00. TopSolid's DateTime contract does not specify timezone conversion.");
            return DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
        }
        internal static void Validate(JObject value, bool create)
        {
            var type = (string)value["valueType"];
            if (!(create ? CreateTypes : SetTypes).Contains(type)) throw new ArgumentException("No documented adapter for this parameter creation/value type.");
            string[] required;
            switch (type) {
                case "Real": required = new[] { "realValueSI", "unitType" }; break;
                case "Integer": required = new[] { "integerValue" }; break;
                case "Boolean": required = new[] { "booleanValue" }; break;
                case "Text": required = new[] { "textValue" }; break;
                case "DateTime": required = new[] { "dateTimeValue" }; break;
                case "Color": required = new[] { "colorValue" }; break;
                case "Tolerance": required = new[] { "unitType", "lowerDeviationSI", "upperDeviationSI" }; break;
                case "UserEnumeration": required = create ? new[] { "enumerationValue", "definitionDocumentId" } : new[] { "enumerationValue" }; break;
                case "Enumeration": required = new[] { "enumerationValue" }; break;
                case "Code": required = new[] { "codeValue" }; break;
                default: required = new[] { "familyDocumentId" }; break;
            }
            foreach (var field in Fields)
                if ((value[field] != null) != required.Contains(field)) throw new ArgumentException("Provide exactly these fields for " + type + ": " + string.Join(", ", required) + ".");
            if (required.Contains("unitType")) Unit(value);
            if (type == "DateTime") Date(value["dateTimeValue"]);
            if (type == "Tolerance" && (double)value["lowerDeviationSI"] > (double)value["upperDeviationSI"]) throw new ArgumentException("Lower deviation must not exceed upper deviation.");
            if (create) ValidateName((string)value["name"]);
        }
        internal static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("$", StringComparison.Ordinal)) throw new ArgumentException("Provide a nonempty user name. $ system names are reserved.");
        }
        internal static bool Close(double actual, double expected) => Math.Abs(actual - expected) <= Math.Max(1e-12, Math.Abs(expected) * 1e-10);
        internal static Real Deviation(JObject value, string key, string symbol) => new Real(Unit(value), symbol, (double)value[key]);
    }
}
