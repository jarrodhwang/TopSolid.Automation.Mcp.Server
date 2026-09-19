using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Cam.NC.Kernel.Automating;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
    internal static class CamNcActionTools
    {
        public static void Register(AutomationGateway a, Action<ToolDefinition> register)
        {
            var generate = DocumentActionTools.Target();
            generate["operations"] = Schema.Array(OperationReference(), 1, 128);
            generate["postProcessorId"] = Schema.TextValue("Exact post-processor identifier. Empty uses the identifier configured in the CAM document.", 512);
            register(new ToolDefinition("topsolid_generate_nc_for_selection",
                "Generate NC code for the selected existing CAM operations with INCPostProcessor. The selected operations are validated first; no output is reported as successful unless TopSolid returns generated NC files. Does not save the result to a user-selected PC path.",
                generate,
                p => a.Read("cam", () => Generate(a, p)),
                "Cam/Nc", new[] { "documentId", "operations" }, false,
                ApiRefs.Cam("INCPostProcessor.GetNCPostProcessorId", "INCPostProcessor.GenerateNCCodesForSelectionWithOptions", "INCFiles.GetNCFilesDocuments", "INCFiles.GetNCFiles"),
                ValidateGenerate,
                p => PreviewGenerate(a, p),
                "Generate NC code for the selected CAM operations with the chosen post-processor. The generated NC entities are not exported to a user-selected PC path until the user chooses a destination."));

            var export = DocumentActionTools.Target();
            export["ncFile"] = Schema.Element();
            export["fileName"] = Schema.Text("Absolute destination file path chosen by the user in the PC save dialog.", 4096);
            register(new ToolDefinition("topsolid_export_nc_file",
                "Export one generated NC file entity to the exact user-selected PC path through INCFiles.ExportNCCodes.",
                export,
                p => a.Read("cam", () => Export(a, p)),
                "Cam/Nc", new[] { "documentId", "ncFile", "fileName" }, false,
                ApiRefs.Cam("INCFiles.IsNCFile", "INCFiles.ExportNCCodes"),
                ValidateExport,
                p => PreviewExport(a, p),
                "Export the selected generated NC file to the user-selected PC path. This writes an external file and does not save the TopSolid document."));
        }

        private static JObject OperationReference()
        {
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["element"] = Schema.Element(),
                    ["preparationId"] = Schema.Text("CAM preparation identifier for an operation that is not represented by an element.", 36)
                },
                ["additionalProperties"] = false
            };
        }

        private static void ValidateGenerate(JObject p)
        {
            foreach (var item in (JArray)p["operations"]!)
            {
                if (item is not JObject operation || (operation["element"] == null) == (operation["preparationId"] == null))
                    throw new ArgumentException("Each operation must contain exactly one element or preparationId.");
                if (operation["preparationId"] != null && (!Guid.TryParse((string)operation["preparationId"]!, out var id) || id == Guid.Empty))
                    throw new ArgumentException("Each preparationId must be a non-empty GUID.");
            }
        }

        private static void ValidateExport(JObject p)
        {
            var fileName = (string)p["fileName"]!;
            if (fileName.IndexOf('\0') >= 0 || !Path.IsPathRooted(fileName) || Directory.Exists(fileName))
                throw new ArgumentException("fileName must be an absolute file path, not a directory or an invalid path.");
        }

        private static JObject PreviewGenerate(AutomationGateway a, JObject p)
        {
            return a.Read("cam", () =>
            {
                var document = a.Document(p);
                var postProcessorId = ResolvePostProcessor(document, (string)p["postProcessorId"]);
                var operations = ReadOperations(a, p, document);
                return new JObject
                {
                    ["action"] = "generate NC",
                    ["documentId"] = document.PdmDocumentId,
                    ["postProcessorId"] = postProcessorId,
                    ["operations"] = new JArray(operations.Select(operation => AutomationValues.Json(operation))),
                    ["operationCount"] = operations.Count
                };
            });
        }

        private static JObject Generate(AutomationGateway a, JObject p)
        {
            var document = a.Document(p);
            var postProcessorId = ResolvePostProcessor(document, (string)p["postProcessorId"]);
            var operations = ReadOperations(a, p, document);
            if (operations.Count == 0) throw new ArgumentException("Select at least one CAM operation.");

            List<string> log;
            var files = TopSolidCamHost.NCPostProcessor.GenerateNCCodesForSelectionWithOptions(
                document, postProcessorId, new List<KeyValue>(), operations, out log) ?? new List<string>();
            if (files.Count == 0)
                throw new InvalidOperationException("TopSolid could not generate an NC file for the selected operations. " + JoinLog(log));

            var ncFiles = ReadNcFiles(document, postProcessorId, files);
            if (ncFiles.Count == 0)
                throw new InvalidOperationException("TopSolid generated NC output but returned no exportable NC file entity. " + JoinLog(log));

            return new JObject
            {
                ["generated"] = true,
                ["postProcessorId"] = postProcessorId,
                ["generatedFiles"] = new JArray(files),
                ["ncFiles"] = ncFiles,
                ["log"] = new JArray(log ?? new List<string>()),
                ["operationCount"] = operations.Count,
                ["message"] = "NC code was generated. Choose a PC destination for each returned NC file."
            };
        }

        private static JObject PreviewExport(AutomationGateway a, JObject p)
        {
            return a.Read("cam", () =>
            {
                var file = NcFile(a, p);
                var path = (string)p["fileName"]!;
                return new JObject
                {
                    ["action"] = "export NC file",
                    ["documentId"] = file.DocumentId.PdmDocumentId,
                    ["ncFile"] = AutomationValues.Json(file),
                    ["fileName"] = path,
                    ["name"] = SafeName(file)
                };
            });
        }

        private static JObject Export(AutomationGateway a, JObject p)
        {
            var file = NcFile(a, p);
            var path = (string)p["fileName"]!;
            TopSolidCamHost.NCFiles.ExportNCCodes(file, path);
            return new JObject
            {
                ["exported"] = true,
                ["fileName"] = path,
                ["ncFile"] = AutomationValues.Json(file),
                ["name"] = SafeName(file)
            };
        }

        private static ElementId NcFile(AutomationGateway a, JObject p)
        {
            var file = a.Element(p, "ncFile");
            if (!TopSolidCamHost.NCFiles.IsNCFile(file)) throw new ArgumentException("The selected entity is not an NC file.");
            return file;
        }

        private static string ResolvePostProcessor(DocumentId document, string requested)
        {
            var id = requested ?? string.Empty;
            if (id.Length == 0) id = TopSolidCamHost.NCPostProcessor.GetNCPostProcessorId(document);
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("The CAM document has no configured NC post-processor.");
            return id;
        }

        private static List<ElementExId> ReadOperations(AutomationGateway a, JObject p, DocumentId document)
        {
            var result = new List<ElementExId>();
            var documentId = document.PdmDocumentId;
            foreach (var item in (JArray)p["operations"]!)
            {
                var operation = (JObject)item;
                ElementExId id;
                if (operation["preparationId"] != null)
                {
                    id = new ElementExId(Guid.Parse((string)operation["preparationId"]!));
                }
                else
                {
                    var element = (JObject)operation["element"]!;
                    if (!string.Equals((string)element["documentId"], documentId, StringComparison.Ordinal))
                        throw new ArgumentException("Every selected CAM operation must belong to the target CAM document.");
                    id = new ElementExId(new ElementId(new DocumentId((string)element["documentId"]!), (int)element["id"]!));
                }
                if (!TopSolidCamHost.Operations.IsOperation(id))
                    throw new ArgumentException("One selected item is not a valid CAM operation and cannot be post-processed.");
                if (result.Any(existing => AutomationValues.Json(existing).ToString() == AutomationValues.Json(id).ToString()))
                    throw new ArgumentException("The same CAM operation was selected more than once.");
                result.Add(id);
            }
            return result;
        }

        private static JArray ReadNcFiles(DocumentId camDocument, string postProcessorId, IReadOnlyList<string> generatedPaths)
        {
            var result = new JArray();
            var documents = TopSolidCamHost.NCFiles.GetNCFilesDocuments(camDocument, postProcessorId) ?? new List<DocumentId>();
            var index = 0;
            foreach (var document in documents)
            {
                foreach (var file in TopSolidCamHost.NCFiles.GetNCFiles(document) ?? new List<ElementId>())
                {
                    var generatedPath = index < generatedPaths.Count ? generatedPaths[index] : null;
                    result.Add(new JObject
                    {
                        ["element"] = AutomationValues.Json(file),
                        ["documentId"] = document.PdmDocumentId,
                        ["documentName"] = SafeDocumentName(document),
                        ["name"] = SafeName(file),
                        ["suggestedFileName"] = string.IsNullOrWhiteSpace(generatedPath) ? SafeName(file) : Path.GetFileName(generatedPath),
                        ["generatedPath"] = generatedPath
                    });
                    index++;
                }
            }
            return result;
        }

        private static string SafeName(ElementId id)
        {
            try
            {
                var friendly = TopSolidHost.Elements.GetFriendlyName(id);
                if (!string.IsNullOrWhiteSpace(friendly)) return friendly;
                return TopSolidHost.Elements.GetName(id) ?? "NC file";
            }
            catch { return "NC file"; }
        }

        private static string SafeDocumentName(DocumentId id)
        {
            try
            {
                var name = TopSolidHost.Documents.GetName(id);
                return string.IsNullOrWhiteSpace(name) ? id.PdmDocumentId : name;
            }
            catch { return id.PdmDocumentId; }
        }

        private static string JoinLog(IEnumerable<string> log)
        {
            var text = string.Join(" ", (log ?? Enumerable.Empty<string>()).Where(line => !string.IsNullOrWhiteSpace(line)).Take(8));
            return text.Length == 0 ? "TopSolid returned no diagnostic log." : "TopSolid log: " + text;
        }
    }
}
