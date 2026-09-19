using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Preview;
using TopSolid.Automation.AI.Studio.Preview.Streaming;

namespace TopSolid.Automation.Tests;

internal static class LiveToolPreviewReview
{
    // Kept separate from raster/GPU review so a locked Windows desktop cannot be mistaken for visual proof.
    internal static async Task ValidateData(string server)
    {
        var folder = Path.GetFullPath("artifacts/tool-preview-fix"); Directory.CreateDirectory(folder);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await using var client = new StdioMcpClient(); await client.ConnectAsync(server, timeout.Token);
        async Task<JObject> Read(string name, JObject arguments)
        {
            Check.True(!client.Tools.Single(t => t.Name == name).RequiresConfirmation, "Data review attempted a CAD write");
            var result = await client.CallToolAsync(name, arguments, timeout.Token);
            Check.True(!result.IsError, name + ": " + result.Content);
            return result.StructuredContent ?? JObject.Parse((string)result.Content[0]["text"]!);
        }
        var before = await Read("topsolid_get_document_info", new());
        var document = (JObject)before["document"]!;
        var arguments = new JObject { ["documentId"] = document["documentId"]!.DeepClone(), ["limit"] = 100 };
        var tools = await Read("topsolid_list_cam_tools", arguments);
        File.WriteAllText(Path.Combine(folder, "live-tools.json"), tools.ToString());
        var sources = new QuestionSources(); sources.Capture("tools", "topsolid_list_cam_tools", arguments, new() { StructuredContent = tools });
        var question = sources.Create(JObject.Parse("{question:'Tools',kind:'select',itemKind:'tool',sources:[{toolCallId:'tools',path:'/items'}]}"));
        Check.True(question.Choices.Count > 0, "No CAM tools available for native validation");
        var results = new JArray();
        foreach (var choice in question.Choices)
        {
            var target = question.PreviewTargetFor(choice.Key);
            Check.True(target != null && !JToken.DeepEquals(target["documentId"], document["documentId"]), "Tool resolved to CAM owner");
            Check.True(choice.Label.StartsWith("T ") && choice.IconKey != "cam-tool-generic", "Missing tool number/name/type");
            using var payload = await PreviewFileTransfer.DownloadAsync(client, target!, timeout.Token);
            Check.True((string?)payload.Metadata["status"] == "ready" && (string?)payload.Metadata["format"] == "glb" &&
                (string?)payload.Metadata["colorEncoding"] == "srgb" && JToken.DeepEquals(payload.Metadata["documentId"], target!["documentId"]), "Wrong tool preview or lost native materials");
            using var source = await GlbChunkSource.OpenAsync(payload.FilePath!, PreviewResources.Detect(), timeout.Token, true);
            foreach (var chunk in source.Chunks) await source.ReadAsync(chunk.Id, timeout.Token);
            results.Add(new JObject { ["label"] = choice.Label, ["icon"] = choice.IconKey, ["target"] = target,
                ["triangles"] = source.TriangleCount, ["colors"] = new JArray(source.Chunks.Select(c => c.Color!.Value.ToString()).Distinct()) });
        }
        var after = await Read("topsolid_get_document_info", new());
        Check.True(JToken.DeepEquals(before, after), "Tool preview changed the active document/dirty state");
        var report = new JObject { ["server"] = Path.GetFullPath(server), ["tools"] = results, ["nativeWrites"] = 0,
            ["documentUnchanged"] = true, ["validation"] = "Native API, GLB geometry and material data; no raster/GPU assertion" };
        File.WriteAllText(Path.Combine(folder, "live-tool-data.json"), report.ToString()); Console.WriteLine(report);
    }

