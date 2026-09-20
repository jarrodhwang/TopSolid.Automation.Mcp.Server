using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed partial class AutomationGateway
    {
        // Private Studio RPC, never advertised to the model. No paths are accepted from the caller.
        // Export is outside StartModification/EndModification, as required by IDocuments.
        private readonly PreviewTransferStore previewTransfers = new PreviewTransferStore();
        public JObject GraphicPreview(JObject request)
        {
            if (request["action"] != null) return previewTransfers.Handle(request);
            return Read("kernel", () =>
        {
            if (request.Properties().Any(p => p.Name != "documentId" && p.Name != "pdmObjectId" && p.Name != "chunked" && p.Name != "fileBacked" && p.Name != "camContext") ||
                (request["documentId"] != null) == (request["pdmObjectId"] != null))
                throw new ArgumentException("A preview requires exactly one explicit document or PDM document identity.");
            var identity = request["documentId"] ?? request["pdmObjectId"];
            if (request["fileBacked"] != null && (request["fileBacked"].Type != JTokenType.Boolean || (bool?)request["chunked"] != true))
                throw new ArgumentException("File-backed previews require chunked transport.");
            var fileBacked = (bool?)request["fileBacked"] == true;
            if (request["camContext"] != null && request["camContext"].Type != JTokenType.Boolean) throw new ArgumentException("Invalid CAM context flag.");
            if (identity.Type != JTokenType.String || ((string)identity).Length > 256 || string.IsNullOrWhiteSpace((string)identity))
                throw new ArgumentException("Invalid preview document identity.");
            var doc = request["documentId"] != null ? new DocumentId((string)identity) : TopSolidHost.Documents.GetDocument(new PdmObjectId((string)identity));
            // Resolving an unloaded library/document must never open it or switch the user's edited document.
            if (doc.IsEmpty || !TopSolidHost.Documents.GetDocuments().Contains(doc) || !TopSolidHost.Documents.Exists(doc))
                return new JObject { ["status"] = "notLoaded" };
            var name = TopSolidHost.Documents.GetName(doc);
            var camContext = (bool?)request["camContext"] == true && TopSolidHost.Documents.GetTypeFullName(doc).StartsWith("TopSolid.Cam.NC.", StringComparison.Ordinal);
            var exporter = -1; var format = "glb";
            var stlExporter = -1; List<KeyValue> stlOptions = null;
            List<KeyValue> options = null;
            for (var i = 0; i < TopSolidHost.Application.ExporterCount; i++)
            {
                TopSolidHost.Application.GetExporterFileType(i, out _, out var extensions);
                var stl = extensions.Any(e => e.Equals(".stl", StringComparison.OrdinalIgnoreCase));
                var glb = extensions.Any(e => e.Equals(".glb", StringComparison.OrdinalIgnoreCase));
                if ((!stl && !glb) || !TopSolidHost.Application.IsExporterValid(i) || !TopSolidHost.Documents.CanExport(i, doc)) continue;
                if (stl && GraphicPreviewExportOptions.TryStl(TopSolidHost.Application.GetExporterOptions(i), out var preciseOptions))
                { stlExporter = i; stlOptions = preciseOptions; }
                if (glb) { exporter = i; options = TopSolidHost.Application.GetExporterOptions(i); break; }
            }
            // STL discards every face/part color and transparency. Prefer the native
            // material-preserving exporter even when the document needs paged geometry.
            if (exporter < 0 && stlExporter >= 0) { exporter = stlExporter; options = stlOptions; format = "stl"; }
            if (exporter < 0) return new JObject { ["status"] = "unsupported", ["name"] = name };
            for (var i = 0; format == "glb" && i < options.Count; i++)
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
            var file = Path.Combine(directory, "document." + format);
            var retained = false; var toleranceScale = 1d;
            try
            {
                var dirty = TopSolidHost.Documents.IsDirty(doc);
                if (camContext)
                {
                    // Organized native export includes the machine and environment omitted by the ordinary representation.
                    // It is display-only, outside a modification, and never changes native visibility or camera state.
                    format = "glb"; file = Path.Combine(directory, "document.glb");
                    TopSolidHost.Documents.zExportToTopglTF(doc, directory, "document", true, false, false, true, true, true, true, 0, Color.Empty);
                }
                else TopSolidHost.Documents.ExportWithOptions(exporter, options, doc, file);
                // Coarser display-only tessellation for large native B-reps; no document tolerance is changed.
                for (var retry = 0; !fileBacked && format == "stl" && File.Exists(file) && new FileInfo(file).Length > GraphicPreviewQuality.MaximumStlBytes && retry < 3; retry++)
                {
                    toleranceScale *= 4;
                    GraphicPreviewExportOptions.TryStl(options, out options, toleranceScale);
                    TopSolidHost.Documents.ExportWithOptions(exporter, options, doc, file);
                }
                if (!TopSolidHost.Documents.Exists(doc) || TopSolidHost.Documents.IsDirty(doc) != dirty)
                    return new JObject { ["status"] = "changed", ["name"] = name };
                if (!File.Exists(file)) return new JObject { ["status"] = "unsupported", ["name"] = name };
                if (!fileBacked && new FileInfo(file).Length > (format == "stl" ? GraphicPreviewQuality.MaximumStlBytes : GraphicPreviewQuality.MaximumGlbBytes))
                    return new JObject { ["status"] = "tooLarge", ["name"] = name };
                var length = new FileInfo(file).Length;
                if (format == "stl")
                {
                    using (var reader = new BinaryReader(File.OpenRead(file)))
                    {
                        if (length < 84) return new JObject { ["status"] = "unsupported", ["name"] = name };
                        reader.BaseStream.Position = 80; var triangles = reader.ReadUInt32();
                        if (triangles == 0 || 84L + triangles * 50L != length) return new JObject { ["status"] = "unsupported", ["name"] = name };
                    }
                }
                var result = new JObject { ["status"] = "ready", ["name"] = name, ["documentId"] = doc.PdmDocumentId,
                    ["scope"] = "document", ["format"] = format, ["units"] = format == "stl" ? "mm" : "m", ["upAxis"] = format == "stl" ? "Z" : "Y",
                    ["camContext"] = camContext,
                    ["appearance"] = format == "glb" ? "materials" : "neutral",
                    // The installed TopSolid exporter writes native RGB/255 factors,
                    // including 192/255 for the default surface, without linearization.
                    ["colorEncoding"] = format == "glb" ? "srgb" : null,
                    ["linearToleranceMm"] = format == "stl" ? (JToken)(GraphicPreviewQuality.LinearToleranceMm * toleranceScale) : JValue.CreateNull(),
                    ["angularToleranceDegrees"] = format == "stl" ? (JToken)Math.Min(20, GraphicPreviewQuality.AngularToleranceDegrees * Math.Sqrt(toleranceScale)) : JValue.CreateNull(),
                    ["capturedAt"] = DateTime.UtcNow.ToString("O") };
                if ((bool?)request["chunked"] == true)
                { result["transferId"] = previewTransfers.Add(file); result["byteLength"] = length; retained = true; }
                else if (length <= 84 + 250000 * 50) result["data"] = Convert.ToBase64String(File.ReadAllBytes(file));
                else return new JObject { ["status"] = "tooLarge", ["name"] = name };
                return result;
            }
            finally
            {
                // Only delete our exact owned file and empty directory, never recursively follow exporter output.
                try { if (!retained) { if (File.Exists(file)) File.Delete(file); Directory.Delete(directory); } }
                catch (IOException) { Console.Error.WriteLine("Preview temporary-file cleanup could not finish."); }
                catch (UnauthorizedAccessException) { Console.Error.WriteLine("Preview temporary-file cleanup was denied."); }
            }
        });
        }
    }
}
