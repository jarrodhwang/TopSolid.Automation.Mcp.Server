using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal static class ElementAppearance
    {
        private static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>(StringComparer.Ordinal) {
            ["red"] = new Color(255,0,0), ["blue"] = new Color(0,0,255), ["green"] = new Color(0,128,0), ["lime"] = new Color(0,255,0),
            ["yellow"] = new Color(255,255,0), ["orange"] = new Color(255,165,0), ["purple"] = new Color(128,0,128),
            ["magenta"] = new Color(255,0,255), ["cyan"] = new Color(0,255,255), ["black"] = new Color(0,0,0), ["white"] = new Color(255,255,255), ["gray"] = new Color(128,128,128) };
        internal static string[] Names => Palette.Keys.ToArray();
        internal static JObject Read(IElements elements, ElementId id)
        {
            var hasColor = elements.HasColor(id);
            return new JObject { ["colorModifiable"] = elements.IsColorModifiable(id), ["hasColor"] = hasColor, ["color"] = hasColor ? Json(elements.GetColor(id)) : null };
        }
        internal static JObject Json(Color color) => color.IsEmpty ? new JObject { ["empty"] = true } : new JObject { ["r"] = color.R, ["g"] = color.G, ["b"] = color.B };
        internal static void Validate(JObject p) { if (p["color"] != null) Parse(p["color"]); }
        internal static Color Parse(JToken color)
        {
            if (color is JObject value && value.Count == 1 && value["name"]?.Type == JTokenType.String && Palette.TryGetValue((string)value["name"], out var named)) return named;
            if (!(color is JObject rgb) || rgb.Count != 3 || new[] { "r", "g", "b" }.Any(k => rgb[k]?.Type != JTokenType.Integer || (int)rgb[k] < 0 || (int)rgb[k] > 255))
                throw new ArgumentException("Color requires exactly {name:'red'} (a supported named color) OR {r:255,g:0,b:0} byte values, never both.");
            return new Color((byte)(int)rgb["r"], (byte)(int)rgb["g"], (byte)(int)rgb["b"]);
        }
        internal static JObject Set(IElements elements, ElementId id, JToken color)
        {
            if (!elements.IsColorModifiable(id)) throw new ArgumentException("TopSolid does not allow this element's color to be modified. For a shape's individual faces use topsolid_color_shape_faces with verified face handles.");
            var requested = Parse(color); elements.SetColor(id, requested); var actual = elements.GetColor(id);
            if (!actual.Equals(requested)) throw new InvalidOperationException("Native element color differs from the request; rolling back.");
            return new JObject { ["element"] = AutomationValues.Json(id), ["color"] = Json(actual), ["readBackVerified"] = true,
                ["scope"] = "element color; existing face-specific colors are unchanged" };
        }
    }
}