    internal static Task Run(string server)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(async () =>
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/TopSolid.Automation.AI.Studio;component/Appearance/TopSolidStyles.xaml", UriKind.Relative) });
                try { await Review(server); done.TrySetResult(); }
                catch (Exception ex) { done.TrySetException(ex); }
                finally { app.Shutdown(); }
            });
            dispatcher.UnhandledException += (_, e) => { e.Handled = true; done.TrySetException(e.Exception); dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); };
            Dispatcher.Run();
        }) { IsBackground = true, Name = "Native tool/appearance review" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return done.Task;
    }

    private static async Task Review(string server)
    {
        var folder = Path.GetFullPath("artifacts/tool-preview-fix"); Directory.CreateDirectory(folder);
        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
        StudioStrings.Apply("ko"); TopSolidTheme.Apply(new(false, "TopSolid Classic", "Native preview review"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await using var client = new StdioMcpClient(); await client.ConnectAsync(server, timeout.Token);
        async Task<JObject> Read(string tool, JObject args)
        {
            Check.True(!client.Tools.Single(t => t.Name == tool).RequiresConfirmation, "Review attempted a CAD write");
            var result = await client.CallToolAsync(tool, args, timeout.Token);
            Check.True(!result.IsError, tool + ": " + result.Content);
            return result.StructuredContent ?? JObject.Parse((string)result.Content[0]["text"]!);
        }
        var before = await Read("topsolid_get_document_info", new());
        var doc = before["document"] as JObject ?? throw new InvalidOperationException("No active CAM document.");
        var args = new JObject { ["documentId"] = doc["documentId"]!.DeepClone(), ["limit"] = 100 };
        var tools = await Read("topsolid_list_cam_tools", args);
        File.WriteAllText(Path.Combine(folder, "live-tools.json"), tools.ToString());
        var sources = new QuestionSources(); sources.Capture("native-tools", "topsolid_list_cam_tools", args, new() { StructuredContent = tools });
        var question = sources.Create(JObject.Parse("{question:'공구 리스트',kind:'select',itemKind:'tool',sources:[{toolCallId:'native-tools',path:'/items'}]}"));
        var report = new JObject { ["server"] = Path.GetFullPath(server), ["tools"] = new JArray(), ["nativeWrites"] = 0 };
        // Direct3D visual review requires a visible desktop: the compositor may suspend offscreen surfaces.
        var window = new QuestionWindow(question, client) { Width = 1250, Height = 800, ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        try
        {
            window.Show(); window.UpdateLayout();
            var pane = Descendants<GraphicPreviewPane>(window).Single();
            for (var i = 0; i < question.Choices.Count; i++)
            {
                var choice = question.Choices[i]; var target = question.PreviewTargetFor(choice.Key);
                Check.True(target != null && !JToken.DeepEquals(target["documentId"], doc["documentId"]), "Tool resolved to CAM document");
                Check.True(choice.Label.StartsWith("T ") && choice.IconKey != "cam-tool-generic", "Native tool lacks its pocket/name/type artwork");
                window.ChoiceList.SelectedIndex = i;
                var started = Stopwatch.StartNew();
                if (i == 0) { Check.True(!pane.IsScenePresented, "An unfinished scene was revealed"); Capture(window, "tool-loading.png"); }
                await WaitReady(pane, timeout.Token);
                ((JArray)report["tools"]!).Add(new JObject { ["label"] = choice.Label, ["icon"] = choice.IconKey, ["preview"] = target,
                    ["triangles"] = pane.TotalTriangles, ["seconds"] = started.Elapsed.TotalSeconds,
                    ["colors"] = new JArray(pane.Scene!.GpuMeshes.Select(m => m.Color.ToString()).Distinct()) });
                if (i < 3) Capture(window, "tool-" + (i+1) + "-light.png");
            }
            TopSolidTheme.Apply(new(true, "TopSolid Dark", "Native preview review"));
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); Capture(window, "tool-dark.png");
            pane.SetTarget(new JObject { ["documentId"] = doc["documentId"]!.DeepClone() });
            var camStarted = Stopwatch.StartNew(); await WaitReady(pane, timeout.Token);
            Check.True(pane.IsPaged && pane.ResidentTiles > 0, "Native color model was not paged");
            report["cam"] = new JObject { ["triangles"] = pane.TotalTriangles, ["tiles"] = pane.ResidentTiles, ["seconds"] = camStarted.Elapsed.TotalSeconds };
            Capture(window, "cam-dark.png");
            TopSolidTheme.Apply(new(false, "TopSolid Classic", "Native preview review"));
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); Capture(window, "cam-light.png");
        }
        finally { window.Close(); }
        var after = await Read("topsolid_get_document_info", new());
        Check.True(JToken.DeepEquals(before, after), "Read-only previews changed the active document/dirty state");
        report["documentUnchanged"] = true; report["peakWorkingSetMiB"] = Process.GetCurrentProcess().PeakWorkingSet64 / 1048576d;
        File.WriteAllText(Path.Combine(folder, "live-review.json"), report.ToString()); Console.WriteLine(report);

        void Capture(Window target, string name)
        {
            target.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)Math.Ceiling(target.ActualWidth), (int)Math.Ceiling(target.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(target);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            Check.True(pixels.Any(b => b != 0), "Desktop raster is empty; visual verification is unavailable");
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(Path.Combine(folder, name)); encoder.Save(output);
        }
    }
    private static async Task WaitReady(GraphicPreviewPane pane, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        while (pane.IsLoading || !pane.IsScenePresented)
        { if (watch.Elapsed > TimeSpan.FromSeconds(90)) throw new TimeoutException("Preview did not present: " + pane.StatusText); await Task.Delay(30, token); }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); if (child is T value) yield return value; foreach (var item in Descendants<T>(child)) yield return item; }
    }
}
