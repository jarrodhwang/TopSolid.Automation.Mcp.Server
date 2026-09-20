using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Cam.NC.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed partial class AutomationGateway
    {
        public JObject ToolpathPreview(JObject request) => Read("cam", () =>
        {
            if (request.Properties().Any(p => p.Name != "documentId" && p.Name != "id" && p.Name != "view" && p.Name != "nativeImage") || request["documentId"]?.Type != JTokenType.String ||
                request["id"]?.Type != JTokenType.Integer || ((string)request["documentId"]).Length > 256)
                throw new ArgumentException("An explicit operation identity is required.");
            var view = ToolpathCaptureView.Parse(request["view"]);
            if (request["nativeImage"] != null && request["nativeImage"].Type != JTokenType.Boolean) throw new ArgumentException("Invalid preview mode.");
            var nativeImage = (bool?)request["nativeImage"] == true;
            request = new JObject { ["documentId"] = request["documentId"], ["id"] = request["id"] };
            var operation = Element(new JObject { ["element"] = request.DeepClone() }); var doc = operation.DocumentId;
            if (!TopSolidHost.Documents.GetDocuments().Contains(doc) || !TopSolidCamHost.Operations.IsOperation(new ElementExId(operation)))
                return new JObject { ["status"] = "notLoaded", ["operation"] = request.DeepClone() };
            var dirty = TopSolidHost.Documents.IsDirty(doc);
            if (nativeImage) return CaptureToolpathView(operation, request, view);
            var columns = TopSolidCamHost.ToolPath.StartToolPath(operation);
            if (columns == null) return new JObject { ["status"] = "unavailable", ["operation"] = request.DeepClone() };
            JObject result;
            try
            {
                // No simulation, recalculation, selection, visibility or document state changes.
                var builder = new ToolpathPreviewGeometry(); var timer = Stopwatch.StartNew(); var ended = false; var rows = 0;
                for (; rows < 200000 && timer.Elapsed < TimeSpan.FromSeconds(12) && builder.Segments < ToolpathPreviewGeometry.MaximumSegments; rows++)
                {
                    var row = TopSolidCamHost.ToolPath.NextToolPathItem(operation);
                    if (row == null) { ended = true; break; }
                    builder.Add(row);
                    // Some 7.20 hosts expose point columns as empty strings. More scanning cannot recover those coordinates.
                    if (builder.MissingPoints >= 256 && builder.Segments == 0) break;
                }
                if (!TopSolidHost.Documents.Exists(doc) || dirty != TopSolidHost.Documents.IsDirty(doc))
                    return new JObject { ["status"] = "changed", ["operation"] = request.DeepClone() };
                result = builder.Result(ended); result["operation"] = request.DeepClone(); result["rowsScanned"] = rows;
                result["upToDate"] = TopSolidCamHost.Operations.IsUpToDate(new ElementExId(operation));
            }
            finally { TopSolidCamHost.ToolPath.EndToolPath(operation); }
            // End the table scan before the independent, reversible native-view capture.
            // Old clients provide no view and retain the segments-only protocol.
            if (view != null && (string)result["status"] == "coordinatesUnavailable")
            {
                var capture = CaptureToolpathView(operation, request, view);
                capture["coordinateStatus"] = "coordinatesUnavailable";
                capture["rowsScanned"] = result["rowsScanned"];
                return capture;
            }
            return result;
        });
    }

    /// <summary>Only explicit 3D coordinates form segments. Missing points and frame/arc changes break continuity.</summary>
    internal sealed class ToolpathPreviewGeometry
    {
        internal const int MaximumSegments = 100000;
        private readonly MemoryStream output = new MemoryStream();
        private Point3D? previous;
        private bool omitted;
        private string frame;
        internal int Segments { get; private set; }
        internal int MissingPoints { get; private set; }
        internal void Add(IDictionary<string, object> row)
        {
            if (row.TryGetValue("FRAME_NAME", out var nextFrame) && nextFrame is string name && name != frame)
            { frame = name; previous = null; }
            // An unresolved machining frame cannot be overlaid on the document's default export frame.
            if (!string.IsNullOrWhiteSpace(frame)) { previous = null; omitted = true; MissingPoints++; return; }
            if (row.ContainsKey("3D_CENTER_XYZ") && Point(row["3D_CENTER_XYZ"], out _))
            { previous = null; omitted = true; return; } // Never replace an unsupported circular move with a straight cutting move.
            Point3D point;
            if (row.TryGetValue("GOTO_XYZ_3D", out var value))
            {
                if (!Point(value, out point)) { MissingPoints++; previous = null; return; }
            }
            else if (row.TryGetValue("X", out var x) && row.TryGetValue("Y", out var y) && row.TryGetValue("Z", out var z) &&
                Number(x, out var px) && Number(y, out var py) && Number(z, out var pz)) point = new Point3D(px, py, pz);
            else return;
            if (!Finite(point)) { previous = null; omitted = true; return; }
            if (previous.HasValue && Segments < MaximumSegments)
            {
                using (var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, true))
                {
                    Write(previous.Value); Write(point); Segments++;
                    void Write(Point3D p) { writer.Write((float)(p.X * 1000)); writer.Write((float)(p.Y * 1000)); writer.Write((float)(p.Z * 1000)); }
                }
            }
            previous = point;
        }
        internal JObject Result(bool ended) => new JObject { ["status"] = Segments == 0 ? MissingPoints > 0 ? "coordinatesUnavailable" : "unavailable" : "ready",
            ["format"] = "segments-f32", ["units"] = "mm", ["upAxis"] = "Z", ["segments"] = Segments,
            ["partial"] = !ended || omitted || MissingPoints > 0, ["missingPoints"] = MissingPoints, ["data"] = Convert.ToBase64String(output.ToArray()) };
        private static bool Number(object value, out double number)
        {
            number = 0;
            if (!(value is double || value is float || value is int || value is long)) return false;
            number = Convert.ToDouble(value, CultureInfo.InvariantCulture); return !double.IsNaN(number) && !double.IsInfinity(number);
        }
        private static bool Point(object value, out Point3D point)
        {
            point = default;
            if (value is Point3D native) { point = native; return Finite(point); }
            return false;
        }
        private static bool Finite(Point3D p) => !double.IsNaN(p.X) && !double.IsNaN(p.Y) && !double.IsNaN(p.Z) &&
            Math.Abs(p.X) < 1e6 && Math.Abs(p.Y) < 1e6 && Math.Abs(p.Z) < 1e6;
    }
}
