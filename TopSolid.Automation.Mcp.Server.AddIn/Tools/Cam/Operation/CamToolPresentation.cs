using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Cam.NC.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class CamToolPresentation
    {
        internal const string Prefix = "$TopSolid.Cam.NC.Kernel.DB.Tools.Entities.Tool.";
        private static readonly string[] Fields = { "ToolDefinitionName", "ToolDescription", "PocketDescription", "ToolFunction" };

        internal static JObject Read(ElementId tool)
        {
            if (tool.IsEmpty) return new JObject();
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var result = new JObject();
            try
            {
                // Read the four display properties, not every geometry/cutting-condition value.
                // ToolFunction is a native semantic code; translated names never choose an icon.
                var parameters = TopSolidCamHost.Tools.GetParameters(tool);
                foreach (var field in Fields)
                {
                    var matches = parameters.Where(p => p.Name == Prefix + field).ToList();
                    if (matches.Count != 1) continue;
                    try { values[field] = TopSolidCamHost.Parameters.ToInvariantStringValue(matches[0]); }
                    catch (TimeoutException) { throw; }
                    catch (CommunicationException ex) when (!(ex is FaultException)) { throw; }
                    catch { result["toolDisplayMetadataIncomplete"] = true; }
                }
            }
            catch (TimeoutException) { throw; }
            catch (CommunicationException ex) when (!(ex is FaultException)) { throw; }
            catch { result["toolDisplayMetadataIncomplete"] = true; }
            result.Merge(FromValues(values));
            return result;
        }

        internal static JObject FromValues(IReadOnlyDictionary<string, string> values)
        {
            string Read(string key) => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
            var name = Read("ToolDefinitionName") ?? Read("ToolDescription");
            var pocket = Read("PocketDescription");
            return new JObject { ["toolDefinitionName"] = name, ["toolPocket"] = pocket, ["toolFunction"] = Read("ToolFunction"),
                ["toolDisplayName"] = name == null ? JValue.CreateNull() : new JValue(string.IsNullOrEmpty(pocket) ? name : pocket + " : " + name) };
        }
    }
}
