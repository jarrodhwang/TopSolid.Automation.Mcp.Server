using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Cam.NC.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal static class AutomationValues
    {
        // ItemLabel.Name is null when absent. Empty string requests native lookup
        // by an empty name and breaks otherwise valid returned vertex/segment IDs.
        internal static ItemLabel ParseItemLabel(JToken label) => new ItemLabel((byte)label["type"], (int)label["id"],
            (string)label["moniker"], (string)label["name"]);
        public static JObject Result(object value) => new JObject { ["value"] = Json(value), ["units"] = "SI: metres, radians, kilograms; scalar values retain their API-defined unit." };
        public static JToken Json(object value)
        {
            if (value == null) return JValue.CreateNull();
            if (value is JToken token) return token.DeepClone();
            if (value is DocumentId document) return document.IsEmpty ? JValue.CreateNull() : new JValue(document.PdmDocumentId);
            if (value is PdmObjectId pdm) return pdm.IsEmpty ? JValue.CreateNull() : new JValue(pdm.Id);
            if (value is PdmMajorRevisionId major) return major.IsEmpty ? JValue.CreateNull() : new JValue(major.Id);
            if (value is PdmMinorRevisionId minor) return minor.IsEmpty ? JValue.CreateNull() : new JValue(minor.Id);
            if (value is ElementId element) return element.IsEmpty ? JValue.CreateNull() : new JObject { ["documentId"] = element.DocumentId.PdmDocumentId, ["id"] = element.Id };
            if (value is ItemLabel label)
            {
                var result = new JObject { ["type"] = label.Type, ["id"] = label.Id };
                if (!string.IsNullOrWhiteSpace(label.Moniker)) result["moniker"] = label.Moniker;
                if (!string.IsNullOrWhiteSpace(label.Name)) result["name"] = label.Name;
                return result;
            }
            if (value is ElementItemId item) return item.IsEmpty ? JValue.CreateNull() : new JObject { ["element"] = Json(item.ElementId), ["label"] = Json(item.ItemLabel) };
            if (value is ElementExId extended) return extended.IsEmpty ? JValue.CreateNull() : extended.IsCreated ? new JObject { ["element"] = Json(extended.ElementId) } : new JObject { ["preparationId"] = extended.Id.ToString("D") };
            if (value is ParameterId parameter)
            {
                if (parameter.IsEmpty) return JValue.CreateNull();
                var result = (JObject)Json(parameter.ElementId);
                result["name"] = parameter.Name;
                return result;
            }
            if (value is Enum) return new JValue(value.ToString());
            if (value is IEnumerable sequence && !(value is string)) return new JArray(sequence.Cast<object>().Select(Json));
            return JToken.FromObject(value, JsonSerializer.Create(new JsonSerializerSettings { MaxDepth = 12, ReferenceLoopHandling = ReferenceLoopHandling.Error, Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() } }));
        }
        public static JObject Page<T>(IEnumerable<T> values, JObject arguments, Func<T, JToken> project = null, int defaultLimit = 25)
        {
            var list = values as IList<T> ?? values.ToList();
            int offset = (int?)arguments["offset"] ?? 0, limit = (int?)arguments["limit"] ?? defaultLimit;
            return new JObject { ["items"] = new JArray(list.Skip(offset).Take(limit).Select(v => project == null ? Json(v) : project(v))), ["total"] = list.Count, ["offset"] = offset, ["limit"] = limit, ["hasMore"] = offset + limit < list.Count };
        }
    }
}
