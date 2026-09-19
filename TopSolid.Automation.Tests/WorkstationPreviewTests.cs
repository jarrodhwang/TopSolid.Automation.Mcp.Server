using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Preview;
using TopSolid.Automation.AI.Studio.Preview.Streaming;
using H = HelixToolkit.Wpf.SharpDX;

namespace TopSolid.Automation.Tests;

internal static class WorkstationPreviewTests
{
    internal static Task Run()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.UnhandledException += (_, e) => { e.Handled = true; done.TrySetException(e.Exception); };
            dispatcher.BeginInvoke(async () =>
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                try { await Exercise(); done.TrySetResult(); }
                catch (Exception e) { done.TrySetException(e); }
                finally { app.Shutdown(); dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return done.Task;
    }

    private static async Task Exercise()
    {
        Check.Equal(Colors.Yellow, TopSolidPreviewPalette.Toolpath(ToolpathColorRole.Feed), "Native feed palette changed");
        Check.Equal(Colors.Lime, TopSolidPreviewPalette.Toolpath(ToolpathColorRole.Rapid), "Native rapid palette changed");
        Check.Equal(Color.FromRgb(251, 126, 20), TopSolidPreviewPalette.Toolpath(ToolpathColorRole.LeadIn), "Native lead-in palette changed");
        Check.Equal(Colors.Magenta, TopSolidPreviewPalette.Sketch(SketchColorRole.Underconstrained), "Native sketch palette changed");
        Check.Equal(Colors.Blue, TopSolidPreviewPalette.Sketch(SketchColorRole.FullyConstrained), "Native sketch palette changed");
        var pathResult = PreviewRuntimeTests.PathResult(new JObject { ["documentId"] = "fixture", ["id"] = 1 });
        pathResult["motionRoles"] = new JArray("Feed", "Rapid");
        var coloredPath = ToolpathPreviewScene.Read(pathResult, CancellationToken.None);
        Check.True(coloredPath.Geometry.Colors![0].Red == 1 && coloredPath.Geometry.Colors[0].Green == 1 &&
            coloredPath.Geometry.Colors[2].Red == 0 && coloredPath.Geometry.Colors[2].Green == 1, "Native motion palette did not reach GPU vertices");
        Check.True(coloredPath.Ribbons(new(0,0,1), 1).Children.Count == 2, "WPF fallback lost motion colors");
        Check.True(OcctPreviewImporter.IsAvailable, "OCCT worker was not bundled in the test output");
        var folder = Path.Combine(Path.GetTempPath(), "TopSolid-workstation-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder); var step = Path.Combine(folder, "검증 부품.step");
        try
        {
            var start = new ProcessStartInfo(OcctPreviewImporter.WorkerPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("--write-test-step"); start.ArgumentList.Add(step);
            using (var process = Process.Start(start)!)
            {
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(); await stdout; var error = await stderr;
                Check.True(process.ExitCode == 0, "OCCT fixture failed: " + error);
            }
            using var imported = await OcctPreviewImporter.ImportAsync(step, CancellationToken.None);
            using (var source = await StlChunkSource.OpenAsync(imported.FilePath!, PreviewResources.Detect(), CancellationToken.None))
                Check.True(source.TriangleCount == 12 && source.Bounds.Max == new PreviewPoint(40,30,20), "OCCT STEP units, geometry or Unicode path changed");
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/TopSolid.Automation.AI.Studio;component/Appearance/TopSolidStyles.xaml", UriKind.Relative) });
            TopSolidTheme.Apply(new(false, "Preview test", "Synthetic"));
            using var pane = new GraphicPreviewPane(null, null);
            var window = new Window { Content = pane, Width = 900, Height = 650, ShowInTaskbar = false, ShowActivated = false, Left = -16000, Top = -16000,
                WindowStartupLocation = WindowStartupLocation.Manual };
            try
            {
                window.Show(); await Task.Delay(200);
                await pane.LoadLocalFileAsync(step);
                var timeout = Stopwatch.StartNew();
                while (pane.ResidentTiles == 0 && timeout.Elapsed < TimeSpan.FromSeconds(20)) await Task.Delay(40);
                Check.True(pane.IsPaged && pane.TotalTriangles == 12 && pane.ResidentTiles == 1, "Local OCCT file did not reach paged Studio viewport: " + pane.StatusText);
                var position = pane.Camera.Position; pane.Orbit(.2, .1); Check.True(position != pane.Camera.Position, "Paged viewport lost TopSolid camera controls");
                pane.Zoom(.8); pane.Fit();
                await Task.Delay(500);
                var view = Descendants<H.Viewport3DX>(pane).Single();
                var directory = Path.GetFullPath("artifacts/workstation-preview/evidence"); Directory.CreateDirectory(directory);
                H.ViewportExtensions.SaveScreen(view, Path.Combine(directory, "occt-studio-preview.png"));
                var report = new JObject { ["occtKernel"] = "7.9.3", ["unicodeStepImported"] = true, ["triangles"] = pane.TotalTriangles,
                    ["residentTiles"] = pane.ResidentTiles, ["studioViewport"] = true, ["cameraControls"] = true,
                    ["note"] = "Small STEP fixture in the actual Studio preview pane. Not a 15 GB native CAM or kernel-competitiveness benchmark." };
                File.WriteAllText(Path.Combine(directory, "occt-studio-preview.json"), report.ToString()); Console.WriteLine(report);
                // An invalid next file must remove prior geometry and keep the window usable.
                var bad = Path.Combine(folder, "invalid.stl"); File.WriteAllBytes(bad, [1,2,3]);
                await pane.LoadLocalFileAsync(bad);
                Check.True(pane.Scene == null && !pane.IsLoading && !pane.IsPaged, "Failed replacement retained stale geometry");
                File.Delete(bad);
            }
            finally { window.Close(); }
        }
        finally { File.Delete(Path.Combine(folder, "invalid.stl")); File.Delete(step); Directory.Delete(folder); }
        Console.WriteLine("PASS workstation preview: OCCT, Unicode, dimensions, actual Studio GPU pane, camera, palette, failed replacement");
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); if (child is T item) yield return item;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
