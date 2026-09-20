using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class Schema
    {
        public static JObject Text(string description, int max = 2048) => new JObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = max, ["description"] = description };
        public static JObject TextValue(string description, int max = 2048) { var schema = Text(description, max); schema["minLength"] = 0; return schema; }
        public static JObject Number(string description, double minimum = -1000000, double maximum = 1000000) => new JObject { ["type"] = "number", ["minimum"] = minimum, ["maximum"] = maximum, ["description"] = description };
        public static JObject Integer(string description, int minimum = 0, int maximum = int.MaxValue) => new JObject { ["type"] = "integer", ["minimum"] = minimum, ["maximum"] = maximum, ["description"] = description };
        public static JObject Boolean(string description) => new JObject { ["type"] = "boolean", ["description"] = description };
        public static JObject Choice(string description, params string[] values) { var schema = Text(description); schema["enum"] = new JArray(values); return schema; }
        public static JObject Object(JObject properties, params string[] required) => new JObject { ["type"] = "object", ["properties"] = properties, ["required"] = new JArray(required), ["additionalProperties"] = false };
        public static JObject Array(JObject item, int min = 0, int max = 100) => new JObject { ["type"] = "array", ["items"] = item, ["minItems"] = min, ["maxItems"] = max };
        public static JObject Element() => Object(new JObject { ["documentId"] = Text("Exact document revision ID returned by a tool."), ["id"] = Integer("Element number returned by a tool.", 1) }, "documentId", "id");
        public static JObject Item() => Object(new JObject { ["element"] = Element(), ["label"] = Object(new JObject
        { ["type"] = Integer("Exact label type returned by a topology tool.", 0, 255), ["id"] = Integer("Exact label ID.", int.MinValue),
            ["moniker"] = Text("Label moniker, when present."), ["name"] = Text("Label name, when present.") }, "type", "id") }, "element", "label");
        public static JObject Page(JObject properties, int defaultLimit = 25)
        {
            properties["offset"] = Integer("Zero-based page offset; defaults to 0.", 0, 1000000);
            properties["limit"] = Integer("Page size; defaults to " + defaultLimit + ", maximum 100.", 1, 100);
            properties["limit"]["default"] = defaultLimit;
            return properties;
        }
        public static void Validate(JToken value, JObject schema, string path = "arguments", int depth = 0)
        {
            if (depth > 12) throw new RpcException(-32602, "Arguments are too deeply nested.");
            if (schema["anyOf"] is JArray alternatives)
            {
                foreach (var alternative in alternatives.OfType<JObject>())
                {
                    try { Validate(value, alternative, path, depth + 1); return; }
                    catch (RpcException) { }
                }
                throw new RpcException(-32602, path + " does not match an allowed value type.");
            }
            var type = (string)schema["type"];
            bool valid = type == "object" ? value is JObject : type == "array" ? value is JArray : type == "string" ? value.Type == JTokenType.String : type == "boolean" ? value.Type == JTokenType.Boolean : type == "integer" ? value.Type == JTokenType.Integer : type == "null" ? value.Type == JTokenType.Null : type == "number" && (value.Type == JTokenType.Float || value.Type == JTokenType.Integer);
            if (!valid) throw new RpcException(-32602, path + " must be " + type + ".");
            if (value is JObject obj)
            {
                var properties = (JObject)schema["properties"] ?? new JObject();
                foreach (var name in (JArray)schema["required"] ?? new JArray())
                    if (obj[(string)name] == null) throw new RpcException(-32602, path + "." + name + " is required." +
                        ((string)name == "profiles" ? " Put primitive geometry inside profiles:[{kind,...}]; keep placement/name on the sketch. Supply all required primitive dimensions." : ""));
                foreach (var property in obj.Properties())
                {
                    if (!(properties[property.Name] is JObject child)) throw new RpcException(-32602, "Unexpected " + path + "." + property.Name +
                        (property.Name == "createSection" || property.Name == "sectionMode" ? ". Sketch tools no longer create sections. Omit this field; extrusion/revolution accept the original sketch handle directly." : ""));
                    Validate(property.Value, child, path + "." + property.Name, depth + 1);
                }
            }
            else if (value is JArray items)
            {
                if (items.Count < (int?)schema["minItems"] || items.Count > (int?)schema["maxItems"]) throw new RpcException(-32602, path + " has an invalid item count.");
                for (var index = 0; index < items.Count; index++) Validate(items[index], (JObject)schema["items"], path + "[" + index + "]", depth + 1);
            }
            else if (type == "string")
            {
                var text = (string)value;
                if (((int?)schema["minLength"] ?? 1) > 0 && string.IsNullOrWhiteSpace(text) || text.Length > ((int?)schema["maxLength"] ?? 2048)) throw new RpcException(-32602, path + " is empty or too long.");
            }
            else if (type == "number" || type == "integer")
            {
                var number = (double)value;
                if (double.IsNaN(number) || double.IsInfinity(number) || number < (double?)schema["minimum"] || number > (double?)schema["maximum"])
                    throw new RpcException(-32602, path + " is outside the allowed range.");
            }
            if (schema["enum"] is JArray choices && !choices.Any(choice => JToken.DeepEquals(choice, value))) throw new RpcException(-32602, path + " has an unsupported value.");
        }
    }
}
