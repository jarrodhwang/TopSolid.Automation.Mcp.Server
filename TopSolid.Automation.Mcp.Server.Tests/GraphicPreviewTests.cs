using System;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;
using TopSolid.Cam.NC.Kernel.Automating;

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
            ToolpathInterfaceScan();
        }

        private static void ToolpathInterfaceScan()
        {
            var identity = new ElementId(new DocumentId("toolpath-fixture"), 42);
            var path = new ToolpathTable(identity);
            var result = ToolpathPreviewReader.Read(path, identity);
            Check(path.Ended && path.Rows == 3 && (int)result["rowsScanned"] == 2 && (int)result["segments"] == 1 &&
                (string)result["source"] == "IToolPath" && !(bool)result["partial"], "IToolPath lifecycle or geometry was lost");
            path = new ToolpathTable(identity) { Fail = true };
            Throws<InvalidOperationException>(() => ToolpathPreviewReader.Read(path, identity));
            Check(path.Ended, "Failed IToolPath scan leaked its native reader");
            path = new ToolpathTable(identity) { EmptyPoints = true };
            result = ToolpathPreviewReader.Read(path, identity);
            Check(path.Ended && path.Rows == 256 && (int)result["rowsScanned"] == 256 &&
                (string)result["status"] == "coordinatesUnavailable" && (string)result["format"] == "segments-f32",
                "Unavailable IToolPath points were captured as an image or scanned without bounds");
        }

        private sealed class ToolpathTable : IToolPath
        {
            private readonly ElementId identity;
            internal bool Fail, EmptyPoints, Ended;
            internal int Rows;
            internal ToolpathTable(ElementId identity) { this.identity = identity; }
            public List<string> StartToolPath(ElementId operation)
            { Check(operation.Equals(identity), "StartToolPath identity changed"); return new List<string> { "X", "Y", "Z" }; }
            public Dictionary<string, object> NextToolPathItem(ElementId operation)
            {
                Check(operation.Equals(identity), "NextToolPathItem identity changed");
                if (Fail) throw new InvalidOperationException("Native scan failed");
                Rows++;
                if (EmptyPoints) return new Dictionary<string, object> { ["GOTO_XYZ_3D"] = "" };
                return Rows > 2 ? null : new Dictionary<string, object> { ["X"] = Rows * .001, ["Y"] = .02, ["Z"] = .03 };
            }
            public void EndToolPath(ElementId operation)
            { Check(operation.Equals(identity), "EndToolPath identity changed"); Ended = true; }
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
            var xyz = new ToolpathPreviewGeometry();
            Dictionary<string, object> XYZ(double x) => new Dictionary<string, object> { ["GOTO_XYZ_3D"] = "", ["X"] = x, ["Y"] = .02, ["Z"] = .03 };
            xyz.Add(XYZ(.001)); xyz.Add(XYZ(.002));
            Check(xyz.Segments == 1, "Numeric XYZ columns were ignored when a point column was empty");
            xyz.Add(new Dictionary<string, object> { ["X"] = .003 }); xyz.Add(XYZ(.004));
            Check(xyz.Segments == 1 && (bool)xyz.Result(true)["partial"], "Incomplete XYZ created a false cutting segment");
            var emptyArc = XYZ(.005); emptyArc["3D_CENTER_XYZ"] = "";
            xyz.Add(emptyArc); xyz.Add(XYZ(.006));
            Check(xyz.Segments == 1, "Empty arc-center data was drawn as a straight segment");
        }
    }
}
