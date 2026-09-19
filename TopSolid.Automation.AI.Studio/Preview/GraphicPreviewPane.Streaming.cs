using System.IO;
using System.Windows;
using System.Windows.Media.Media3D;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Preview.Streaming;

namespace TopSolid.Automation.AI.Studio.Preview;

internal sealed partial class GraphicPreviewPane
{
    private PreviewFilePayload? streamingFile;
    private IPreviewChunkSource? streamingSource;
    private PagedPreviewSession? pagedPreview;
    private const long PagedThresholdBytes = 16L * 1024 * 1024; // Switch strategy, never reject the source.
    internal bool IsPaged => pagedPreview != null;
    internal int ResidentTiles => pagedPreview?.ResidentCount ?? 0;
    internal long TotalTriangles => streamingSource?.TriangleCount ?? Scene?.Triangles ?? 0;

    private async Task<(JObject Metadata, PreviewScene? Scene)> ReadFilePreviewAsync(JObject document, long current, CancellationToken token)
    {
        var payload = await PreviewFileTransfer.DownloadAsync(client!, document, token);
        var retained = false;
        try
        {
            var result = payload.Metadata;
            if ((string?)result["status"] != "ready") return (result, null);
            if (document["documentId"] != null && !JToken.DeepEquals(document["documentId"], result["documentId"]))
                throw new InvalidDataException("Preview document mismatch.");
            var stl = (string?)result["format"] == "stl";
            if (stl ? (string?)result["units"] != "mm" || (string?)result["upAxis"] != "Z" :
                (string?)result["format"] != "glb" || (string?)result["units"] != "m" || (string?)result["upAxis"] != "Y")
                throw new InvalidDataException("Unsupported preview coordinates.");
            var length = new FileInfo(payload.FilePath!).Length;
            if (length > PagedThresholdBytes && gpu == null)
                return (new JObject { ["status"] = "GpuRequired" }, null);
            if (length > PagedThresholdBytes && gpu != null)
            {
                IPreviewChunkSource source = stl ? await StlChunkSource.OpenAsync(payload.FilePath!, GpuResources(), token) :
                    await GlbChunkSource.OpenAsync(payload.FilePath!, GpuResources(), token, (string?)result["colorEncoding"] == "srgb");
                if (disposed || generation != current) { source.Dispose(); token.ThrowIfCancellationRequested(); return (result, null); }
                streamingSource = source; streamingFile = payload; retained = true;
                return (result, SceneFor(source));
            }
            var maximum = stl ? StlPreviewReader.MaximumBytes : GlbPreviewReader.MaximumBytes;
            if (length > maximum) throw new InvalidDataException("A Direct3D adapter is required for disk-backed geometry.");
            var bytes = await File.ReadAllBytesAsync(payload.FilePath!, token);
            return (result, await Task.Run(() => stl ? StlPreviewReader.Read(bytes, token) : GlbPreviewReader.Read(bytes, token, (string?)result["colorEncoding"] == "srgb"), token));
        }
        finally { if (!retained) payload.Dispose(); }
    }

    private PreviewResources GpuResources()
    {
        long memory = 0;
        try
        {
            if (gpu?.View.EffectsManager?.Device is { } native)
            {
                using var device = native.QueryInterface<SharpDX.DXGI.Device>();
                using var adapter = device.Adapter;
                memory = adapter.Description.DedicatedVideoMemory;
            }
        }
        catch (SharpDX.SharpDXException) { }
        return PreviewResources.Detect(Math.Max(0, memory));
    }

    private static PreviewScene SceneFor(IPreviewChunkSource source)
    {
        var surfaces = new Model3DGroup(); surfaces.Freeze(); var edges = new Model3DGroup(); edges.Freeze();
        var b = source.Bounds;
        return new(surfaces, edges, new Rect3D(b.Min.X, b.Min.Y, b.Min.Z, b.Max.X - b.Min.X, b.Max.Y - b.Min.Y, b.Max.Z - b.Min.Z),
            (int)Math.Min(int.MaxValue, source.TriangleCount), true);
    }

    private void AttachStreaming()
    {
        if (streamingSource == null || gpu == null) return;
        pagedPreview?.Dispose();
        pagedPreview = new(gpu.View, streamingSource, GpuResources());
        pagedPreview.ResidencyChanged += (loaded, visible) =>
            cachedHint = hint.Text = StudioStrings.Get("Preview.Residency", loaded, visible, streamingSource.TriangleCount) + "\n" + StudioStrings.Get("Preview.MouseHint");
        pagedPreview.Failed += error => { App.DiagnosticLog.WriteException("preview.streaming", error, "A visible geometry tile could not load."); SetMessage("Preview.unavailable"); };
    }

    private void ClearStreaming()
    {
        pagedPreview?.Dispose(); pagedPreview = null; streamingSource?.Dispose(); streamingSource = null;
        streamingFile?.Dispose(); streamingFile = null;
    }

    private async Task OpenLocalPreviewAsync()
    {
        var picker = new Microsoft.Win32.OpenFileDialog { CheckFileExists = true,
            Filter = "CAD preview (*.stl;*.step;*.stp;*.iges;*.igs;*.brep)|*.stl;*.step;*.stp;*.iges;*.igs;*.brep" };
        if (picker.ShowDialog(Window.GetWindow(this)) != true) return;
        await LoadLocalFileAsync(picker.FileName);
    }

    internal async Task LoadLocalFileAsync(string file)
    {
        if (disposed) return;
        var current = ++generation; loading?.Cancel(); loading?.Dispose(); loading = new(); var token = loading.Token;
        ClearScene();
        SetMessage("Preview.Loading"); SetBusy(true);
        PreviewFilePayload? imported = null;
        try
        {
            var stl = Path.GetExtension(file).Equals(".stl", StringComparison.OrdinalIgnoreCase);
            if (!stl && !OcctPreviewImporter.IsAvailable) { SetMessage("Preview.OcctUnavailable"); return; }
            if (!stl) imported = await OcctPreviewImporter.ImportAsync(file, token);
            var sourceFile = imported?.FilePath ?? file;
            if (gpu == null) { SetMessage("Preview.GpuRequired"); return; }
            var source = await StlChunkSource.OpenAsync(sourceFile, GpuResources(), token);
            if (disposed || current != generation) { source.Dispose(); return; }
            streamingSource = source; streamingFile = imported; imported = null;
            caption.Text = StudioStrings.Get("Preview.LocalReadOnly") + " · " + Path.GetFileName(file) + (stl ? "" : " · OCCT");
            hint.Text = StudioStrings.Get("Preview.LocalReadOnly");
            ShowScene(SceneFor(source));
            await PresentCompleteSceneAsync(current, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            App.DiagnosticLog.WriteException("preview.local", error, "Local CAD preview could not load.");
            if (!disposed && generation == current) SetMessage("Preview.unavailable");
        }
        finally { imported?.Dispose(); if (!disposed && generation == current) SetBusy(false); }
    }
}
