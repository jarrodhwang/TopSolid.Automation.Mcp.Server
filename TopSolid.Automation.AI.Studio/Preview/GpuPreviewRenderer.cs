using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Newtonsoft.Json.Linq;
using Color4 = HelixToolkit.Maths.Color4;
using H = HelixToolkit.Wpf.SharpDX;
using D = HelixToolkit.SharpDX;
using TopSolid.Automation.AI.Studio.Appearance;

namespace TopSolid.Automation.AI.Studio.Preview;

internal sealed record GpuMesh(D.MeshGeometry3D Geometry, Color Color)
{
    // Runs on CPU workers; only the immutable display buffers are handed to Direct3D.
    internal static GpuMesh Build(PreviewMesh mesh, CancellationToken token)
    {
        var positions = new HelixToolkit.Vector3Collection(mesh.Positions.Length);
        var normals = new HelixToolkit.Vector3Collection(mesh.Positions.Length);
        for (var i = 0; i < mesh.Positions.Length; i++)
        {
            if (i % 4096 == 0) token.ThrowIfCancellationRequested();
            var p = mesh.Positions[i]; positions.Add(new Vector3((float)p.X, (float)p.Y, (float)p.Z));
            if (mesh.Normals != null) { var n = mesh.Normals[i]; normals.Add(new Vector3((float)n.X, (float)n.Y, (float)n.Z)); }
        }
        var geometry = new D.MeshGeometry3D { Positions = positions, Indices = new HelixToolkit.IntCollection(mesh.Indices) };
        if (normals.Count > 0) geometry.Normals = normals;
        else geometry.Normals = new HelixToolkit.Vector3Collection(HelixToolkit.Geometry.MeshGeometryHelper.CalculateNormals(positions, geometry.Indices));
        return new(geometry, mesh.Color);
    }
}

