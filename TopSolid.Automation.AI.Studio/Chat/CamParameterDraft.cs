using System.Globalization;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.AI.Studio.Chat;

/// <summary>Typed local editors from native parameter metadata. All real writes are SI.</summary>
internal sealed class CamParameterDraft
{
    internal JObject Receipt { get; }
    internal JObject Row => (JObject)Receipt["value"]!;
    internal string Name => (string?)Row["displayName"] ?? (string)Row["name"]!;
    internal string Type => (string)Row["valueType"]!;
    internal string Current => (string?)Row["displayValue"] ?? (string?)Row["invariantValue"] ?? "";
    internal JArray? Options => Row["allowedValues"] as JArray;
    internal string Field => Type switch { "Boolean" => "booleanValue", "Integer" => "integerValue", "Real" => "realValueSI", "Text" => "textValue", _ => throw new ArgumentException("Unsupported CAM parameter.") };
    internal double Scale => (string?)Row["unitType"] switch { "Length" => 1000, "Angle" => 180 / Math.PI, _ => 1 };
    internal string Unit => Type != "Real" ? "" : (string?)Row["unitType"] switch { "Length" => "mm", "Angle" => "°", "None" or "Dimensionless" or null => "", var unit => "SI · " + unit };
    internal JToken Initial => Row[Type == "Real" ? "realValueSI" : Field]?.DeepClone() ?? throw new ArgumentException("Missing native parameter value.");
    internal string InitialText => Type == "Real" ? ((double)Initial * Scale).ToString("G15", CultureInfo.CurrentCulture) : Initial.ToString();

    internal CamParameterDraft(JObject receipt)
    {
        Receipt = (JObject)receipt.DeepClone();
        if (Receipt["value"] is not JObject row || (bool?)row["editSupported"] != true || (bool?)row["readOnly"] != false ||
            row["name"]?.Type != JTokenType.String || row["valueType"]?.Type != JTokenType.String ||
            Receipt["sourceArguments"]?["element"] is not JObject element || element["id"]?.Type != JTokenType.Integer || element["documentId"]?.Type != JTokenType.String)
            throw new ArgumentException("CAM parameter is not editable.");
        _ = Field; ValidateValue(Initial);
        if (Type == "Real" && row["unitType"]?.Type != JTokenType.String) throw new ArgumentException("Missing native units.");
    }

    internal JObject Arguments(string text, JToken? option)
    {
        JToken value;
        if (Options is { Count: > 0 } options)
        {
            if (option == null || !options.OfType<JObject>().Any(o => JToken.DeepEquals(o["value"], option))) throw new ArgumentException("Choose a native value.");
            value = option.DeepClone();
        }
        else if (Type == "Boolean") throw new ArgumentException("Missing boolean choices.");
        else if (Type == "Integer")
        {
            if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) throw new ArgumentException("Enter an integer.");
            value = new JValue(number);
        }
        else if (Type == "Real")
        {
            var styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
            if ((!double.TryParse(text, styles, CultureInfo.CurrentCulture, out var number) && !double.TryParse(text, styles, CultureInfo.InvariantCulture, out number)) || !double.IsFinite(number))
                throw new ArgumentException("Enter a finite number.");
            value = new JValue(number / Scale);
        }
        else { if (text.Length > 4000) throw new ArgumentException("Text is too long."); value = new JValue(text); }
        ValidateValue(value);
        var element = (JObject)Receipt["sourceArguments"]!["element"]!;
        var arguments = new JObject { ["documentId"] = element["documentId"]!.DeepClone(), ["element"] = element.DeepClone(),
            ["name"] = Row["name"]!.DeepClone(), ["valueType"] = Type, [Field] = value };
        if (Type == "Real") arguments["unitType"] = Row["unitType"]!.DeepClone();
        return arguments;
    }

    internal void ValidateValue(JToken? value)
    {
        var valid = Type switch
        {
            "Boolean" => value?.Type == JTokenType.Boolean,
            "Integer" => value?.Type == JTokenType.Integer && (long)value >= int.MinValue && (long)value <= int.MaxValue,
            "Real" => value?.Type is JTokenType.Integer or JTokenType.Float && double.IsFinite((double)value),
            "Text" => value?.Type == JTokenType.String && (string?)value is { Length: <= 4000 },
            _ => false
        };
        if (!valid || Options is { Count: > 0 } options && !options.OfType<JObject>().Any(o => JToken.DeepEquals(o["value"], value)))
            throw new ArgumentException("Invalid native parameter value.");
    }

    internal bool Changed(JObject arguments) => Type == "Real"
        ? Math.Abs((double)arguments[Field]! - (double)Initial) > Math.Max(1e-12, Math.Abs((double)Initial) * 1e-12)
        : !JToken.DeepEquals(arguments[Field], Initial);
}
