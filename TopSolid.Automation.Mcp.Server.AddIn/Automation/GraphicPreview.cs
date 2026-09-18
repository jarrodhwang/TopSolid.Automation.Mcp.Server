using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed partial class AutomationGateway
    {
        // Private Studio RPC, never advertised to the model. No paths are accepted from the caller.
        // Export is outside StartModification/EndModification, as required by IDocuments.
        public JObject GraphicPreview(JObject request) => Read("kernel", () =>
        {
            if (request.Properties().Any(p => p.Name != "documentId" && p.Name != "pdmObjectId") ||
                (request["documentId"] != null) == (request["pdmObjectId"] != null))
                throw new ArgumentException("A preview requires exactly one explicit document or PDM document identity.");
            var identity = request["documentId"] ?? request["pdmObjectId"];
            if (identity.Type != JTokenType.String || ((string)identity).Length > 256 || string.IsNullOrWhiteSpace((string)identity))
                throw new ArgumentException("Invalid preview document identity.");
            var doc = request["documentId"] != null ? new DocumentId((string)identity) : TopSolidHost.Documents.GetDocument(new PdmObjectId((string)identity));
            // Resolving an unloaded library/document must never open it or switch the user's edited document.
            if (doc.IsEmpty || !TopSolidHost.Documents.GetDocuments().Contains(doc) || !TopSolidHost.Documents.Exists(doc))
                return new JObject { ["status"] = "notLoaded" };
            var name = TopSolidHost.Documents.GetName(doc);
            var exporter = -1;
            for (var i = 0; i < TopSolidHost.Application.ExporterCount; i++)
            {
                TopSolidHost.Application.GetExporterFileType(i, out _, out var extensions);
                if (extensions.Any(e => e.Equals(".glb", StringComparison.OrdinalIgnoreCase)) &&
                    TopSolidHost.Application.IsExporterValid(i) && TopSolidHost.Documents.CanExport(i, doc)) { exporter = i; break; }
            }
            if (exporter < 0) return new JObject { ["status"] = "unsupported", ["name"] = name };
            var options = TopSolidHost.Application.GetExporterOptions(exporter);
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                switch (option.Key)
                {
                    case "IS_COMPRESSED": case "EXPORTS_CAMERAS": case "EXPORTS_LIGHTS": case "EXPORTS_VISUALIZATIONS": option.Value = "False"; break;
                    case "IS_Y_UP": option.Value = "True"; break;
                    case "EXPORTS_TEXTURES": option.Value = "0"; break;
                    case "REFERENCE_FRAME": case "REPRESENTATION_ID": case "SIMULATION_ID": option.Value = ""; break;
                }
                options[i] = option; // KeyValue is a struct.
            }
            var directory = Path.Combine(Path.GetTempPath(), "TopSolid-Studio-preview-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, "document.glb");
            try
            {
                var dirty = TopSolidHost.Documents.IsDirty(doc);
                TopSolidHost.Documents.ExportWithOptions(exporter, options, doc, file);
                if (!TopSolidHost.Documents.Exists(doc) || TopSolidHost.Documents.IsDirty(doc) != dirty)
                    return new JObject { ["status"] = "changed", ["name"] = name };
                if (!File.Exists(file)) return new JObject { ["status"] = "unsupported", ["name"] = name };
                if (new FileInfo(file).Length > 2 * 1024 * 1024)
                    return new JObject { ["status"] = "tooLarge", ["name"] = name };
                return new JObject { ["status"] = "ready", ["name"] = name, ["documentId"] = doc.PdmDocumentId,
                    ["scope"] = "document", ["format"] = "glb", ["units"] = "m", ["upAxis"] = "Y",
                    ["capturedAt"] = DateTime.UtcNow.ToString("O"), ["data"] = Convert.ToBase64String(File.ReadAllBytes(file)) };
            }
            finally
            {
                // Only delete our exact owned file and empty directory, never recursively follow exporter output.
                try { if (File.Exists(file)) File.Delete(file); Directory.Delete(directory); }
                catch (IOException) { Console.Error.WriteLine("Preview temporary-file cleanup could not finish."); }
                catch (UnauthorizedAccessException) { Console.Error.WriteLine("Preview temporary-file cleanup was denied."); }
            }
        });
    }
}
