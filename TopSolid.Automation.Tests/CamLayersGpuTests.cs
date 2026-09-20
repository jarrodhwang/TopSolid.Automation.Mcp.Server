using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Preview;
using H = HelixToolkit.Wpf.SharpDX;

namespace TopSolid.Automation.Tests;

internal static class CamLayersGpuTests
{
    internal static Task Run(string file)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(async () =>
            {
                try { await Exercise(file); done.SetResult(); }
                catch (Exception error) { done.SetException(error); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return done.Task;
    }
    private static async Task Exercise(string file)
    {
        var watch = Stopwatch.StartNew();
        var scenes = await CamContextPreview.ReadAsync(file, CancellationToken.None);
        var decode = watch.Elapsed.TotalMilliseconds;
        Check.True(scenes.HasRemaining && scenes.Original != null, "Native stock layers missing");
        var stockOnly = PreviewScene.Build(scenes.Work.GpuMeshes.Where(m => m.Color.A < 255).Select(m => new PreviewMesh(
            m.Geometry.Positions!.Select(p => new Point3D(p.X,p.Y,p.Z)).ToArray(), m.Geometry.Indices!.ToArray(), m.Color,
            m.Geometry.Normals!.Select(n => new Vector3D(n.X,n.Y,n.Z)).ToArray())).ToArray(), CancellationToken.None);
        using var renderer = new GpuPreviewRenderer(); var failed = false; renderer.Failed += () => failed = true;
        var window = new Window { Content = renderer.View, Width = 1100, Height = 800, Left = -20000, Top = -20000,
            WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false, ShowActivated = false };
        var output = Path.GetFullPath("artifacts/cam-layer-repair"); Directory.CreateDirectory(output);
        try
        {
            renderer.SetBackground(new LinearGradientBrush(Color.FromRgb(74, 101, 151), Color.FromRgb(231, 228, 228), 90));
            window.Show(); window.UpdateLayout();
            foreach (var (name, scene) in new[] { ("part",scenes.Part), ("remaining",scenes.Work), ("original",scenes.Original!), ("machine",scenes.Machine), ("stock-only",stockOnly) })
            {
                renderer.Show(scene); var b = scene.Bounds;
                var center = new Point3D(b.X + b.SizeX / 2, b.Y + b.SizeY / 2, b.Z + b.SizeZ / 2);
                var extent = new Vector3D(b.SizeX, b.SizeY, b.SizeZ).Length;
                var direction = new Vector3D(.433, -.75, .5); direction.Normalize();
                renderer.CameraChanged(new OrthographicCamera(center + direction * extent * 4, -direction, new Vector3D(0,0,1), extent * 1.22)
                    { NearPlaneDistance = extent / 10000, FarPlaneDistance = extent * 10 });
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)); await renderer.WaitForFrameAsync(deadline.Token);
                await Task.Delay(200);
                Check.True(!failed, "Native CAM GPU rendering failed");
                H.ViewportExtensions.SaveScreen(renderer.View, Path.Combine(output, "gpu-" + name + ".png"));
            }
            var report = renderer.DeviceInfo(); report["decodedMilliseconds"] = decode;
            report["partTriangles"] = scenes.Part.Triangles; report["remainingAndPartTriangles"] = scenes.Work.Triangles;
            report["originalAndPartTriangles"] = scenes.Original!.Triangles; report["machineAndWorkTriangles"] = scenes.Machine.Triangles;
            report["validation"] = "Actual exported CAM geometry, five layer combinations including isolated stock, Direct3D frames; no synthetic path presented as a native path.";
            File.WriteAllText(Path.Combine(output, "gpu-layers.json"), report.ToString()); Console.WriteLine(report);
        }
        finally { window.Close(); }
    }
}
