using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;

namespace TopSolid.Automation.AI.Studio.Preview;

/// <summary>Native WPF/Direct3D viewport. Frozen geometry, camera-only interaction, no render timer or CAD hit testing.</summary>
internal sealed class GraphicPreviewPane : Border, IDisposable
{
    private readonly IGraphicPreviewClient? client;
    private JObject? target;
    private readonly JObject? proposal;
    private readonly Grid canvas = new();
    private readonly Viewport3D viewport = new() { IsHitTestVisible = false, ClipToBounds = false };
    private readonly ModelVisual3D solid = new(), edges = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontSize = 14, Margin = new Thickness(30) };
    private readonly TextBlock caption = new() { TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 13, Margin = new Thickness(0, 5, 0, 0) };
    private readonly TextBlock hint = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar progress = new() { Height = 3, Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed };
    private readonly ComboBox mode = new() { MinWidth = 130, FontSize = 12, Margin = new Thickness(10, 0, 0, 0) };
    private readonly ViewportCompass compass = new() { IsHitTestVisible = false };
    private readonly Button refresh;
    private CancellationTokenSource? loading;
    private Point3D center;
    private double yaw = -Math.PI / 3, pitch = Math.PI / 6, extent = 100;
    private bool disposed, panMode, showEdges = true, dragging;
    private Point lastPointer;
    private long generation;
    internal OrthographicCamera Camera { get; } = new() { UpDirection = new Vector3D(0, 0, 1), Width = 100 };
    internal PreviewScene? Scene { get; private set; }
    internal string StatusText => status.Text;
    internal bool IsLoading => progress.IsIndeterminate;

    internal GraphicPreviewPane(IGraphicPreviewClient? client, JObject? target, JObject? proposal = null)
    {
        this.client = client; this.target = (JObject?)target?.DeepClone();
        this.proposal = proposal != null && ProposalGeometry.Supports(proposal) ? (JObject)proposal.DeepClone() : null;
        CornerRadius = new CornerRadius(12); BorderThickness = new Thickness(1); Padding = new Thickness(1); MinHeight = 270;
        SetResourceReference(BackgroundProperty, "SurfaceBrush"); SetResourceReference(BorderBrushProperty, "BorderBrush");
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new DockPanel { Margin = new Thickness(14, 12, 14, 10) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(actions, Dock.Right);
        actions.Children.Add(mode); refresh = Command("refresh", "Preview.Refresh", async () => await ReloadAsync()); actions.Children.Add(refresh); header.Children.Add(actions);
        var titles = new StackPanel(); var title = new TextBlock { Text = StudioStrings.Get("Preview.Title"), FontSize = 16, FontWeight = FontWeights.SemiBold };
        title.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush"); caption.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
        titles.Children.Add(title); titles.Children.Add(caption); header.Children.Add(titles); root.Children.Add(header);
        Grid.SetRow(canvas, 1); canvas.ClipToBounds = true; canvas.MinHeight = 150;
        canvas.SetResourceReference(Panel.BackgroundProperty, "ViewportGradientBrush");
        if (TryFindResource("ViewportGradientBrush") == null) canvas.Background = new LinearGradientBrush(Color.FromRgb(74, 101, 151), Color.FromRgb(231, 228, 228), 90);
        viewport.Camera = Camera; viewport.Children.Add(solid); viewport.Children.Add(edges);
        var lights = new Model3DGroup(); lights.Children.Add(new AmbientLight(Color.FromRgb(115, 115, 115)));
        lights.Children.Add(new DirectionalLight(Color.FromRgb(210, 210, 210), new Vector3D(-1, 1, -2)));
        lights.Children.Add(new DirectionalLight(Color.FromRgb(95, 95, 95), new Vector3D(1, -1, .5))); lights.Freeze();
        viewport.Children.Add(new ModelVisual3D { Content = lights }); canvas.Children.Add(viewport);
        var input = new Border { Background = Brushes.Transparent, Focusable = true, Cursor = Cursors.Hand };
        AutomationProperties.SetName(input, StudioStrings.Get("Preview.Title")); AutomationProperties.SetHelpText(input, StudioStrings.Get("Preview.Controls"));
        input.MouseDown += (_, e) =>
        {
            if (Scene == null) return;
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2) { Fit(); e.Handled = true; return; }
            if (e.ChangedButton is not (MouseButton.Left or MouseButton.Middle)) return;
            input.Focus(); lastPointer = e.GetPosition(input); dragging = input.CaptureMouse(); e.Handled = true;
        };
        input.MouseMove += (_, e) =>
        {
            if (!dragging || Scene == null) return;
            var current = e.GetPosition(input); var delta = current - lastPointer; lastPointer = current;
            if (panMode || e.MiddleButton == MouseButtonState.Pressed || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) Pan(delta.X, delta.Y);
            else Orbit(delta.X * .008, delta.Y * .008);
        };
        input.MouseUp += (_, _) => { dragging = false; input.ReleaseMouseCapture(); };
        input.LostMouseCapture += (_, _) => dragging = false;
        input.MouseWheel += (_, e) => { Zoom(Math.Exp(-e.Delta / 1000d)); e.Handled = true; };
        input.KeyDown += (_, e) =>
        {
            if (Scene == null) return;
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
        tools.Children.Add(Command("view-orbit", "Preview.Orbit", () => { panMode = false; input.Cursor = Cursors.Hand; input.Focus(); }));
        tools.Children.Add(Command("view-pan", "Preview.Pan", () => { panMode = true; input.Cursor = Cursors.ScrollAll; input.Focus(); }));
        tools.Children.Add(Command("view-fit", "Preview.Fit", Fit));
        tools.Children.Add(Command("view-edges", "Preview.Edges", () => { showEdges = !showEdges; edges.Content = showEdges ? Scene?.Edges : null; }));
        canvas.Children.Add(tools);
        status.Foreground = Brushes.White; status.VerticalAlignment = VerticalAlignment.Center; status.IsHitTestVisible = false; canvas.Children.Add(status);
        progress.VerticalAlignment = VerticalAlignment.Bottom; canvas.Children.Add(progress); root.Children.Add(canvas);
        var footer = new DockPanel { Margin = new Thickness(12, 9, 12, 10) }; Grid.SetRow(footer, 2);
        var views = new ComboBox { MinWidth = 85, Margin = new Thickness(0, 0, 12, 0), FontSize = 11 };
        foreach (var view in new[] { "iso", "top", "front", "right" }) views.Items.Add(new ComboBoxItem { Content = StudioStrings.Get("Preview.View." + view), Tag = view });
        views.SelectedIndex = 0; views.SelectionChanged += (_, _) => SetView((string)((ComboBoxItem)views.SelectedItem).Tag);
        footer.Children.Add(views); hint.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush"); footer.Children.Add(hint); root.Children.Add(footer); Child = root;
        if (this.proposal != null) mode.Items.Add(StudioStrings.Get("Preview.Proposed"));
        mode.Items.Add(StudioStrings.Get("Preview.Current")); mode.SelectedIndex = 0;
        mode.Visibility = this.proposal == null ? Visibility.Collapsed : Visibility.Visible;
        mode.SelectionChanged += async (_, _) => { if (IsLoaded) await ReloadAsync(); };
        Loaded += async (_, _) => { if (!disposed) await ReloadAsync(); };
        canvas.SizeChanged += (_, _) => { if (Scene != null) Fit(); else UpdateCamera(); };
        SetMessage("Preview.Choose");
    }

    private Button Command(string icon, string key, Action action)
    {
        var button = new Button { Width = 32, Height = 32, Margin = new Thickness(2), Padding = new Thickness(3), ToolTip = StudioStrings.Get(key),
            Content = new Image { Source = TopSolidIcons.Get(icon), Width = 24, Height = 24 }, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        ControlChrome.SetCornerRadius(button, new CornerRadius(6)); AutomationProperties.SetName(button, StudioStrings.Get(key));
        button.Click += (_, _) => action(); return button;
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
        ClearScene(); SetBusy(false); refresh.IsEnabled = true;
        var proposed = proposal != null && mode.SelectedIndex == 0;
        caption.Text = StudioStrings.Get(proposed ? "Preview.Proposed" : "Preview.DocumentContext");
        hint.Text = StudioStrings.Get(proposed ? "Preview.ProposedHint" : "Preview.ContextHint");
        if (!proposed && target == null) { SetMessage("Preview.Choose"); return; }
        if (!proposed && client == null) { SetMessage("Preview.unavailable"); return; }
        SetMessage("Preview.Loading"); SetBusy(true);
        var requested = (JObject?)target?.DeepClone();
        try
        {
            if (debounce) await Task.Delay(180, token);
            PreviewScene scene;
            if (proposed) scene = await Task.Run(() => ProposalGeometry.Build(proposal!, token), token);
            else
            {
                var result = await client!.GetGraphicPreviewAsync(requested!, token);
                if (disposed || current != generation) return;
                if ((string?)result["status"] != "ready") { SetMessage("Preview." + ((string?)result["status"] ?? "unavailable")); return; }
                if ((string?)result["format"] != "glb" || (string?)result["upAxis"] != "Y" || (string?)result["units"] != "m") throw new InvalidDataException("Unsupported preview format.");
                if (requested?["documentId"] != null && !JToken.DeepEquals(requested["documentId"], result["documentId"])) throw new InvalidDataException("Preview document mismatch.");
                var data = (string?)result["data"];
                if (data == null || data.Length > (GlbPreviewReader.MaximumBytes + 2L) / 3 * 4) throw new InvalidDataException("Oversized preview.");
                scene = await Task.Run(() => GlbPreviewReader.Read(Convert.FromBase64String(data), token), token);
                if (disposed || current != generation) return;
                caption.Text = StudioStrings.Get("Preview.DocumentContext") + " · " + Friendly((string?)result["name"]);
            }
            if (disposed || current != generation) return;
            ShowScene(scene);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is IOException or TimeoutException or InvalidOperationException or ArgumentException or JsonException or OverflowException or FormatException)
        { if (!disposed && current == generation) SetMessage("Preview.unavailable"); }
        finally { if (!disposed && current == generation) { SetBusy(false); refresh.IsEnabled = true; } }
    }

    internal void ShowScene(PreviewScene scene)
    {
        Scene = scene; solid.Content = scene.Surfaces; edges.Content = showEdges ? scene.Edges : null;
        status.Visibility = Visibility.Collapsed; compass.Visibility = Visibility.Visible;
        AutomationProperties.SetHelpText(this, StudioStrings.Get("Preview.Controls")); Fit();
    }
    private void SetMessage(string key)
    {
        var value = StudioStrings.Get(key); if (value == key) value = StudioStrings.Get("Preview.unavailable");
        status.Text = value; status.Visibility = Visibility.Visible; compass.Visibility = Visibility.Collapsed;
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
    }
    private void SetBusy(bool value)
    { progress.IsIndeterminate = value; progress.Visibility = value ? Visibility.Visible : Visibility.Collapsed; refresh.IsEnabled = !value; }
    private void ClearScene() { Scene = null; solid.Content = edges.Content = null; }
    internal void Fit()
    {
        if (Scene == null) return;
        var b = Scene.Bounds; center = new Point3D(b.X + b.SizeX / 2, b.Y + b.SizeY / 2, b.Z + b.SizeZ / 2);
        extent = Math.Max(1e-4, new Vector3D(b.SizeX, b.SizeY, b.SizeZ).Length);
        Camera.Width = extent * 1.22 * Math.Max(1, canvas.ActualWidth / Math.Max(1, canvas.ActualHeight)); UpdateCamera();
    }
    internal void Orbit(double horizontal, double vertical)
    { yaw += horizontal; pitch = Math.Clamp(pitch + vertical, -Math.PI / 2 + .001, Math.PI / 2 - .001); UpdateCamera(); }
    internal void Zoom(double factor)
    { if (Scene == null) return; Camera.Width = Math.Clamp(Camera.Width * factor, extent * .005, extent * 100); UpdateCamera(); }
    private void Pan(double x, double y)
    {
        var direction = Direction(); var right = Vector3D.CrossProduct(new Vector3D(0, 0, 1), direction); right.Normalize(); var up = Vector3D.CrossProduct(direction, right);
        var perPixel = Camera.Width / Math.Max(1, canvas.ActualWidth); center += -x * perPixel * right + y * perPixel * up; UpdateCamera();
    }
    internal void SetView(string view)
    {
        (yaw, pitch) = view switch { "top" => (-Math.PI / 2, Math.PI / 2 - .001), "front" => (-Math.PI / 2, 0), "right" => (0, 0), _ => (-Math.PI / 3, Math.PI / 6) }; UpdateCamera();
    }
    private Vector3D Direction() => new(Math.Cos(yaw) * Math.Cos(pitch), Math.Sin(yaw) * Math.Cos(pitch), Math.Sin(pitch));
    private void UpdateCamera()
    {
        if (disposed) return;
        var direction = Direction(); Camera.Position = center + direction * extent * 4; Camera.LookDirection = -direction;
        Camera.NearPlaneDistance = Math.Max(1e-8, extent / 10000); Camera.FarPlaneDistance = extent * 10;
        compass.Update(direction, Camera.Width);
    }
    private static string Friendly(string? name) => string.IsNullOrWhiteSpace(name) || name.All(char.IsDigit) ? StudioStrings.Get("Preview.DocumentContext") : name;
    public void Dispose() { if (disposed) return; disposed = true; generation++; loading?.Cancel(); loading?.Dispose(); SetBusy(false); ClearScene(); }
}
