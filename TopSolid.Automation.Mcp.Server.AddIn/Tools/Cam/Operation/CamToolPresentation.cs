using System;
using System.Collections.Generic;
using System.Globalization;
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
        private static readonly string[] Fields = { "ToolDefinitionName", "ToolDescription", "PocketDescription", "ToolFunction", "Document" };

        internal static JObject Read(ElementId tool)
        {
            if (tool.IsEmpty) return new JObject();
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var result = new JObject();
            try
            {
                // Read display properties and the native definition reference, not every cutting-condition value.
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
            try
            {
                var pdm = TopSolidCamHost.Tools.GetPdmId(tool);
                if (!pdm.IsEmpty)
                {
                    // Native Tool.Document is an invariant ElementId string: revision:element.
                    // Prefer that exact loaded revision over GetDocument's latest minor revision.
                    var referenced = values.TryGetValue("Document", out var reference) ? ReferenceDocument(reference) : DocumentId.Empty;
                    var document = !referenced.IsEmpty && TopSolidHost.Documents.GetDocuments().Contains(referenced) &&
                        string.Equals(TopSolidHost.Documents.GetPdmObject(referenced).Id, pdm.Id, StringComparison.Ordinal) ? referenced : TopSolidHost.Documents.GetDocument(pdm);
                    if (!document.IsEmpty && document != tool.DocumentId)
                    {
                        result["toolPdmObjectId"] = pdm.Id;
                        result["toolPreviewDocumentId"] = document.PdmDocumentId;
                    }
                }
            }
            catch (TimeoutException) { throw; }
            catch (CommunicationException ex) when (!(ex is FaultException)) { throw; }
            catch { result["toolPreviewUnavailable"] = true; }
            return result;
        }

        internal static DocumentId ReferenceDocument(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference) || reference.Length > 320) return DocumentId.Empty;
            var separator = reference.LastIndexOf(':');
            return separator > 0 && int.TryParse(reference.Substring(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
                ? new DocumentId(reference.Substring(0, separator)) : DocumentId.Empty;
        }

        internal static JObject FromValues(IReadOnlyDictionary<string, string> values)
        {
            string Read(string key) => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
            var name = Read("ToolDefinitionName") ?? Read("ToolDescription");
            var pocket = Read("PocketDescription");
            return new JObject { ["toolDefinitionName"] = name, ["toolName"] = name, ["toolNumber"] = pocket, ["toolPocket"] = pocket, ["toolFunction"] = Read("ToolFunction"),
                ["toolDisplayName"] = name == null ? JValue.CreateNull() : new JValue(string.IsNullOrEmpty(pocket) ? name : pocket + " : " + name) };
        }
    }
}
