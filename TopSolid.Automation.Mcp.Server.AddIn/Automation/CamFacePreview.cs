using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Win32.SafeHandles;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed partial class AutomationGateway
    {
        private JObject CamFacePreview(JObject request) => Read("kernel", () =>
        {
            if (request.Properties().Any(p => !new[] { "documentId", "workpiece", "faces", "camFaceGeometry", "chunked", "fileBacked" }.Contains(p.Name)) ||
                request["faces"] is not JArray faces || faces.Count == 0 || faces.Count > 32) throw new ArgumentException("Request 1..32 verified faces.");
            var scope = new CamColorGeometry(this, request);
            if (scope.NeedsWorkpiece) throw new ArgumentException("Select the exact workpiece.");
            var before = faces.OfType<JObject>().Select(face => scope.Read(new JObject { ["face"] = face.DeepClone() })).ToArray();
            if (before.Length != faces.Count || before.Select(r => (string)r["key"]).Distinct().Count() != faces.Count) throw new ArgumentException("Duplicate or invalid faces.");
            var pid = TopSolidHost.Application.ProcessId;
            JObject result;
            try
            {
                using (var pipe = new NamedPipeClientStream(".", "StudioCamPreview-" + pid, PipeDirection.InOut))
                using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(35)))
                using (deadline.Token.Register(() => pipe.Dispose()))
                {
                    pipe.Connect(1500);
                    if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var actual) || actual != pid) throw new IOException("Preview host identity mismatch.");
                    var input = Encoding.UTF8.GetBytes(new JObject { ["version"] = 1, ["documentId"] = request["documentId"], ["faces"] = faces.DeepClone() }.ToString(Formatting.None));
                    using (var writer = new BinaryWriter(pipe, Encoding.UTF8, true)) { writer.Write(input.Length); writer.Write(input); writer.Flush(); }
                    using (var reader = new BinaryReader(pipe, Encoding.UTF8, true))
                    {
                        var length = reader.ReadInt32(); if (length < 2 || length > 64 * 1024 * 1024) throw new InvalidDataException("Invalid native preview size.");
                        var bytes = reader.ReadBytes(length); if (bytes.Length != length) throw new EndOfStreamException();
                        using (var json = new JsonTextReader(new StringReader(Encoding.UTF8.GetString(bytes))) { MaxDepth = 16 }) {
                            result = JObject.Load(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                            if (json.Read()) throw new InvalidDataException("Unexpected trailing preview content.");
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is TimeoutException || ex is ObjectDisposedException)
            { return new JObject { ["status"] = "bridgeUnavailable", ["reason"] = "The registered Studio CAM preview add-in is not loaded in the selected TopSolid instance." }; }
            if ((string)result["status"] != "ready") return result;
            if ((int?)result["version"] != 1 || (string)result["hostVersion"] != typeof(TopSolidHost).Assembly.GetName().Version.ToString() ||
                (string)result["documentId"] != scope.Document.PdmDocumentId || result["items"] is not JArray items || items.Count != before.Length)
                throw new InvalidDataException("Preview protocol or host version mismatch.");
            for (int i = 0; i < before.Length; i++)
            {
                if (!JToken.DeepEquals(items[i]["face"], faces[i]) || (string)scope.Read((JObject)before[i]["target"])["fingerprint"] != (string)before[i]["fingerprint"])
                    throw new InvalidOperationException("Face geometry changed during preview.");
                if ((string)items[i]["status"] == "unavailable") {
                    var reason = (string)items[i]["reason"];
                    if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) throw new InvalidDataException("Invalid unavailable-face reason.");
                } else VerifyMesh((JObject)items[i], (JArray)before[i]["geometry"]["bounds"]);
                items[i]["key"] = before[i]["key"]; items[i]["fingerprint"] = before[i]["fingerprint"]; items[i]["color"] = before[i]["color"];
                items[i]["displayColor"] = (bool?)before[i]["color"]?["empty"] == true
                    ? ElementAppearance.Json(TopSolidHost.Elements.GetColor(Element(new JObject { ["element"] = faces[i]["element"].DeepClone() }))) : before[i]["color"].DeepClone();
            }
            var file = Path.Combine(Path.GetTempPath(), "Studio-cam-faces-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(file, result.ToString(Formatting.None), new UTF8Encoding(false));
            return new JObject { ["status"] = "ready", ["format"] = "cam-faces-v1", ["documentId"] = scope.Document.PdmDocumentId,
                ["transferId"] = previewTransfers.Add(file), ["byteLength"] = new FileInfo(file).Length, ["units"] = "m" };
        });
        internal static void VerifyMesh(JObject item, JArray bounds)
        {
            if (item["positions"] is not JArray points || item["indices"] is not JArray indices || points.Count < 9 || points.Count % 3 != 0 ||
                indices.Count == 0 || indices.Count % 3 != 0 || indices.Count > 750000 || points.Count > 2250000) throw new InvalidDataException("Invalid face mesh.");
            var min = new[] { double.MaxValue, double.MaxValue, double.MaxValue }; var max = new[] { double.MinValue, double.MinValue, double.MinValue };
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].Type != JTokenType.Float && points[i].Type != JTokenType.Integer) throw new InvalidDataException("Invalid vertex.");
                var v = (double)points[i]; int axis = i % 3;
                if (double.IsNaN(v) || double.IsInfinity(v) || v < (double)bounds[axis] - .0001 || v > (double)bounds[axis + 3] + .0001)
                    throw new InvalidDataException("Native face display does not match document coordinates.");
                min[axis] = Math.Min(min[axis], v); max[axis] = Math.Max(max[axis], v);
            }
            foreach (var index in indices) if (index.Type != JTokenType.Integer || (long)index < 0 || (long)index >= points.Count / 3) throw new InvalidDataException("Invalid triangle index.");
            for (int axis = 0; axis < 3; axis++) if (Math.Abs(min[axis] - (double)bounds[axis]) > .0001 || Math.Abs(max[axis] - (double)bounds[axis + 3]) > .0001)
                throw new InvalidDataException("Incomplete native face display.");
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetNamedPipeServerProcessId(SafePipeHandle handle, out int serverProcessId);
    }
}
