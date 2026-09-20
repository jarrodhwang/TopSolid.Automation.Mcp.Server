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
internal static class NativeToolpathLiveTests
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
        internal readonly List<JObject> Captures = [];
        public Task<JObject> GetGraphicPreviewAsync(JObject request, CancellationToken token) => server.GetGraphicPreviewAsync(request, token);
        public async Task<JObject> GetToolpathPreviewAsync(JObject request, CancellationToken token)
        {
            var result = await server.GetToolpathPreviewAsync(request, token);
            Captures.Add((JObject)result.DeepClone()); return result;
        }
    }

    private static async Task Exercise(string serverPath, string document, int operation)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await using var server = new StdioMcpClient(); await server.ConnectAsync(serverPath, timeout.Token);
        var client = new PreviewClient(server);
        var identity = new JObject { ["documentId"] = document, ["id"] = operation };
        using var pane = new GraphicPreviewPane(client, new JObject { ["documentId"] = document, ["operation"] = identity });
        var window = new Window { Content = pane, Width = 1000, Height = 780, Left = -20000, Top = -20000,
            ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
        var output = Path.GetFullPath("artifacts/toolpath-repair"); Directory.CreateDirectory(output);
        try
        {
            window.Show(); await Until(() => !pane.IsLoading && pane.HasNativeToolpathImage);
            Check.True(pane.ToolpathStatus.Contains("native rendered view"), "Native capture was not labeled accurately");
            Render("studio-native-toolpath.png");
            var first = (string)client.Captures[^1]["data"]!; var count = client.Captures.Count;
            pane.SetView("top"); pane.Zoom(.8);
            await Until(() => client.Captures.Count > count && pane.HasNativeToolpathImage);
            Check.True(first != (string?)client.Captures[^1]["data"], "Top camera/zoom did not change the real native capture");
            Render("studio-native-toolpath-top.png");
            Check.True(client.Captures.All(c => (string?)c["status"] == "ready" && (bool?)c["stateRestored"] == true && JToken.DeepEquals(c["operation"], identity)),
                "Live UI changed operation identity or failed native state restoration");
            var report = new JObject { ["operation"] = identity.DeepClone(), ["captures"] = client.Captures.Count, ["status"] = pane.ToolpathStatus,
                ["allStatesRestored"] = true, ["viewChanged"] = true, ["rawCoordinatesRetrieved"] = false };
            File.WriteAllText(Path.Combine(output, "studio-native-toolpath.json"), report.ToString());
            Console.WriteLine("PASS live Automation toolpath UI: " + report);
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
