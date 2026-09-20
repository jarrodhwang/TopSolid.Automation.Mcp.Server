using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Kernel.Automating.Host;
using TopSolid.Kernel.G.D3.Shapes;
using TopSolid.Kernel.TX.AddIns;

namespace TopSolid.Automation.CamPreview
{
    // This service has no modification, document-opening, command-execution or file
    // path input. Native access runs on the host UI thread, never the pipe worker.
    [Guid("413735B9-A3A5-4B42-960B-694F95C74F66")]
    public sealed class PreviewAddIn : AddIn
    {
        public override string Name => "Studio CAM geometry preview";
        public override string Manufacturer => "Studio CAM";
        public override string[] Description => new[] { "Read-only native face display geometry for Studio." };
        public override Guid[] RequiredAddIns => new Guid[0];
        private Control dispatcher;
        private CancellationTokenSource lifetime;
        private NamedPipeServerStream active;
        public override string GetRegistrationCertificate()
        {
            var file = Path.Combine(Path.GetDirectoryName(typeof(PreviewAddIn).Assembly.Location), "StudioCamPreview.registration.xml");
            return File.Exists(file) ? File.ReadAllText(file) : "";
        }
        public override void StartSession() { }
        public override void InitializeSession()
        {
            dispatcher = new Control(); dispatcher.CreateControl(); var handle = dispatcher.Handle;
            lifetime = new CancellationTokenSource(); Task.Run(() => Serve(lifetime.Token));
        }
        public override void EndSession() { lifetime?.Cancel(); active?.Dispose(); dispatcher?.Dispose(); }
        private async Task Serve(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var security = new PipeSecurity();
                    security.SetAccessRuleProtection(true, false);
                    security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
                    using (var pipe = new NamedPipeServerStream("StudioCamPreview-" + Process.GetCurrentProcess().Id, PipeDirection.InOut, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536, security))
                    {
                        active = pipe; await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
                        {
                            deadline.CancelAfter(TimeSpan.FromSeconds(30));
                            using (deadline.Token.Register(() => pipe.Dispose()))
                            using (var reader = new BinaryReader(pipe, Encoding.UTF8, true))
                            using (var writer = new BinaryWriter(pipe, Encoding.UTF8, true))
                            {
                                int size = reader.ReadInt32(); if (size < 2 || size > 256 * 1024) throw new InvalidDataException("Invalid preview request size.");
                                var bytes = reader.ReadBytes(size); if (bytes.Length != size) throw new EndOfStreamException();
                                JObject request;
                                using (var json = new JsonTextReader(new StringReader(Encoding.UTF8.GetString(bytes))) { MaxDepth = 12 }) {
                                    request = JObject.Load(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                                    if (json.Read()) throw new InvalidDataException("Unexpected trailing request content.");
                                }
                                var completion = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
                                using (deadline.Token.Register(() => completion.TrySetCanceled())) {
                                dispatcher.BeginInvoke(new Action(() => {
                                    try { deadline.Token.ThrowIfCancellationRequested(); completion.TrySetResult(Read(request)); }
                                    catch (Exception ex) { completion.TrySetResult(new JObject { ["status"] = "unavailable", ["reason"] = ex.Message }); }
                                }));
                                var result = await completion.Task.ConfigureAwait(false);
                                var output = Encoding.UTF8.GetBytes(result.ToString(Formatting.None));
                                if (output.Length > 64 * 1024 * 1024) throw new InvalidDataException("Preview batch exceeds limit.");
                                writer.Write(output.Length); writer.Write(output); writer.Flush();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is OperationCanceledException || ex is ObjectDisposedException || ex is JsonException || ex is InvalidOperationException)
                { Trace.WriteLine("Studio CAM preview: " + ex.Message); }
                finally { active = null; }
            }
        }
        private static JObject Read(JObject request)
        {
            if ((int?)request["version"] != 1 || request.Properties().Any(p => p.Name != "version" && p.Name != "documentId" && p.Name != "faces") ||
                request["faces"] is not JArray faces || faces.Count < 1 || faces.Count > 32) throw new ArgumentException("Invalid face preview batch.");
            var documentId = new DocumentId((string)request["documentId"]);
            var document = DocumentsHost.FindDocument(documentId);
            var modified = document.ModificationId; var dirty = document.IsDirty;
            var output = new JArray(); int triangles = 0; int vertices = 0;
            foreach (JObject target in faces)
            {
                try {
                if ((string)target["element"]?["documentId"] != documentId.PdmDocumentId) throw new ArgumentException("Foreign face.");
                var label = target["label"];
                var id = new ElementItemId(new ElementId(documentId, (int)target["element"]["id"]),
                    new ItemLabel((byte)label["type"], (int)label["id"], (string)label["moniker"] ?? "", (string)label["name"]));
                var shape = ElementsHost.FindElement(id.ElementId).Geometry as Shape;
                if (shape == null) throw new ArgumentException("Not a native shape.");
                var definition = shape.IsOccurrence ? shape.DefinitionShape : shape;
                if (definition == null || !definition.IsDisplayUpToDate) throw new InvalidOperationException("Refresh the native part display in TopSolid before analysis.");
                var nativeLabel = new TopSolid.Kernel.TX.Items.ItemLabel((byte)label["type"], (int)label["id"],
                    string.IsNullOrEmpty((string)label["moniker"]) ? TopSolid.Kernel.TX.Items.ItemMoniker.Empty :
                    new TopSolid.Kernel.TX.Items.ItemMoniker(new TopSolid.Kernel.SX.CString((string)label["moniker"])), (string)label["name"]);
                var face = definition.SearchFace(nativeLabel);
                if (face.IsEmpty || !face.Label.Equals(nativeLabel)) throw new InvalidOperationException("Native face no longer exists with the exact label.");
                var display = face.DisplayItem;
                if (display == null) throw new InvalidOperationException("Native face display is unavailable.");
                var points = new JArray();
                var transform = shape.IsOccurrence ? shape.DefinitionTransform : TopSolid.Kernel.G.D3.Transform.Identity;
                foreach (var vertex in display.Vertices)
                {
                    if (++vertices > 750000) throw new InvalidOperationException("Reduce the face preview batch.");
                    var point = transform * vertex;
                    points.Add(point.X); points.Add(point.Y); points.Add(point.Z);
                }
                var indices = new JArray();
                foreach (var triangle in display.IndexedFacets)
                {
                    if (++triangles > 250000) throw new InvalidOperationException("Reduce the face preview batch.");
                    indices.Add(triangle.P0); indices.Add(triangle.P1); indices.Add(triangle.P2);
                }
                var basis = new JArray();
                foreach (var source in new[] { new TopSolid.Kernel.G.D3.Point(0,0,0), new TopSolid.Kernel.G.D3.Point(1,0,0), new TopSolid.Kernel.G.D3.Point(0,1,0), new TopSolid.Kernel.G.D3.Point(0,0,1) })
                { var point = transform * source; basis.Add(new JArray(point.X, point.Y, point.Z)); }
                output.Add(new JObject { ["face"] = target.DeepClone(), ["positions"] = points, ["indices"] = indices,
                    ["coordinateSpace"] = "document", ["definitionToDocumentBasis"] = basis });
                } catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException) {
                    output.Add(new JObject { ["face"] = target.DeepClone(), ["status"] = "unavailable", ["reason"] = ex.Message });
                }
            }
            if (document.ModificationId != modified || document.IsDirty != dirty) throw new InvalidOperationException("Document changed during read.");
            return new JObject { ["status"] = "ready", ["version"] = 1, ["hostVersion"] = typeof(Shape).Assembly.GetName().Version.ToString(),
                ["documentId"] = documentId.PdmDocumentId, ["units"] = "m", ["items"] = output };
        }
    }
}
