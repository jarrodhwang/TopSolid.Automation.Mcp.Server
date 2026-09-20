using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Preview;

namespace TopSolid.Automation.Tests;

// Explicit live fixture: uses an already-calculated operation. Never saves or calculates CAM.
internal static class ToolpathGeometryLiveTests
{
    internal static Task Run(string server, string document, int operation)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(async () =>
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                try
                {
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/TopSolid.Automation.AI.Studio;component/Appearance/TopSolidStyles.xaml", UriKind.Relative) });
                    RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
                    StudioStrings.Apply("en");
                    await Exercise(server, document, operation); completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally { app.Shutdown(); dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true, Name = "Live Automation toolpath UI" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }

    private sealed class PreviewClient(StdioMcpClient server) : IGraphicPreviewClient, IToolpathPreviewClient
    {
        internal readonly List<JObject> Results = [];
        internal int ModelRequests;
        public Task<JObject> GetGraphicPreviewAsync(JObject request, CancellationToken token)
        { ModelRequests++; return server.GetGraphicPreviewAsync(request, token); }
        public async Task<JObject> GetToolpathPreviewAsync(JObject request, CancellationToken token)
        {
            var result = await server.GetToolpathPreviewAsync(request, token);
            Results.Add((JObject)result.DeepClone()); return result;
        }
    }

    private static async Task Exercise(string serverPath, string document, int operation)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await using var server = new StdioMcpClient(); await server.ConnectAsync(serverPath, timeout.Token);
        async Task<JObject> ReadDocument()
        {
            var receipt = await server.CallToolAsync("topsolid_get_document_info", new JObject { ["documentId"] = document }, timeout.Token);
            Check.True(!receipt.IsError, "Cannot read the CAM document");
            return receipt.StructuredContent as JObject ?? JObject.Parse((string)receipt.Content[0]["text"]!);
        }
        var before = await ReadDocument();
        var client = new PreviewClient(server);
        var identity = new JObject { ["documentId"] = document, ["id"] = operation };
        using var pane = new GraphicPreviewPane(client, new JObject { ["documentId"] = document, ["camContext"] = true, ["operation"] = identity });
        var window = new Window { Content = pane, Width = 1000, Height = 780, Left = -20000, Top = -20000,
            ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
        var output = Path.GetFullPath("artifacts/toolpath-repair"); Directory.CreateDirectory(output);
        try
        {
            window.Show(); await Until(() => !pane.IsLoading && client.Results.Count > 0);
            var result = client.Results.Single(); var status = (string?)result["status"];
            Check.True(pane.Scene != null && pane.HasMachineContext, "Native model/machine geometry unavailable");
            Check.True((string?)result["source"] == "IToolPath" && (string?)result["format"] == "segments-f32" &&
                JToken.DeepEquals(result["operation"], identity), "Preview did not use IToolPath coordinate geometry");
            if (status == "ready") Check.True(pane.ToolpathSegments > 0, "API coordinates were not rendered");
            else Check.True(status == "coordinatesUnavailable" && pane.ToolpathSegments == 0 &&
                pane.ToolpathStatus == StudioStrings.Get("Preview.PathCoordinatesUnavailable"), "Missing coordinates were not reported accurately");
            Render("studio-itoolpath-model.png");
            var requests = client.ModelRequests; var camera = pane.Camera.Position;
            pane.SetView("top"); pane.Zoom(.8);
            pane.SetMachineVisible(true); await Task.Delay(500);
            Check.True(camera != pane.Camera.Position && client.Results.Count == 1 && client.ModelRequests == requests,
                "Local camera/machine toggle requested another scan or export");
            Render("studio-itoolpath-machine.png");
            var after = await ReadDocument();
            Check.True(JToken.DeepEquals(before, after), "Preview changed document info or dirty state");
            var metadata = (JObject)result.DeepClone(); metadata.Remove("data"); metadata.Remove("columns");
            var report = new JObject { ["operation"] = identity.DeepClone(), ["toolpath"] = metadata, ["statusText"] = pane.ToolpathStatus,
                ["documentUnchanged"] = true, ["navigationWithoutRequests"] = true, ["rawCoordinatesRetrieved"] = status == "ready",
                ["validation"] = status == "ready" ? "Live IToolPath geometry rendered" : "Live API supplies no coordinates; model, machine and failure handling verified" };
            File.WriteAllText(Path.Combine(output, "studio-itoolpath-geometry.json"), report.ToString());
            Console.WriteLine(report);
        }
        finally { window.Close(); }

        async Task Until(Func<bool> condition)
        {
            var watch = Stopwatch.StartNew();
            while (!condition())
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(45)) throw new TimeoutException("Native toolpath UI: " + pane.StatusText + " / " + pane.ToolpathStatus);
                window.UpdateLayout(); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); await Task.Delay(40);
            }
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); await Task.Delay(100);
        }
        void Render(string name)
        {
            window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(output, name)); encoder.Save(file);
        }
    }
}
