using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    // One MCP request resolves a page locally. No extra model round per object.
    internal static class BatchRead
    {
        internal const int TextBudget = 45000;
        public static JObject Page<T>(IEnumerable<T> source, JObject arguments, Func<T, JObject> identify, Func<T, JObject> read)
        {
            var values = source as IList<T> ?? source.ToList();
            var offset = (int?)arguments["offset"] ?? 0;
            var limit = (int?)arguments["limit"] ?? 100;
            var items = new JArray(); var size = 0; var failed = 0;
            foreach (var value in values.Skip(offset).Take(limit))
            {
                var row = identify(value);
                try { row.Merge(read(value)); }
                catch (TimeoutException) { throw; }
                catch (CommunicationException ex) when (!(ex is FaultException)) { throw; }
                catch (Exception ex) { row["isError"] = true; row["error"] = ex.GetType().Name + ": " + ex.Message; }
                var length = row.ToString(Formatting.None).Length;
                if (length > TextBudget)
                {
                    row = identify(value); row["isError"] = true;
                    row["error"] = "This item exceeds the batch result budget. Inspect it separately with a narrower query.";
                    length = row.ToString(Formatting.None).Length;
                }
                if (items.Count > 0 && size + length > TextBudget) break;
                items.Add(row); size += length; if ((bool?)row["isError"] == true) failed++;
            }
            var next = offset + items.Count;
            return new JObject { ["items"] = items, ["total"] = values.Count, ["offset"] = offset, ["limit"] = limit,
                ["returned"] = items.Count, ["failed"] = failed, ["hasMore"] = next < values.Count,
                ["nextOffset"] = next < values.Count ? new JValue(next) : JValue.CreateNull() };
        }
        public static JObject Paging(JObject properties) => Schema.Page(properties, 100);
    }

    internal static class BatchInput
    {
        public const int MaximumChanges = 32;
        public static void Unique(IEnumerable<JToken> values, string label)
        {
            var seen = new List<JToken>();
            foreach (var value in values)
            {
                if (seen.Any(existing => JToken.DeepEquals(existing, value))) throw new ArgumentException("Duplicate " + label + " in the batch.");
                seen.Add(value);
            }
        }
        public static void Elements(JObject p, string array = "elements", bool entries = false)
        {
            Automation.MutationReferences.Validate(p);
            Unique(((JArray)p[array]).Select(v => entries ? v["element"] : v), "element");
        }
    }
}
