using System;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    // A literal is the default. A requested parameter stays a native dependency;
    // its current value is used only for validation/preview, never as its replacement.
    internal sealed class FeatureDimension
    {
        internal SmartReal Smart;
        internal double ValueSI;
        internal JObject Receipt;

        internal static void Validate(JObject p, string literal, string reference)
        {
            if ((p[literal] == null) == (p[reference] == null))
                throw new ArgumentException("Supply exactly one of " + literal + " (absolute value) or " + reference + " (existing Real parameter handle).");
        }
        internal static FeatureDimension Resolve(JObject p, string literal, string reference, UnitType unit, double scale,
            IParameters parameters, IElements elements, double maximumSI = double.MaxValue)
        {
            Validate(p, literal, reference);
            SmartReal smart; double value; JObject receipt;
            if (p[reference] == null)
            {
                value = (double)p[literal] * scale;
                smart = new SmartReal(unit, value);
                receipt = new JObject { ["mode"] = "absolute" };
            }
            else
            {
                var handle = p[reference]; var id = new ElementId(new DocumentId((string)handle["documentId"]), (int)handle["id"]);
                if (!elements.Exists(id) || elements.IsInvalid(id) || parameters.GetParameterType(id) != ParameterType.Real || !parameters.HasValue(id))
                    throw new ArgumentException(reference + " must identify a valid Real parameter with a value.");
                parameters.GetRealUnit(id, out var actualUnit, out _);
                if (actualUnit != unit) throw new ArgumentException(reference + " requires UnitType." + unit + ", not " + actualUnit + ". No implicit conversion is performed.");
                value = parameters.GetRealValue(id);
                smart = new SmartReal(id);
                receipt = new JObject { ["mode"] = "parameter", ["parameter"] = AutomationValues.Json(id), ["name"] = elements.GetName(id),
                    ["binding"] = "Native SmartReal(ElementId); subsequent parameter changes drive this feature dimension." };
            }
            if (!(value > 0) || double.IsInfinity(value) || value > maximumSI)
                throw new ArgumentException(literal + " must currently be finite, positive and within the supported feature range.");
            receipt["unitType"] = unit.ToString(); receipt["currentValueSI"] = value;
            return new FeatureDimension { Smart = smart, ValueSI = value, Receipt = receipt };
        }
        internal static FeatureDimension Length(JObject p, string field, double scale) => Resolve(p, field, field + "Parameter", UnitType.Length, scale, TopSolidHost.Parameters, TopSolidHost.Elements, 100000);
        internal static FeatureDimension Angle(JObject p) => Resolve(p, "angleDegrees", "angleParameter", UnitType.Angle, Math.PI / 180, TopSolidHost.Parameters, TopSolidHost.Elements, 2 * Math.PI);
        internal SmartReal RevolutionAngle => Smart.Type == SmartRealType.Basic && Math.Abs(ValueSI - 2 * Math.PI) < 1e-12 ? null : Smart;
    }
}
