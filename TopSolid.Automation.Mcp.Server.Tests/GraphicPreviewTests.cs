using System;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void GraphicPreview()
        {
            var source = new[] { new KeyValue("LINEAR_TOLERANCE", "0.0002"), new KeyValue("ANGULAR_TOLERANCE", "0.261799387799149"),
                new KeyValue("WRITE_MODE", "1"), new KeyValue("USER_UNIT_SET", "False"), new KeyValue("USER_UNIT", "INCH"),
                new KeyValue("AGGREGATES_SHAPES", "False"), new KeyValue("REFERENCE_FRAME", "old-frame") };
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Check(GraphicPreviewExportOptions.TryStl(source, out var prepared), "Native precision export options rejected");
                var values = prepared.ToDictionary(k => k.Key, k => k.Value);
                Check(double.Parse(values["LINEAR_TOLERANCE"], CultureInfo.InvariantCulture) == .00005, "Preview linear tolerance must be 0.05 mm in SI");
                Check(Math.Abs(double.Parse(values["ANGULAR_TOLERANCE"], CultureInfo.InvariantCulture) - Math.PI / 36) < 1e-15, "Preview angular tolerance must be 5 degrees in radians");
                Check(values["WRITE_MODE"] == "0" && values["USER_UNIT_SET"] == "True" && values["USER_UNIT"] == "MILLIMETER", "Binary STL/mm contract changed");
                Check(values["AGGREGATES_SHAPES"] == "True" && values["REFERENCE_FRAME"] == "", "Preview may export multiple files or a non-default frame");
                Check(source[0].Value == "0.0002" && source[3].Value == "False", "Preview modified shared exporter defaults");
                Check(!GraphicPreviewExportOptions.TryStl(source.Skip(1), out _), "Exporter without tolerance support falsely advertised precision");
            }
            finally { CultureInfo.CurrentCulture = previous; }
            PreviewChunks();
            ToolpathCoordinates();
            NativeToolpathCamera();
        }

        private static void NativeToolpathCamera()
        {
            var view = JObject.Parse("{eye:[0,-0.4,0.2],look:[0,2,-1],up:[0,0,1],angle:0,radius:0.1}");
            var parsed = ToolpathCaptureView.Parse(view);
            Check(Math.Abs(parsed.Look.Y - 2 / Math.Sqrt(5)) < 1e-12 && parsed.Radius == .1, "Native capture did not preserve SI camera units");
            Check(ToolpathCaptureView.Parse(null) == null, "Old toolpath clients unexpectedly requested native capture");
            foreach (var change in new Action<JObject>[] { p => p["radius"] = 0, p => p["eye"] = new JArray(0, double.NaN, 0),
                p => p["look"] = new JArray(0,0,1), p => p["file"] = "caller.png", p => p["machine"] = "true", p => p["up"] = new JArray(0,0,0) })
            {
                var invalid = (JObject)view.DeepClone(); change(invalid); Throws<ArgumentException>(() => ToolpathCaptureView.Parse(invalid));
            }
            Check(!ToolpathCaptureView.IsBoundedPng(new byte[32]), "Truncated native screenshot accepted");
            var png = new byte[33]; new byte[] {137,80,78,71,13,10,26,10}.CopyTo(png,0);
            new byte[] {73,72,68,82}.CopyTo(png,12); png[19] = 2; png[23] = 2;
            Check(ToolpathCaptureView.IsBoundedPng(png), "Valid bounded PNG header rejected");
            png[16] = 127; Check(!ToolpathCaptureView.IsBoundedPng(png), "Oversized native screenshot accepted");
        }

        private static void PreviewChunks()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TopSolid-preview-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, "owned.stl");
            var data = Enumerable.Range(0, GraphicPreviewQuality.ChunkBytes + 19).Select(i => (byte)(i % 251)).ToArray();
            File.WriteAllBytes(file, data);
            using (var store = new PreviewTransferStore())
            {
                var id = store.Add(file);
                JObject Read(long offset) => store.Handle(new JObject { ["action"] = "read", ["transferId"] = id, ["offset"] = offset });
                Check(Convert.FromBase64String((string)Read(0)["data"]).SequenceEqual(data.Take(GraphicPreviewQuality.ChunkBytes)), "Preview chunk changed source bytes");
                Check(Convert.FromBase64String((string)Read(GraphicPreviewQuality.ChunkBytes)["data"]).SequenceEqual(data.Skip(GraphicPreviewQuality.ChunkBytes)), "Final preview chunk lost bytes");
                Throws<ArgumentException>(() => Read(-1)); Throws<ArgumentException>(() => Read(data.LongLength));
                Throws<ArgumentException>(() => store.Handle(new JObject { ["action"] = "read", ["transferId"] = file, ["offset"] = 0 }));
                Throws<ArgumentException>(() => store.Handle(new JObject { ["action"] = "read", ["transferId"] = id, ["offset"] = 0, ["path"] = file }));
                store.Handle(new JObject { ["action"] = "release", ["transferId"] = id });
                Check(!File.Exists(file) && !Directory.Exists(directory), "Preview capability release left its owned temporary file");
                Throws<ArgumentException>(() => Read(0));
            }
        }

        private static void ToolpathCoordinates()
        {
            var builder = new ToolpathPreviewGeometry();
            Dictionary<string, object> Point(double x) => new Dictionary<string, object> { ["GOTO_XYZ_3D"] = new Point3D(x, .02, .03) };
            builder.Add(Point(.001)); builder.Add(Point(.002));
            var data = builder.Result(true); var bytes = Convert.FromBase64String((string)data["data"]);
            Check((string)data["status"] == "ready" && (int)data["segments"] == 1 && !(bool)data["partial"], "Valid toolpath was not displayed");
            Check(BitConverter.ToSingle(bytes, 0) == 1 && BitConverter.ToSingle(bytes, 4) == 20 && BitConverter.ToSingle(bytes, 12) == 2,
                "Toolpath SI coordinates did not convert to the STL millimetre frame");
            builder.Add(new Dictionary<string, object> { ["GOTO_XYZ_3D"] = "" }); builder.Add(Point(.003));
            Check(builder.Segments == 1 && (bool)builder.Result(true)["partial"], "Missing coordinates created a phantom cutting segment");
            builder.Add(Point(.004)); Check(builder.Segments == 2, "Valid path did not resume after a gap");
            builder.Add(new Dictionary<string, object> { ["GOTO_XYZ_3D"] = new Point3D(.005, 0, 0), ["3D_CENTER_XYZ"] = new Point3D(0, 0, 0) });
            builder.Add(Point(.006)); Check(builder.Segments == 2, "Unsupported arc became a straight cut");
            builder.Add(new Dictionary<string, object> { ["FRAME_NAME"] = "unknown", ["GOTO_XYZ_3D"] = new Point3D(100, 100, 100) });
            builder.Add(Point(.007)); Check(builder.Segments == 2, "Unresolved work frame was overlaid on the default document frame");
            var missing = new ToolpathPreviewGeometry(); missing.Add(new Dictionary<string, object> { ["GOTO_XYZ_3D"] = "" });
            Check((string)missing.Result(true)["status"] == "coordinatesUnavailable", "Native empty point was reported as a complete path");
        }
    }
}
