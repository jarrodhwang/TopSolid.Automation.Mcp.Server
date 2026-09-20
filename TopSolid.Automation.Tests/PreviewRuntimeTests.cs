using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Preview;
using TopSolid.Automation.AI.Studio.Preview.Streaming;
using TopSolid.Automation.Mcp.Contracts;
using H = HelixToolkit.Wpf.SharpDX;

namespace TopSolid.Automation.Tests;

internal static class PreviewRuntimeTests
{
    internal static JObject PathResult(JObject operation, float end = 30) => new()
    {
        ["status"] = "ready", ["operation"] = operation.DeepClone(), ["format"] = "segments-f32", ["units"] = "mm", ["upAxis"] = "Z",
        ["segments"] = 2, ["partial"] = false, ["upToDate"] = true,
        ["data"] = Convert.ToBase64String(new float[] { 0, 0, 22, 20, 15, 22, 20, 15, 22, end, 28, 22 }.SelectMany(BitConverter.GetBytes).ToArray())
    };

    internal static async Task Run()
    {
        var native = new JObject { ["format"] = "native-view-png", ["stateRestored"] = true,
            ["data"] = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aLxkAAAAASUVORK5CYII=" };
        try { ToolpathPreviewScene.Read(native, CancellationToken.None); throw new Exception("Screenshot accepted as toolpath geometry"); }
        catch (InvalidDataException) { }
        var result = PathResult(new JObject { ["documentId"] = "fixture", ["id"] = 42 });
        var path = ToolpathPreviewScene.Read(result, CancellationToken.None);
        Check.True(path.Segments == 2 && path.Geometry.Positions?.Count == 4 && !path.Partial, "Validated path lost segments");
        foreach (var change in new Action<JObject>[] { p => p["units"] = "m", p => p["segments"] = 3,
            p => p["data"] = Convert.ToBase64String(Enumerable.Repeat(float.NaN, 12).SelectMany(BitConverter.GetBytes).ToArray()) })
        {
            var invalid = (JObject)result.DeepClone(); change(invalid);
            try { ToolpathPreviewScene.Read(invalid, CancellationToken.None); throw new Exception("Malformed path was displayed"); } catch (InvalidDataException) { }
        }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Check.ThrowsAsync<OperationCanceledException>(() => Task.Run(() => ToolpathPreviewScene.Read(result, cancelled.Token)));
        foreach (var failure in new[] { "", "offset", "identity", "truncated", "cancel" })
        {
            var client = new Chunks(failure); PreviewFilePayload? payload = null; string? localFile = null;
            try
            {
                if (failure == "")
                {
                    payload = await PreviewFileTransfer.DownloadAsync(client, new JObject { ["documentId"] = "fixture" }, CancellationToken.None);
                    localFile = payload.FilePath;
                    Check.True((await File.ReadAllBytesAsync(localFile!)).SequenceEqual(client.Bytes), "Chunked disk transfer changed source bytes");
                }
                else if (failure == "cancel")
                    await Check.ThrowsAsync<OperationCanceledException>(() => PreviewFileTransfer.DownloadAsync(client, new JObject { ["documentId"] = "fixture" }, CancellationToken.None));
                else await Check.ThrowsAsync<InvalidDataException>(() => PreviewFileTransfer.DownloadAsync(client, new JObject { ["documentId"] = "fixture" }, CancellationToken.None));
            }
            finally { payload?.Dispose(); }
            Check.True(client.Released && (localFile == null || !File.Exists(localFile)), "Transfer capability/temp file survived success or failure");
        }
    }

    private sealed class Chunks(string failure) : IGraphicPreviewClient
    {
        internal readonly byte[] Bytes = Enumerable.Range(0, GraphicPreviewQuality.ChunkBytes + 89).Select(i => (byte)(i % 251)).ToArray();
        private readonly string id = Guid.NewGuid().ToString("N");
        internal bool Released;
        public Task<JObject> GetGraphicPreviewAsync(JObject request, CancellationToken token)
        {
            if ((string?)request["action"] == "release") { Released = true; return Task.FromResult(new JObject { ["status"] = "released" }); }
            if ((string?)request["action"] != "read") return Task.FromResult(new JObject { ["status"] = "ready", ["format"] = "stl", ["transferId"] = id, ["byteLength"] = Bytes.Length });
            if (failure == "cancel") throw new OperationCanceledException();
            var offset = (int)request["offset"]!; var bytes = Bytes.Skip(offset).Take(GraphicPreviewQuality.ChunkBytes).ToArray();
            if (failure == "truncated") bytes = bytes[..^1];
            return Task.FromResult(new JObject { ["transferId"] = failure == "identity" ? Guid.NewGuid().ToString("N") : id,
                ["offset"] = failure == "offset" ? offset + 1 : offset, ["data"] = Convert.ToBase64String(bytes) });
        }
    }

    internal static async Task LivePaged(string server, string documentId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await using var client = new StdioMcpClient(); await client.ConnectAsync(server, timeout.Token);
        var target = new JObject { ["documentId"] = documentId };
        async Task<JObject> Read(string name, JObject args)
        {
            var receipt = await client.CallToolAsync(name, args, timeout.Token);
            Check.True(!receipt.IsError, "Native read failed: " + name);
            return receipt.StructuredContent as JObject ?? JObject.Parse((string)receipt.Content[0]["text"]!);
        }
        var before = await Read("topsolid_get_document_info", target);
        var watch = Stopwatch.StartNew();
        using var payload = await PreviewFileTransfer.DownloadAsync(client, target, timeout.Token);
        Check.Equal("ready", (string)payload.Metadata["status"]!, "Native file-backed export unavailable");
        Check.Equal("stl", (string)payload.Metadata["format"]!, "Live fixture expects binary STL");
        var exportMs = watch.Elapsed.TotalMilliseconds; watch.Restart();
        using var source = await StlChunkSource.OpenAsync(payload.FilePath!, PreviewResources.Detect(), timeout.Token);
        var indexMs = watch.Elapsed.TotalMilliseconds; watch.Restart();
        var first = await source.ReadAsync(0, timeout.Token); var last = await source.ReadAsync(source.Chunks.Length - 1, timeout.Token);
        Check.True(first.Indices.Length > 0 && last.Indices.Length > 0, "Native mesh tiles were empty");
        var decodeMs = watch.Elapsed.TotalMilliseconds;
        var operations = await Read("topsolid_list_cam_operation_summaries", new JObject { ["documentId"] = documentId, ["limit"] = 20 });
        var operation = operations["items"]!.OfType<JObject>().FirstOrDefault(o => (bool?)o["hasTool"] == true)?["operation"]?["element"] as JObject;
        var path = operation == null ? null : await client.GetToolpathPreviewAsync(operation, timeout.Token);
        if (path != null) path.Remove("data");
        var after = await Read("topsolid_get_document_info", target);
        Check.True(JToken.DeepEquals(before, after), "Read-only preview changed document info/dirty state");
        var report = new JObject { ["document"] = before.DeepClone(), ["export"] = payload.Metadata.DeepClone(),
            ["bytes"] = source.ByteLength, ["triangles"] = source.TriangleCount, ["chunks"] = source.Chunks.Length,
            ["exportAndTransferMilliseconds"] = exportMs, ["indexMilliseconds"] = indexMs, ["decodeTwoTilesMilliseconds"] = decodeMs,
            ["documentUnchanged"] = true, ["toolpath"] = path, ["peakWorkingSetMiB"] = Process.GetCurrentProcess().PeakWorkingSet64 / 1048576d,
            ["validation"] = "Actual native export, disk transfer, full index and two tile decodes. GPU drawing is verified separately with synthetic geometry." };
        var directory = Path.GetFullPath("artifacts/list-preview-20260918"); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "live-paged-preview.json"), report.ToString()); Console.WriteLine(report);
    }

    // Explicit GPU fixture: separate process from the deliberately software-only WPF layout tests.
    internal static Task Gpu(string? server = null, string? documentId = null)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.UnhandledException += (_, e) => { e.Handled = true; completion.TrySetException(e.Exception); dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); };
            dispatcher.BeginInvoke(async () =>
            {
                try { await ExerciseGpu(server, documentId); completion.TrySetResult(); }
                catch (Exception e) { completion.TrySetException(e); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true, Name = "Direct3D preview fixture" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }

    private static async Task ExerciseGpu(string? server, string? documentId)
    {
        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
        using var renderer = new GpuPreviewRenderer();
        var failed = false; renderer.Failed += () => failed = true;
        var window = new Window { Content = renderer.View, Width = 900, Height = 640, Left = -20000, Top = -20000,
            ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
        var directory = Path.GetFullPath("artifacts/list-preview-20260918"); Directory.CreateDirectory(directory);
        try
        {
            renderer.SetBackground(new LinearGradientBrush(Color.FromRgb(74, 101, 151), Color.FromRgb(231, 228, 228), 90));
            window.Show(); window.UpdateLayout();
            var scene = StlPreviewReader.Read(GraphicPreviewTests.StlFixture(), CancellationToken.None);
            renderer.Show(scene);
            renderer.ShowToolpath(ToolpathPreviewScene.Read(PathResult(new JObject()), CancellationToken.None).Geometry);
            renderer.CameraChanged(new OrthographicCamera(new Point3D(150, -200, 170), new Vector3D(-130, 215, -160), new Vector3D(0, 0, 1), 80)
                { NearPlaneDistance = .01, FarPlaneDistance = 10000 });
            await Task.Delay(700); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            Check.True(!failed, "Direct3D raised a render/device error");
            H.ViewportExtensions.SaveScreen(renderer.View, Path.Combine(directory, "direct3d-model-toolpath.png"));
            renderer.CameraChanged(new OrthographicCamera(new Point3D(150, -200, 170), new Vector3D(-130, 215, -160), new Vector3D(0, 0, 1), 80)
                { NearPlaneDistance = .01, FarPlaneDistance = 10000 }, perspective: true, fieldOfView: 35);
            await Task.Delay(250); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            Check.True(renderer.View.Camera is H.PerspectiveCamera && !failed, "Direct3D perspective camera did not render");
            H.ViewportExtensions.SaveScreen(renderer.View, Path.Combine(directory, "direct3d-perspective-model-toolpath.png"));
            var pixels = H.ViewportExtensions.RenderBitmap(renderer.View);
            Check.True(pixels != null && pixels.PixelWidth > 100, "Direct3D produced no framebuffer");
            var report = renderer.DeviceInfo(); report["fixture"] = "12-triangle model plus two synthetic path segments; no native CAM coordinates";
            report["framebufferWidth"] = pixels!.PixelWidth; report["framebufferHeight"] = pixels.PixelHeight;
            File.WriteAllText(Path.Combine(directory, "direct3d-device.json"), report.ToString()); Console.WriteLine(report);
            if (server != null && documentId != null)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                await using var client = new StdioMcpClient(); await client.ConnectAsync(server, timeout.Token);
                using var payload = await PreviewFileTransfer.DownloadAsync(client, new JObject { ["documentId"] = documentId }, timeout.Token);
                Check.Equal("ready", (string)payload.Metadata["status"]!, "Native paged GPU source unavailable");
                var resources = PreviewResources.Detect((long)report["dedicatedVideoMemoryBytes"]!);
                using var source = await StlChunkSource.OpenAsync(payload.FilePath!, resources, timeout.Token);
                var b = source.Bounds; var center = b.Center; var distance = Math.Max(1, b.Radius * 2);
                var camera = new OrthographicCamera { Position = new Point3D(center.X + distance * 2, center.Y - distance * 2, center.Z + distance * 2),
                    LookDirection = new Vector3D(-1, 1, -1), UpDirection = new Vector3D(0, 0, 1), Width = distance * 1.4, NearPlaneDistance = .01, FarPlaneDistance = distance * 20 };
                // Match the pane's camera-distance contract used by visibility selection.
                var direction = -camera.LookDirection; direction.Normalize(); camera.Position = new Point3D(center.X, center.Y, center.Z) + direction * distance * 4;
                renderer.Clear(); renderer.CameraChanged(camera);
                using var paged = new PagedPreviewSession(renderer.View, source, resources);
                var ready = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                paged.ResidencyChanged += (loaded, visible) => { if (loaded > 0 && loaded == visible) ready.TrySetResult(loaded); };
                paged.Failed += e => ready.TrySetException(e);
                var watch = Stopwatch.StartNew();
                paged.CameraChanged(camera, renderer.View.ActualWidth / renderer.View.ActualHeight);
                var loaded = await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
                await Task.Delay(700); Check.True(!failed, "Native model raised a GPU error");
                H.ViewportExtensions.SaveScreen(renderer.View, Path.Combine(directory, "direct3d-native-model.png"));
                var native = new JObject { ["name"] = payload.Metadata["name"], ["triangles"] = source.TriangleCount, ["bytes"] = source.ByteLength,
                    ["sourceChunks"] = source.Chunks.Length, ["residentChunks"] = loaded, ["tilesAndFirstFramesMilliseconds"] = watch.Elapsed.TotalMilliseconds,
                    ["adapter"] = report["adapter"], ["peakWorkingSetMiB"] = Process.GetCurrentProcess().PeakWorkingSet64 / 1048576d,
                    ["note"] = "Actual TopSolid native STL rendered with full-detail GPU tiles; no toolpath coordinates or FPS benchmark." };
                File.WriteAllText(Path.Combine(directory, "direct3d-native-model.json"), native.ToString()); Console.WriteLine(native);
            }
        }
        finally { window.Close(); }
    }
}