/// <summary>Direct3D 11 buffers, frustum culling and deferred rendering. Camera controls remain TopSolid-style.</summary>
internal sealed class GpuPreviewRenderer : IDisposable
{
    private readonly D.DefaultEffectsManager effects = new();
    internal H.Viewport3DX View { get; }
    private readonly H.OrthographicCamera camera = new();
    private readonly H.PerspectiveCamera perspectiveCamera = new() { FieldOfView = 35 };
    private readonly List<H.Element3D> models = [];
    private readonly H.MeshGeometryModel3D backgroundModel;
    private readonly H.DiffuseMaterial backgroundMaterial;
    private D.MeshGeometry3D? backgroundGeometry;
    private Color backgroundTop;
    private Color backgroundBottom;
    private bool backgroundIsGradient;
    private H.LineGeometryModel3D? toolpath;
    private H.LineGeometryModel3D? edges;
    internal event Action? Failed;
    internal GpuPreviewRenderer()
    {
        var clear = TopSolidPreviewPalette.ViewportClear(TopSolidTheme.Current.IsDark);
        backgroundTop = clear;
        backgroundBottom = clear;
        View = new H.Viewport3DX { EffectsManager = effects, Camera = camera,
            Background = new SolidColorBrush(clear), BackgroundColor = clear,
            IsHitTestVisible = false, ShowViewCube = false, ShowCoordinateSystem = false,
            EnableMouseButtonHitTest = false, EnableAutoOctreeUpdate = false, EnableRenderFrustum = true,
            EnableDeferredRendering = true, EnableD2DRendering = false, EnableRenderOrder = true,
            OITRenderMode = D.OITRenderType.DepthPeeling,
            MSAA = D.MSAALevel.Four, FXAALevel = D.FXAALevel.Low };
        backgroundMaterial = new H.DiffuseMaterial
        {
            DiffuseColor = new Color4(1, 1, 1, 1),
            EnableUnLit = true,
            DiffuseMap = GradientTexture(backgroundTop, backgroundBottom),
            DiffuseMapSampler = D.Shaders.DefaultSamplers.LinearSamplerClampAni4
        };
        backgroundModel = new H.MeshGeometryModel3D
        {
            Material = backgroundMaterial,
            IsHitTestVisible = false,
            IsTransparent = false,
            RenderOrder = -10000,
            Visibility = Visibility.Collapsed,
            CullMode = SharpDX.Direct3D11.CullMode.None
        };
        View.Items.Add(backgroundModel);
        // Viewport3DX creates its Direct3D render host when the control is loaded. A
        // BackgroundColor assigned before that point is otherwise replaced by the
        // host's default clear color, which makes the GPU path look unrelated to
        // the WPF TopSolid viewport palette on first paint.
        View.Loaded += (_, _) =>
        {
            View.BackgroundColor = backgroundTop;
            if (backgroundIsGradient) UpdateBackgroundPlane();
            View.InvalidateRender();
        };
        View.Items.Add(new H.AmbientLight3D { Color = Color.FromRgb(115, 115, 115) });
        View.Items.Add(new H.DirectionalLight3D { Color = Color.FromRgb(210, 210, 210), Direction = new Vector3D(-1, 1, -2) });
        View.Items.Add(new H.DirectionalLight3D { Color = Color.FromRgb(95, 95, 95), Direction = new Vector3D(1, -1, .5) });
        View.RenderExceptionOccurred += (_, e) => { e.Handled = true;
            App.DiagnosticLog.WriteException("preview.device", e.Exception, "Direct3D preview render failed."); Failed?.Invoke(); };
    }
    internal void SetBackground(Brush background)
    {
        View.Background = background;
        backgroundIsGradient = background is LinearGradientBrush gradientBrush && gradientBrush.GradientStops.Count > 1 &&
            gradientBrush.GradientStops.Any(stop => stop.Color != gradientBrush.GradientStops[0].Color);
        (backgroundTop, backgroundBottom) = background switch
        {
            LinearGradientBrush gradient when gradient.GradientStops.Count > 0 =>
                (gradient.GradientStops[0].Color, gradient.GradientStops[^1].Color),
            SolidColorBrush solid => (solid.Color, solid.Color),
            _ => (TopSolidPreviewPalette.ViewportClear(TopSolidTheme.Current.IsDark), TopSolidPreviewPalette.ViewportClear(TopSolidTheme.Current.IsDark))
        };
        View.BackgroundColor = backgroundTop;
        backgroundModel.Visibility = backgroundIsGradient ? Visibility.Visible : Visibility.Collapsed;
        backgroundMaterial.DiffuseMap = GradientTexture(backgroundTop, backgroundBottom);
        if (backgroundIsGradient) UpdateBackgroundPlane();
        View.InvalidateRender();
    }
    internal void Show(PreviewScene scene)
    {
        Clear();
        foreach (var source in scene.GpuMeshes)
        {
            var color = source.Color;
            var model = new H.MeshGeometryModel3D { Geometry = source.Geometry, IsHitTestVisible = false,
                Material = Material(color), IsTransparent = color.A < 255,
                CullMode = SharpDX.Direct3D11.CullMode.None };
            models.Add(model); View.Items.Add(model);
        }
        edges = new H.LineGeometryModel3D { Geometry = scene.GpuEdges, Color = Colors.Black, Thickness = 1, IsHitTestVisible = false };
        models.Add(edges); View.Items.Add(edges);
    }
    internal void SetEdges(bool visible) { if (edges != null) edges.Visibility = visible ? Visibility.Visible : Visibility.Collapsed; }
    internal static H.PhongMaterial Material(Color color) => new()
    {
        DiffuseColor = new Color4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f),
        AmbientColor = new Color4(color.R / 255f, color.G / 255f, color.B / 255f, 1),
        SpecularColor = new Color4(.15f, .15f, .15f, 1), SpecularShininess = 36
    };
    internal async Task WaitForFrameAsync(CancellationToken token)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frames = 0;
        void Failure() => completion.TrySetException(new InvalidOperationException("Direct3D could not complete the preview frame."));
        void Rendered(object? sender, EventArgs args)
        {
            if (Interlocked.Increment(ref frames) >= 2) completion.TrySetResult();
            else View.Dispatcher.BeginInvoke(new Action(() => View.InvalidateRender()));
        }
        View.OnRendered += Rendered; Failed += Failure;
        try { View.InvalidateRender(); await completion.Task.WaitAsync(TimeSpan.FromSeconds(30), token); }
        finally { View.OnRendered -= Rendered; Failed -= Failure; }
    }
    internal JObject DeviceInfo()
    {
        var native = effects.Device ?? throw new InvalidOperationException("Direct3D device was not initialized.");
        using var device = native.QueryInterface<SharpDX.DXGI.Device>();
        using var adapter = device.Adapter;
        var description = adapter.Description;
        return new JObject { ["adapter"] = description.Description.TrimEnd('\0'), ["vendorId"] = description.VendorId,
            ["dedicatedVideoMemoryBytes"] = (long)description.DedicatedVideoMemory, ["featureLevel"] = native.FeatureLevel.ToString(),
            ["renderer"] = "Direct3D 11", ["cpuWorkers"] = Math.Max(1, Environment.ProcessorCount - 1) };
    }
    internal void CameraChanged(OrthographicCamera source) => CameraChanged(source, false, perspectiveCamera.FieldOfView);
    internal void CameraChanged(OrthographicCamera source, bool perspective, double fieldOfView)
    {
        if (perspective)
        {
            perspectiveCamera.Position = source.Position; perspectiveCamera.LookDirection = source.LookDirection; perspectiveCamera.UpDirection = source.UpDirection;
            perspectiveCamera.FieldOfView = Math.Clamp(fieldOfView, 10, 120); perspectiveCamera.NearPlaneDistance = source.NearPlaneDistance;
            perspectiveCamera.FarPlaneDistance = source.FarPlaneDistance; View.Camera = perspectiveCamera;
        }
        else
        {
            camera.Position = source.Position; camera.LookDirection = source.LookDirection; camera.UpDirection = source.UpDirection;
            camera.Width = source.Width; camera.NearPlaneDistance = source.NearPlaneDistance; camera.FarPlaneDistance = source.FarPlaneDistance; View.Camera = camera;
        }
        if (backgroundIsGradient) UpdateBackgroundPlane();
    }
    internal void ShowToolpath(D.LineGeometry3D? lines)
    {
        if (toolpath != null) { View.Items.Remove(toolpath); toolpath.Dispose(); toolpath = null; }
        if (lines == null) return;
        toolpath = new H.LineGeometryModel3D { Geometry = lines, Color = lines.Colors is { Count: > 0 } ? Colors.White : TopSolidPreviewPalette.Toolpath(ToolpathColorRole.Unknown), Thickness = 1.6, IsHitTestVisible = false };
        View.Items.Add(toolpath);
    }
    private static Color4 ToColor4(Color color) => new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
    private void UpdateBackgroundPlane()
    {
        var perspective = View.Camera is H.PerspectiveCamera;
        var position = perspective ? perspectiveCamera.Position : camera.Position;
        var look = perspective ? perspectiveCamera.LookDirection : camera.LookDirection;
        var farPlane = perspective ? perspectiveCamera.FarPlaneDistance : camera.FarPlaneDistance;
        if (look.LengthSquared < 1e-8 || (!perspective && camera.Width <= 0) || farPlane <= 0) return;
        look.Normalize();
        var up = perspective ? perspectiveCamera.UpDirection : camera.UpDirection;
        if (up.LengthSquared < 1e-8) up = new Vector3D(0, 0, 1);
        up.Normalize();
        var right = Vector3D.CrossProduct(look, up);
        if (right.LengthSquared < 1e-8) right = new Vector3D(1, 0, 0);
        right.Normalize();
        up = Vector3D.CrossProduct(right, look);
        up.Normalize();

        // Keep a camera-facing quad behind the scene. This preserves the TopSolid
        // gradient on the Direct3D path, whose swap-chain clear is necessarily flat.
        var depth = Math.Min(farPlane * .9, Math.Max(look.Length * 1.1, farPlane * .75));
        // Size the quad from the active camera frustum instead of the far plane;
        // otherwise an orthographic viewport samples only the middle of the texture.
        var aspect = View.ActualWidth > 0 && View.ActualHeight > 0 ? View.ActualWidth / View.ActualHeight : 1.5;
        var halfWidth = perspective
            ? Math.Max(depth * Math.Tan(perspectiveCamera.FieldOfView * Math.PI / 360) * aspect * 1.05, 1e-4)
            : Math.Max(camera.Width * .5 * 1.05, 1e-4);
        var halfHeight = perspective
            ? Math.Max(depth * Math.Tan(perspectiveCamera.FieldOfView * Math.PI / 360) * 1.05, 1e-4)
            : Math.Max(camera.Width / Math.Max(aspect, 1e-4) * .5 * 1.05, 1e-4);
        var center = position + look * depth;
        var topLeft = center - right * halfWidth + up * halfHeight;
        var topRight = center + right * halfWidth + up * halfHeight;
        var bottomRight = center + right * halfWidth - up * halfHeight;
        var bottomLeft = center - right * halfWidth - up * halfHeight;

        if (backgroundGeometry == null)
        {
            backgroundGeometry = new D.MeshGeometry3D
            {
                Positions = new HelixToolkit.Vector3Collection
                {
                    ToVector3(topLeft), ToVector3(topRight), ToVector3(bottomRight), ToVector3(bottomLeft)
                },
                Indices = new HelixToolkit.IntCollection { 0, 1, 2, 0, 2, 3 },
                TextureCoordinates = new HelixToolkit.Vector2Collection
                {
                    new(0, 0), new(1, 0), new(1, 1), new(0, 1)
                }
            };
            backgroundModel.Geometry = backgroundGeometry;
        }
        else
        {
            if (backgroundGeometry.Positions is not { Count: >= 4 } positions) return;
            positions[0] = ToVector3(topLeft); positions[1] = ToVector3(topRight);
            positions[2] = ToVector3(bottomRight); positions[3] = ToVector3(bottomLeft);
            backgroundGeometry.UpdateVertices();
            backgroundGeometry.UpdateBounds();
        }
    }
    private static D.TextureModel GradientTexture(Color top, Color bottom)
    {
        const int width = 2, height = 128;
        var colors = new Color4[width * height];
        for (var y = 0; y < height; y++)
        {
            var t = (float)y / (height - 1);
            var color = Color.FromRgb(
                (byte)Math.Round(top.R + (bottom.R - top.R) * t),
                (byte)Math.Round(top.G + (bottom.G - top.G) * t),
                (byte)Math.Round(top.B + (bottom.B - top.B) * t));
            colors[y * width] = colors[y * width + 1] = ToColor4(color);
        }
        return new D.TextureModel(colors, width, height);
    }
    private static Vector3 ToVector3(Point3D point) => new((float)point.X, (float)point.Y, (float)point.Z);
    internal void Clear()
    { ShowToolpath(null); foreach (var model in models) { View.Items.Remove(model); model.Dispose(); } models.Clear(); }
    public void Dispose()
    {
        Clear();
        View.Items.Remove(backgroundModel); backgroundModel.Dispose();
        View.Dispose(); effects.Dispose();
    }
}
