using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Maths;
using TopSolid.Automation.AI.Studio.Preview.Streaming;
using H = HelixToolkit.Wpf.SharpDX;
using D = HelixToolkit.SharpDX;
using N = System.Numerics;
using Color = System.Windows.Media.Color;

namespace TopSolid.Automation.AI.Studio.Preview;

/// <summary>Visible full-detail tiles under an adapter-specific residency budget.</summary>
internal sealed class PagedPreviewSession : IDisposable
{
    private readonly H.Viewport3DX view;
    private readonly H.GroupModel3D root = new();
    private readonly Dictionary<int, H.MeshGeometryModel3D> resident = [];
    private readonly IPreviewChunkSource source;
    private readonly PreviewResources resources;
    private readonly Dictionary<Color, H.PhongMaterial> materials = [];
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task Ready => ready.Task;
    private CancellationTokenSource? pending;
    private readonly SemaphoreSlim updateGate = new(1, 1);
    private bool disposed;
    internal event Action<int, int>? ResidencyChanged;
    internal event Action<Exception>? Failed;
    internal long TotalTriangles => source.TriangleCount;
    internal int ResidentCount => resident.Count;

    internal PagedPreviewSession(H.Viewport3DX view, IPreviewChunkSource source, PreviewResources resources)
    {
        this.view = view; this.source = source; this.resources = resources;
        view.Items.Add(root);
    }

    internal void CameraChanged(OrthographicCamera camera, double aspect)
    {
        if (disposed) return;
        pending?.Cancel(); pending?.Dispose(); pending = new();
        var direction = -camera.LookDirection; direction.Normalize();
        var target = camera.Position - direction * Math.Max(1e-4, source.Bounds.Radius * 2) * 4;
        var request = new PreviewCamera(new(target.X, target.Y, target.Z), new((float)direction.X, (float)direction.Y, (float)direction.Z),
            new((float)camera.UpDirection.X, (float)camera.UpDirection.Y, (float)camera.UpDirection.Z), camera.Width, aspect);
        _ = UpdateAsync(request, pending.Token);
    }

    private async Task UpdateAsync(PreviewCamera camera, CancellationToken token)
    {
        var entered = false;
        try
        {
            // Coalesce camera motion. Keep the current tiles visible until the new plan is known.
            await Task.Delay(80, token);
            await updateGate.WaitAsync(token); entered = true;
            var budget = Math.Min(resources.GpuResidentBytes / 2, resources.CpuResidentBytes / 3);
            var plan = await Task.Run(() => PreviewResidency.Select(source.Chunks, camera, budget), token);
            token.ThrowIfCancellationRequested();
            // Never reveal a bounding-box/wireframe stand-in as a completed model.
            if (plan.Proxies.Count > 0) throw new InvalidOperationException("Visible model exceeds the preview residency budget.");
            var wanted = plan.Detailed.Select(c => c.Id).ToHashSet();
            foreach (var id in resident.Keys.Where(id => !wanted.Contains(id)).ToArray())
            { var model = resident[id]; root.Children.Remove(model); model.Dispose(); resident.Remove(id); }
            ResidencyChanged?.Invoke(resident.Count, plan.Detailed.Count + plan.Proxies.Count);
            await Parallel.ForEachAsync(plan.Detailed.Where(c => !resident.ContainsKey(c.Id)).ToArray(),
                new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = resources.Workers }, async (chunk, ct) =>
                {
                    var data = await source.ReadAsync(chunk.Id, ct).ConfigureAwait(false);
                    var geometry = new D.MeshGeometry3D { Positions = new HelixToolkit.Vector3Collection(data.Positions),
                        Normals = new HelixToolkit.Vector3Collection(data.Normals), Indices = new HelixToolkit.IntCollection(data.Indices) };
                    await view.Dispatcher.InvokeAsync(() =>
                    {
                        if (disposed || ct.IsCancellationRequested) return;
                        var color = data.Chunk.Color ?? TopSolidPreviewPalette.Surface;
                        if (!materials.TryGetValue(color, out var material)) materials[color] = material = GpuPreviewRenderer.Material(color);
                        var model = new H.MeshGeometryModel3D { Geometry = geometry, Material = material, IsTransparent = color.A < 255, IsHitTestVisible = false,
                            CullMode = SharpDX.Direct3D11.CullMode.None,
                            Transform = new TranslateTransform3D(data.Origin.X, data.Origin.Y, data.Origin.Z) };
                        resident.Add(chunk.Id, model); root.Children.Add(model);
                    });
                });
            token.ThrowIfCancellationRequested();
            ResidencyChanged?.Invoke(resident.Count, plan.Detailed.Count + plan.Proxies.Count);
            ready.TrySetResult();
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or ArgumentException or SharpDX.SharpDXException)
        { if (!disposed && !token.IsCancellationRequested) { ready.TrySetException(error); Failed?.Invoke(error); } }
        finally { if (entered) updateGate.Release(); }
    }

    public void Dispose()
    {
        if (disposed) return; disposed = true; pending?.Cancel(); pending?.Dispose();
        ready.TrySetCanceled();
        view.Items.Remove(root); foreach (var model in resident.Values) model.Dispose(); resident.Clear();
        root.Dispose(); source.Dispose();
        // Waiters hold the gate until their cancelled reads drain; do not dispose it underneath them.
    }
}
