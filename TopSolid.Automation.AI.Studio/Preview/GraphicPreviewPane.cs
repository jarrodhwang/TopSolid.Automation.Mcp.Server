using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;

namespace TopSolid.Automation.AI.Studio.Preview;

/// <summary>Native WPF/Direct3D viewport. Frozen surfaces, bounded screen-width edges, no render timer or CAD hit testing.</summary>
internal sealed partial class GraphicPreviewPane : Border, IDisposable
{
    private readonly IGraphicPreviewClient? client;
    private JObject? target;
    private readonly JObject? proposal;
    private readonly Grid canvas = new();
    private readonly Border loadingCover = new() { Visibility = Visibility.Visible };
    private readonly Viewport3D viewport = new() { IsHitTestVisible = false, ClipToBounds = false };
    private readonly ModelVisual3D solid = new(), edges = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontSize = 14, Margin = new Thickness(30) };
    private readonly TextBlock caption = new() { TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 13, Margin = new Thickness(0, 5, 0, 0) };
    private readonly TextBlock hint = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar progress = new() { Height = 3, Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed };
    private readonly ComboBox mode = new() { MinWidth = 130, FontSize = 12, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly ViewportCompass compass = new() { IsHitTestVisible = false };
    private readonly Button refresh;
    private readonly Border input = new() { Background = Brushes.Transparent, Focusable = true, Cursor = Cursors.Arrow };
    private CancellationTokenSource? loading;
    private Point3D center;
    private double yaw = -Math.PI / 3, pitch = Math.PI / 6, extent = 100;
    private bool disposed, showEdges = true, edgeUpdateQueued;
    private PreviewDrag drag;
    private MouseButton dragButton;
    private Point lastPointer;
    private PreviewScene? edgeScene;
    private Vector3D edgeDirection;
    private double edgeScale;
    private long generation;
    private GpuPreviewRenderer? gpu;
    private bool gpuAttempted;
    private string? cachedDocument;
    private string cachedCaption = "", cachedHint = "";
    private ToolpathPreviewScene? toolpathScene;
    private readonly ModelVisual3D toolpathVisual = new();
    private readonly EventHandler themeChanged;
    private readonly TextBlock pathStatus = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White,
        Background = new SolidColorBrush(Color.FromArgb(180, 24, 34, 48)), Padding = new Thickness(7), Margin = new Thickness(8),
        VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed };
    internal OrthographicCamera Camera { get; } = new() { UpDirection = new Vector3D(0, 0, 1), Width = 100 };
    private readonly PerspectiveCamera perspectiveCamera = new() { UpDirection = new Vector3D(0, 0, 1), FieldOfView = 35 };
    private bool perspectiveMode;
    private double perspectiveDistance = 400;
    internal PreviewScene? Scene { get; private set; }
    internal bool IsPerspective => perspectiveMode;
    internal string StatusText => status.Text;
    internal bool IsLoading => progress.IsIndeterminate;
    internal bool IsScenePresented => Scene != null && loadingCover.Visibility == Visibility.Collapsed;
    internal int ToolpathSegments => toolpathScene?.Segments ?? 0;
    internal string ToolpathStatus => pathStatus.Text;

    internal GraphicPreviewPane(IGraphicPreviewClient? client, JObject? target, JObject? proposal = null)
    {
        this.client = client; this.target = (JObject?)target?.DeepClone();
        this.proposal = proposal != null && ProposalGeometry.Supports(proposal) ? (JObject)proposal.DeepClone() : null;
        themeChanged = (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(UpdateGpuBackground));
        TopSolidTheme.Changed += themeChanged;
        CornerRadius = new CornerRadius(3); BorderThickness = new Thickness(1); MinHeight = 270;
        SetResourceReference(BackgroundProperty, "SurfaceBrush"); SetResourceReference(BorderBrushProperty, "BorderBrush");
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new DockPanel();
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(actions, Dock.Right);
        actions.Children.Add(mode); actions.Children.Add(Command("document", "Preview.OpenFile", async () => await OpenLocalPreviewAsync()));
        refresh = Command("refresh", "Preview.Refresh", async () => await ReloadAsync()); actions.Children.Add(refresh); header.Children.Add(actions);
        var titles = new StackPanel(); var title = new TextBlock { Text = StudioStrings.Get("Preview.Title"), FontSize = 14, FontWeight = FontWeights.SemiBold };
        title.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush"); caption.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
        titles.Children.Add(title); titles.Children.Add(caption); header.Children.Add(titles);
        var toolbar = DialogLayout.Toolbar(header); toolbar.Padding = new Thickness(10, 7, 10, 7); root.Children.Add(toolbar);
        Grid.SetRow(canvas, 1); canvas.ClipToBounds = true; canvas.MinHeight = 150;
        canvas.SetResourceReference(Panel.BackgroundProperty, "ViewportGradientBrush");
        if (TryFindResource("ViewportGradientBrush") == null) canvas.Background = new LinearGradientBrush(Color.FromRgb(74, 101, 151), Color.FromRgb(231, 228, 228), 90);
        viewport.Camera = Camera; viewport.Children.Add(solid); viewport.Children.Add(edges);
        viewport.Children.Add(toolpathVisual);
        RenderOptions.SetEdgeMode(viewport, EdgeMode.Unspecified); // Retain Direct3D multisample antialiasing.
        var lights = new Model3DGroup(); lights.Children.Add(new AmbientLight(Color.FromRgb(115, 115, 115)));
        lights.Children.Add(new DirectionalLight(Color.FromRgb(210, 210, 210), new Vector3D(-1, 1, -2)));
        lights.Children.Add(new DirectionalLight(Color.FromRgb(95, 95, 95), new Vector3D(1, -1, .5))); lights.Freeze();
        viewport.Children.Add(new ModelVisual3D { Content = lights }); canvas.Children.Add(viewport);
        input.ToolTip = StudioStrings.Get("Preview.Controls");
        AutomationProperties.SetName(input, StudioStrings.Get("Preview.Title")); AutomationProperties.SetHelpText(input, StudioStrings.Get("Preview.Controls"));
        input.MouseDown += (_, e) =>
        {
            if (Scene == null) return;
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2) { Fit(); e.Handled = true; return; }
            if (!BeginDrag(e.ChangedButton, Keyboard.Modifiers, e.GetPosition(input))) return;
            input.Focus();
            if (!input.CaptureMouse()) CancelDrag();
            e.Handled = true;
        };
        input.MouseMove += (_, e) =>
        {
            MoveDrag(e.GetPosition(input));
        };
        input.MouseUp += (_, e) => { if (EndDrag(e.ChangedButton)) e.Handled = true; };
        input.LostMouseCapture += (_, _) => CancelDrag();
        input.MouseWheel += (_, e) => { Zoom(Math.Exp(-e.Delta / 1000d)); e.Handled = true; };
        input.KeyDown += (_, e) =>
        {
            if (Scene == null) return;
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                var shortcutView = e.Key switch { Key.T => "top", Key.F => "front", Key.L => "left", Key.R => "right", _ => null };
                if (shortcutView != null) { SetView(shortcutView); e.Handled = true; return; }
            }
            switch (e.Key)
            {
                case Key.Left: Orbit(-.12, 0); break; case Key.Right: Orbit(.12, 0); break;
                case Key.Up: Orbit(0, -.12); break; case Key.Down: Orbit(0, .12); break;
                case Key.Add: case Key.OemPlus: Zoom(.85); break; case Key.Subtract: case Key.OemMinus: Zoom(1.15); break;
                case Key.F: Fit(); break; case Key.Home: SetView("iso"); break; default: return;
            }
            e.Handled = true;
        };
        canvas.Children.Add(input); canvas.Children.Add(compass);
        var tools = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8) };
        tools.Children.Add(CameraCommand());
        tools.Children.Add(Command("view-fit", "Preview.Fit", Fit));
        tools.Children.Add(Command("view-edges", "Preview.Edges", () => { showEdges = !showEdges; gpu?.SetEdges(showEdges); if (showEdges) QueueEdges(); else edges.Content = null; }));
        canvas.Children.Add(tools);
        status.Foreground = Brushes.White; status.VerticalAlignment = VerticalAlignment.Center; status.IsHitTestVisible = false;
        loadingCover.SetResourceReference(BackgroundProperty, "ViewportGradientBrush");
        loadingCover.Child = status; canvas.Children.Add(loadingCover);
        progress.VerticalAlignment = VerticalAlignment.Bottom; canvas.Children.Add(progress); root.Children.Add(canvas);
        canvas.Children.Add(pathStatus);
        var footer = new DockPanel();
        hint.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush"); footer.Children.Add(hint);
        var footerBar = DialogLayout.Footer(footer); footerBar.Padding = new Thickness(10, 7, 10, 7); Grid.SetRow(footerBar, 2); root.Children.Add(footerBar); Child = root;
        if (this.proposal != null) mode.Items.Add(StudioStrings.Get("Preview.Proposed"));
        mode.Items.Add(StudioStrings.Get("Preview.Current")); mode.SelectedIndex = 0;
        mode.Visibility = this.proposal == null ? Visibility.Collapsed : Visibility.Visible;
        mode.SelectionChanged += async (_, _) => { if (IsLoaded) await ReloadAsync(); };
        Loaded += async (_, _) =>
        {
            if (disposed) return;
            if (!gpuAttempted && RenderOptions.ProcessRenderMode != System.Windows.Interop.RenderMode.SoftwareOnly)
            {
                gpuAttempted = true;
                try
                {
                    gpu = new GpuPreviewRenderer(); canvas.Children.Insert(0, gpu.View);
                    UpdateGpuBackground();
                    App.DiagnosticLog.Write("info", "preview.adapter", "Direct3D 11 preview device created.", gpu.DeviceInfo());
                    gpu.Failed += () => Dispatcher.BeginInvoke(new Action(UseWpfFallback));
                    viewport.Visibility = Visibility.Collapsed;
                }
                catch (Exception error) when (error is SharpDX.SharpDXException or InvalidOperationException or System.Runtime.InteropServices.COMException)
                { UseWpfFallback(); }
            }
            App.DiagnosticLog.Write("info", "preview.rendering", "WPF Direct3D preference; capability does not identify the active adapter.",
                new JObject { ["renderPreference"] = RenderOptions.ProcessRenderMode.ToString(), ["hardwareCapabilityTier"] = RenderCapability.Tier >> 16 });
            await ReloadAsync();
        };
        canvas.SizeChanged += (_, _) => { if (Scene != null) Fit(); else UpdateCamera(); };
        SetMessage("Preview.Choose");
    }

    internal bool BeginDrag(MouseButton button, ModifierKeys modifiers, Point position)
    {
        if (Scene == null || drag != PreviewDrag.None) return false;
        var gesture = PreviewNavigation.Gesture(button, modifiers);
        if (gesture == PreviewDrag.None) return false;
        drag = gesture; dragButton = button; lastPointer = position;
        input.Cursor = drag == PreviewDrag.Pan ? Cursors.ScrollAll : Cursors.Hand; return true;
    }
    internal void MoveDrag(Point position)
    {
        if (drag == PreviewDrag.None || Scene == null) return;
        var delta = position - lastPointer; lastPointer = position;
        if (drag == PreviewDrag.Pan) Pan(delta.X, delta.Y);
        else { var orbit = PreviewNavigation.OrbitDelta(delta); Orbit(orbit.Horizontal, orbit.Vertical); }
    }
    internal bool EndDrag(MouseButton button)
    { if (drag == PreviewDrag.None || button != dragButton) return false; CancelDrag(); return true; }
    internal void CancelDrag()
    { drag = PreviewDrag.None; input.Cursor = Cursors.Arrow; if (input.IsMouseCaptured) input.ReleaseMouseCapture(); }

    private Button Command(string icon, string key, Action action)
    {
        var button = new Button { Width = 32, Height = 32, Margin = new Thickness(2), Padding = new Thickness(3), ToolTip = StudioStrings.Get(key),
            Content = new Image { Source = TopSolidIcons.Get(icon), Width = 24, Height = 24 }, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        ControlChrome.SetCornerRadius(button, new CornerRadius(3)); AutomationProperties.SetName(button, StudioStrings.Get(key));
        button.Click += (_, _) => action(); return button;
    }

    private Button CameraCommand()
    {
        var button = new Button { Width = 32, Height = 32, Margin = new Thickness(2), Padding = new Thickness(2),
            ToolTip = StudioStrings.Get("Preview.CameraMenu"), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        var glyph = new Grid();
        glyph.Children.Add(new Image { Source = TopSolidIcons.Get("view-camera"), Width = 24, Height = 24 });
        glyph.Children.Add(new TextBlock { Text = "▾", FontSize = 9, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, -1), Foreground = Brushes.White, IsHitTestVisible = false });
        button.Content = glyph;
        ControlChrome.SetCornerRadius(button, new CornerRadius(3)); AutomationProperties.SetName(button, StudioStrings.Get("Preview.CameraMenu"));
        var menu = new ContextMenu { Placement = System.Windows.Controls.Primitives.PlacementMode.Left, MinWidth = 170 };
        menu.SetResourceReference(Control.BackgroundProperty, "SurfaceBrush");
        menu.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        foreach (var view in new[] { "top", "bottom", "front", "back", "left", "right", "iso", "perspective" })
        {
            var item = new MenuItem { Header = StudioStrings.Get("Preview.View." + view), Tag = view,
                Icon = new Image { Source = TopSolidIcons.Get("view-camera"), Width = 18, Height = 18 } };
            item.InputGestureText = view switch
            {
                "top" => "Ctrl+Shift+T",
                "front" => "Ctrl+Shift+F",
                "left" => "Ctrl+Shift+L",
                "right" => "Ctrl+Shift+R",
                _ => ""
            };
            item.Click += (_, _) => SetView((string)item.Tag);
            menu.Items.Add(item);
        }
        button.ContextMenu = menu;
        button.Click += (_, _) => { menu.PlacementTarget = button; menu.IsOpen = true; };
        return button;
    }

    internal void SetTarget(JObject? next)
    {
        if (JToken.DeepEquals(next, target)) return;
        target = (JObject?)next?.DeepClone();
        if (IsLoaded) _ = ReloadAsync(debounce: true);
    }

    internal async Task ReloadAsync(bool debounce = false)
    {
        if (disposed) return;
        var current = ++generation; loading?.Cancel(); loading?.Dispose(); loading = new CancellationTokenSource(); var token = loading.Token;
        // Selection changes only replace the overlay. Keep disk-backed tiles and the camera alive too.
        var reuse = debounce && IsScenePresented && cachedDocument != null && (string?)target?["documentId"] == cachedDocument;
        if (!reuse) ClearScene(); else { ClearToolpath(); SetBusy(false); }
        SetBusy(false); refresh.IsEnabled = true;
        var proposed = proposal != null && mode.SelectedIndex == 0;
        caption.Text = StudioStrings.Get(proposed ? "Preview.Proposed" : "Preview.DocumentContext");
        hint.Text = StudioStrings.Get(proposed ? "Preview.ProposedHint" : "Preview.ContextHint") + "\n" + StudioStrings.Get("Preview.MouseHint");
        if (!proposed && target == null) { SetMessage("Preview.Choose"); return; }
        if (!proposed && client == null) { SetMessage("Preview.unavailable"); return; }
        if (!reuse) SetMessage("Preview.Loading"); SetBusy(true);
        var requested = (JObject?)target?.DeepClone();
        try
        {
            if (debounce) await Task.Delay(180, token);
            PreviewScene scene;
            if (proposed) scene = await Task.Run(() => ProposalGeometry.Build(proposal!, token), token);
            else
            {
                if (reuse)
                {
                    scene = Scene!; caption.Text = cachedCaption; hint.Text = cachedHint;
                }
                else
                {
                var documentTarget = (JObject)requested!.DeepClone(); documentTarget.Remove("operation");
                var payload = await ReadFilePreviewAsync(documentTarget, current, token);
                var result = payload.Metadata;
                if (disposed || current != generation) return;
                if ((string?)result["status"] != "ready") { SetMessage("Preview." + ((string?)result["status"] ?? "unavailable")); return; }
                var stl = (string?)result["format"] == "stl";
                if (stl ? (string?)result["upAxis"] != "Z" || (string?)result["units"] != "mm" :
                    (string?)result["format"] != "glb" || (string?)result["upAxis"] != "Y" || (string?)result["units"] != "m") throw new InvalidDataException("Unsupported preview format.");
                if (requested?["documentId"] != null && !JToken.DeepEquals(requested["documentId"], result["documentId"])) throw new InvalidDataException("Preview document mismatch.");
                scene = payload.Scene ?? throw new InvalidDataException("Missing preview geometry.");
                if (disposed || current != generation) return;
                caption.Text = StudioStrings.Get("Preview.DocumentContext") + " · " + Friendly((string?)result["name"]);
                var precise = stl && (double?)result["linearToleranceMm"] == PreviewQuality.LinearToleranceMm &&
                    (double?)result["angularToleranceDegrees"] == PreviewQuality.AngularToleranceDegrees;
                hint.Text = StudioStrings.Get("Preview.ContextHint") + " · " + StudioStrings.Get(precise ? "Preview.Precision" : "Preview.NativePrecision") +
                    "\n" + StudioStrings.Get("Preview.MouseHint");
                if (stl && !precise) hint.Text = StudioStrings.Get("Preview.Adaptive", (double?)result["linearToleranceMm"] ?? 0, (double?)result["angularToleranceDegrees"] ?? 0) + "\n" + StudioStrings.Get("Preview.MouseHint");
                if (stl) hint.Text = StudioStrings.Get("Preview.NeutralColors") + "\n" + hint.Text;
                if (scene.EdgesOmitted) hint.Text += " · " + StudioStrings.Get("Preview.LargeShaded");
                cachedDocument = (string?)result["documentId"]; cachedCaption = caption.Text; cachedHint = hint.Text;
                }
            }
            if (disposed || current != generation) return;
            if (!reuse) { ShowScene(scene); await PresentCompleteSceneAsync(current, token); }
            else { status.Visibility = Visibility.Collapsed; compass.Visibility = Visibility.Visible; }
            if (!proposed && requested?["operation"] is JObject operation) await LoadToolpath(operation, current, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or TimeoutException or InvalidOperationException or ArgumentException or JsonException or OverflowException or FormatException)
        {
            App.DiagnosticLog.WriteException("preview.load", error, "Preview loading failed.");
            if (!disposed && current == generation) SetMessage("Preview.unavailable");
        }
        finally { if (!disposed && current == generation) { SetBusy(false); refresh.IsEnabled = true; } }
    }

    internal void ShowScene(PreviewScene scene)
    {
        Scene = scene; solid.Content = scene.Surfaces; edgeScene = null;
        if (gpu != null) { gpu.Show(scene); gpu.SetEdges(showEdges); solid.Content = null; AttachStreaming(); }
        if (!IsLoading) { status.Visibility = Visibility.Collapsed; loadingCover.Visibility = Visibility.Collapsed; compass.Visibility = Visibility.Visible; }
        AutomationProperties.SetHelpText(this, StudioStrings.Get("Preview.Controls")); Fit();
    }
    private async Task PresentCompleteSceneAsync(long current, CancellationToken token)
    {
        if (pagedPreview != null) await pagedPreview.Ready.WaitAsync(token);
        if (gpu is { } renderer)
        {
            try { await renderer.WaitForFrameAsync(token); }
            catch (InvalidOperationException)
            {
                if (ReferenceEquals(gpu, renderer)) UseWpfFallback();
                if (Scene == null) throw;
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle, token);
            }
        }
        else await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle, token);
        token.ThrowIfCancellationRequested();
        if (disposed || generation != current) return;
        loadingCover.Visibility = Visibility.Collapsed; status.Visibility = Visibility.Collapsed; compass.Visibility = Visibility.Visible;
    }
    private void SetMessage(string key)
    {
        var value = StudioStrings.Get(key); if (value == key) value = StudioStrings.Get("Preview.unavailable");
        status.Text = value; status.Visibility = Visibility.Visible; loadingCover.Visibility = Visibility.Visible; compass.Visibility = Visibility.Collapsed;
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
    }
    private void SetBusy(bool value)
    { progress.IsIndeterminate = value; progress.Visibility = value ? Visibility.Visible : Visibility.Collapsed; refresh.IsEnabled = !value; }
    private void ClearScene() { CancelDrag(); ClearToolpath(); ClearStreaming(); cachedDocument = null; Scene = edgeScene = null; solid.Content = edges.Content = null; gpu?.Clear(); }
    internal void Fit()
    {
        if (Scene == null) return;
        var b = Scene.Bounds; center = new Point3D(b.X + b.SizeX / 2, b.Y + b.SizeY / 2, b.Z + b.SizeZ / 2);
        extent = Math.Max(1e-4, new Vector3D(b.SizeX, b.SizeY, b.SizeZ).Length);
        perspectiveDistance = Math.Max(extent * .05, extent / (2 * Math.Tan(perspectiveCamera.FieldOfView * Math.PI / 360)) * 1.15);
        Camera.Width = extent * 1.22 * Math.Max(1, canvas.ActualWidth / Math.Max(1, canvas.ActualHeight)); UpdateCamera();
    }
    internal void Orbit(double horizontal, double vertical)
    { yaw += horizontal; pitch = Math.Clamp(pitch + vertical, -Math.PI / 2 + .001, Math.PI / 2 - .001); UpdateCamera(); }
    internal void Zoom(double factor)
    {
        if (Scene == null) return;
        Camera.Width = Math.Clamp(Camera.Width * factor, extent * .005, extent * 100);
        if (perspectiveMode) perspectiveDistance = Math.Clamp(perspectiveDistance * factor, extent * .05, extent * 100);
        UpdateCamera();
    }
    private void Pan(double x, double y)
    {
        var direction = Direction(); var right = Vector3D.CrossProduct(new Vector3D(0, 0, 1), direction); right.Normalize(); var up = Vector3D.CrossProduct(direction, right);
        var perPixel = perspectiveMode
            ? 2 * perspectiveDistance * Math.Tan(perspectiveCamera.FieldOfView * Math.PI / 360) / Math.Max(1, canvas.ActualHeight)
            : Camera.Width / Math.Max(1, canvas.ActualWidth);
        center += -x * perPixel * right + y * perPixel * up; UpdateCamera();
    }
    internal void SetView(string view)
    {
        if (view == "perspective")
        {
            perspectiveMode = true;
            perspectiveDistance = Math.Clamp(perspectiveDistance, Math.Max(extent * .05, 1e-4), Math.Max(extent * 100, 1e-4));
            UpdateCamera();
            return;
        }
        perspectiveMode = false;
        (yaw, pitch) = view switch
        {
            "top" => (0, Math.PI / 2 - .001),
            "bottom" => (0, -Math.PI / 2 + .001),
            "front" => (-Math.PI / 2, 0),
            "back" => (Math.PI / 2, 0),
            "left" => (Math.PI, 0),
            "right" => (0, 0),
            _ => (-Math.PI / 3, Math.PI / 6)
        };
        UpdateCamera();
    }
    private Vector3D Direction() => new(Math.Cos(yaw) * Math.Cos(pitch), Math.Sin(yaw) * Math.Cos(pitch), Math.Sin(pitch));
    private void UpdateCamera()
    {
        if (disposed) return;
        var direction = Direction(); var distance = perspectiveMode ? perspectiveDistance : extent * 4; var position = center + direction * distance;
        Camera.Position = position; Camera.LookDirection = -direction;
        Camera.NearPlaneDistance = Math.Max(1e-8, extent / 10000); Camera.FarPlaneDistance = extent * 10;
        perspectiveCamera.Position = position; perspectiveCamera.LookDirection = -direction; perspectiveCamera.NearPlaneDistance = Camera.NearPlaneDistance;
        perspectiveCamera.FarPlaneDistance = Camera.FarPlaneDistance; viewport.Camera = perspectiveMode ? perspectiveCamera : Camera;
        compass.Update(direction, Camera.Width);
        gpu?.CameraChanged(Camera, perspectiveMode, perspectiveCamera.FieldOfView);
        pagedPreview?.CameraChanged(Camera, Math.Max(1, canvas.ActualWidth) / Math.Max(1, canvas.ActualHeight));
        if (toolpathScene != null && gpu == null) toolpathVisual.Content = toolpathScene.Ribbons(direction, Camera.Width / Math.Max(1, canvas.ActualWidth));
        QueueEdges();
    }
    private void QueueEdges()
    {
        if (gpu != null || edgeUpdateQueued || !showEdges || Scene == null || disposed) return;
        edgeUpdateQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            edgeUpdateQueued = false;
            if (disposed || !showEdges || Scene == null) return;
            var direction = Direction(); var scale = Camera.Width / Math.Max(1, canvas.ActualWidth);
            if (edgeScene == Scene && edgeDirection == direction && edgeScale == scale && edges.Content != null) return;
            edges.Content = Scene.CreateEdges(direction, scale); edgeScene = Scene; edgeDirection = direction; edgeScale = scale;
        }));
    }
    private static string Friendly(string? name) => string.IsNullOrWhiteSpace(name) || name.All(char.IsDigit) ? StudioStrings.Get("Preview.DocumentContext") : name;
    private void UseWpfFallback()
    {
        if (streamingSource != null) { ClearScene(); SetMessage("Preview.GpuRequired"); }
        if (gpu != null) { canvas.Children.Remove(gpu.View); gpu.Dispose(); gpu = null; }
        viewport.Visibility = Visibility.Visible; viewport.Camera = perspectiveMode ? perspectiveCamera : Camera;
        if (Scene != null) { solid.Content = Scene.Surfaces; edgeScene = null; QueueEdges(); }
        App.DiagnosticLog.Write("warning", "preview.renderer", "Using the WPF renderer after Direct3D 11 was unavailable.");
    }
    private void UpdateGpuBackground()
    {
        if (disposed || gpu == null) return;
        if (TryFindResource("ViewportGradientBrush") is Brush background) gpu.SetBackground(background);
    }
    private void ClearToolpath()
    { toolpathScene = null; gpu?.ShowToolpath(null); toolpathVisual.Content = null; pathStatus.Visibility = Visibility.Collapsed; }
    private async Task LoadToolpath(JObject operation, long current, CancellationToken token)
    {
        pathStatus.Text = StudioStrings.Get("Preview.PathLoading"); pathStatus.Visibility = Visibility.Visible;
        try
        {
            var result = client is IToolpathPreviewClient paths ? await paths.GetToolpathPreviewAsync(operation, token) : new JObject { ["status"] = "unavailable" };
            if (disposed || current != generation) return;
            if (!JToken.DeepEquals(result["operation"], operation) || (string?)result["status"] != "ready")
            { pathStatus.Text = StudioStrings.Get((string?)result["status"] == "coordinatesUnavailable" ? "Preview.PathCoordinatesUnavailable" : "Preview.PathUnavailable"); return; }
            var path = await Task.Run(() => ToolpathPreviewScene.Read(result, token), token);
            if (disposed || current != generation) return;
            toolpathScene = path; gpu?.ShowToolpath(path.Geometry); UpdateCamera();
            var compatibilityLimit = gpu == null && path.Segments > ToolpathPreviewScene.MaximumWpfSegments;
            pathStatus.Text = StudioStrings.Get(path.Partial || compatibilityLimit ? "Preview.PathPartial" : "Preview.PathReady",
                compatibilityLimit ? ToolpathPreviewScene.MaximumWpfSegments : path.Segments);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException or JsonException or FormatException)
        { if (!disposed && current == generation) pathStatus.Text = StudioStrings.Get("Preview.PathUnavailable"); }
    }
    public void Dispose() { if (disposed) return; disposed = true; TopSolidTheme.Changed -= themeChanged; generation++; loading?.Cancel(); loading?.Dispose(); SetBusy(false); ClearScene(); gpu?.Dispose(); gpu = null; }
}
