using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;
using TopSolid.Cam.NC.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal sealed partial class AutomationGateway
    {
        private JObject CaptureToolpathView(ElementId operation, JObject identity, ToolpathCaptureView view)
        {
            JObject Status(string status) => new JObject { ["status"] = status, ["operation"] = identity.DeepClone() };
            if (view == null || TopSolidHost.Application.Version < 720326000 || connectionTarget.Options.Mode != "local") return Status("coordinatesUnavailable");
            if (!string.IsNullOrEmpty(TopSolidHost.Application.ActiveCommandFullName)) return Status("busy");
            var document = operation.DocumentId;
            var selected = ToolpathEntities(new[] { operation });
            if (selected.Count == 0) return Status("unavailable");
            var all = ToolpathEntities(TopSolidCamHost.Operations.GetOperations(document));
            all.UnionWith(selected);
            var states = all.ToDictionary(e => e, e => TopSolidHost.Elements.IsVisible(e));
            var machine = TopSolidCamHost.Documents.GetMachine(document);
            if (!machine.IsEmpty && TopSolidHost.Elements.Exists(machine)) states[machine] = TopSolidHost.Elements.IsVisible(machine);
            var dirty = TopSolidHost.Documents.IsDirty(document);
            var edited = TopSolidHost.Documents.EditedDocument;
            var nativeView = TopSolidHost.Visualization3D.GetActiveView(document);
            TopSolidHost.Visualization3D.GetViewCamera(document, nativeView, out var eye, out var look, out var up, out var angle, out var radius);
            var folder = Path.Combine(Path.GetTempPath(), "TopSolid-toolpath-view-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "view.png");
            var began = false; var cameraChanged = false;
            JObject result;
            try
            {
                // The screenshot API renders the true calculated CAM display, including 5-axis kinematics
                // and motion colors. Visibility edits exist only in this cancelled transaction.
                if (!TopSolidHost.Application.StartModification("AI: temporary toolpath view", false)) return Status("busy");
                began = true;
                var temporaryDocument = document;
                TopSolidHost.Documents.EnsureIsDirty(ref temporaryDocument);
                foreach (var state in states)
                {
                    var id = new ElementId(temporaryDocument, state.Key.Id);
                    if (selected.Contains(state.Key) || (state.Key == machine && view.ShowMachine)) TopSolidHost.Elements.Show(id);
                    else if (state.Value) TopSolidHost.Elements.Hide(id);
                }
                cameraChanged = true;
                TopSolidHost.Visualization3D.SetViewCamera(temporaryDocument, nativeView, view.Eye, view.Look, view.Up, view.Angle, view.Radius);
                TopSolidHost.Visualization3D.RedrawView(temporaryDocument, nativeView);
                TopSolidHost.Visualization3D.SaveScreenShotBitmap(temporaryDocument, nativeView, file);
                if (!File.Exists(file) || new FileInfo(file).Length > 8 * 1024 * 1024) return Status("unavailable");
                var bytes = File.ReadAllBytes(file);
                if (!ToolpathCaptureView.IsBoundedPng(bytes)) return Status("unavailable");
                result = Status("ready"); result["format"] = "native-view-png";
                result["source"] = "TopSolid Automation Visualization3D.SaveScreenShotBitmap";
                result["data"] = Convert.ToBase64String(bytes); result["pathEntities"] = selected.Count;
                result["upToDate"] = TopSolidCamHost.Operations.IsUpToDate(new ElementExId(new ElementId(temporaryDocument, operation.Id)));
            }
            finally
            {
                try { if (began) TopSolidHost.Application.EndModification(false, false); }
                finally
                {
                    try
                    {
                        if (cameraChanged)
                        {
                            TopSolidHost.Visualization3D.SetViewCamera(document, nativeView, eye, look, up, angle, radius);
                            TopSolidHost.Visualization3D.RedrawView(document, nativeView);
                        }
                    }
                    finally { try { if (File.Exists(file)) File.Delete(file); Directory.Delete(folder); } catch (IOException) { } }
                }
            }
            // A native image is only returned after state restoration has succeeded.
            if (!TopSolidHost.Documents.Exists(document) || TopSolidHost.Documents.IsDirty(document) != dirty ||
                TopSolidHost.Documents.EditedDocument != edited || states.Any(s => TopSolidHost.Elements.IsVisible(s.Key) != s.Value)) return Status("changed");
            TopSolidHost.Visualization3D.GetViewCamera(document, nativeView, out var restoredEye, out var restoredLook, out var restoredUp, out var restoredAngle, out var restoredRadius);
            bool Same(double a, double b) => Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Abs(a));
            if (!Same(eye.X, restoredEye.X) || !Same(eye.Y, restoredEye.Y) || !Same(eye.Z, restoredEye.Z) ||
                !Same(look.X, restoredLook.X) || !Same(look.Y, restoredLook.Y) || !Same(look.Z, restoredLook.Z) ||
                !Same(up.X, restoredUp.X) || !Same(up.Y, restoredUp.Y) || !Same(up.Z, restoredUp.Z) ||
                !Same(angle, restoredAngle) || !Same(radius, restoredRadius)) return Status("changed");
            result["stateRestored"] = true;
            return result;
        }

        private static HashSet<ElementId> ToolpathEntities(IEnumerable<ElementId> roots)
        {
            var pending = new Queue<ElementId>(roots); var seen = new HashSet<ElementId>(); var paths = new HashSet<ElementId>();
            var watch = Stopwatch.StartNew();
            while (pending.Count > 0)
            {
                if (seen.Count > 1500 || watch.Elapsed > TimeSpan.FromSeconds(4)) throw new InvalidOperationException("Toolpath entity inspection exceeded its bound.");
                var element = pending.Dequeue(); if (!seen.Add(element)) continue;
                if (TopSolidHost.Elements.GetTypeFullName(element) == "TopSolid.Cam.NC.Kernel.DB.Entities.CLFile.CLFilePath") paths.Add(element);
                if (!TopSolidHost.Operations.IsOperation(element)) continue;
                foreach (var child in TopSolidHost.Elements.GetConstituents(element).Concat(TopSolidHost.Operations.GetChildren(element))) pending.Enqueue(child);
            }
            return paths;
        }
    }

    internal sealed class ToolpathCaptureView
    {
        internal Point3D Eye;
        internal Direction3D Look, Up;
        internal double Angle, Radius;
        internal bool ShowMachine;
        internal static ToolpathCaptureView Parse(JToken token)
        {
            if (token == null) return null;
            if (!(token is JObject view) || view.Count < 5 || view.Count > 6 || view.Properties().Any(p => !new[] { "eye", "look", "up", "angle", "radius", "machine" }.Contains(p.Name)) ||
                (view["machine"] != null && view["machine"].Type != JTokenType.Boolean))
                throw new ArgumentException("Invalid native preview camera.");
            var eye = Vector(view["eye"]); var look = Vector(view["look"]); var up = Vector(view["up"]);
            var angle = Number(view["angle"]); var radius = Number(view["radius"]);
            var ll = Math.Sqrt(look.Sum(n => n * n)); var ul = Math.Sqrt(up.Sum(n => n * n));
            if (ll < 1e-8 || ul < 1e-8 || Math.Abs(look.Zip(up, (a,b) => a*b).Sum() / ll / ul) > .999 || angle < 0 || angle > Math.PI / 2 || radius <= 0 || radius > 10000)
                throw new ArgumentException("Invalid native preview camera range.");
            return new ToolpathCaptureView { Eye = new Point3D(eye[0], eye[1], eye[2]), Look = new Direction3D(look[0]/ll, look[1]/ll, look[2]/ll),
                Up = new Direction3D(up[0]/ul, up[1]/ul, up[2]/ul), Angle = angle, Radius = radius, ShowMachine = (bool?)view["machine"] == true };
        }
        private static double[] Vector(JToken token)
        {
            if (!(token is JArray a) || a.Count != 3) throw new ArgumentException("Invalid native preview vector.");
            return a.Select(Number).ToArray();
        }
        private static double Number(JToken token)
        {
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)) throw new ArgumentException("Invalid native preview number.");
            var n = (double)token;
            if (double.IsNaN(n) || double.IsInfinity(n) || Math.Abs(n) > 1e6) throw new ArgumentException("Invalid native preview number range.");
            return n;
        }
        internal static bool IsBoundedPng(byte[] data)
        {
            if (data == null || data.Length < 33 || data.Length > 8 * 1024 * 1024 ||
                !data.Take(8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 }) ||
                data[12] != 73 || data[13] != 72 || data[14] != 68 || data[15] != 82) return false;
            long w = 0, h = 0;
            for (var i = 0; i < 4; i++) { w = (w << 8) | data[16+i]; h = (h << 8) | data[20+i]; }
            return w > 0 && h > 0 && w <= 8192 && h <= 8192 && w * h <= 16000000;
        }
    }
}
