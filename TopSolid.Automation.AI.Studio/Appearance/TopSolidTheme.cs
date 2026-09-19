using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace TopSolid.Automation.AI.Studio.Appearance;

public sealed record TopSolidThemeSnapshot(bool IsDark, string Name, string Source,
    IReadOnlyDictionary<string, string>? Colors = null)
{
    internal string Signature => $"{IsDark}|{Name}|{Source}|" +
        string.Join(";", (Colors ?? new Dictionary<string, string>()).OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value));
}

/// <summary>TopSolid palette resources shared by chat, settings, approvals and diagnostics.</summary>
public static class TopSolidTheme
{
    private static readonly ConditionalWeakTable<Window, object> TrackedWindows = new();
    public static TopSolidThemeSnapshot Current { get; private set; } = new(false, "TopSolid Classic", "Classic fallback");
    public static event EventHandler? Changed;

    public static void InitializeResources(Window window)
    {
        // App.xaml provides resources in production. Standalone windows in tests also need styles.
        if (Application.Current == null || Application.Current.TryFindResource("WindowBrush") == null)
            window.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/TopSolid.Automation.AI.Studio;component/Appearance/TopSolidStyles.xaml", UriKind.Relative)
            });
    }

    public static void Apply(TopSolidThemeSnapshot snapshot)
    {
        var previousSignature = Current.Signature;
        Current = snapshot;
        var app = Application.Current;
        if (app == null) return;
        app.Dispatcher.VerifyAccess();
        var dark = snapshot.IsDark;
        var colors = new Dictionary<string, string>
        {
            ["WindowBrush"] = dark ? "#292929" : "#F6F6FA",
            ["WindowFrameBorderBrush"] = dark ? "#FFFFFF" : "#233149",
            ["SurfaceBrush"] = dark ? "#292929" : "#FFFFFF",
            ["PanelBrush"] = dark ? "#333333" : "#E4E4EB",
            ["BorderBrush"] = dark ? "#373737" : "#C8C8C8",
            ["TextBrush"] = dark ? "#FAFAFA" : "#000000",
            ["MutedTextBrush"] = dark ? "#BDBDBD" : "#62626C",
            ["AccentBrush"] = dark ? "#0080D7" : "#0078D7",
            ["AccentTextBrush"] = "#FFFFFF",
            ["HoverBrush"] = dark ? "#006EBB" : "#DDEBFC",
            ["TitleTopBrush"] = dark ? "#0A0A0A" : "#464650",
            ["TitleBottomBrush"] = dark ? "#0A0A0A" : "#1E1E28",
            ["TitleTextBrush"] = "#FAFAFA",
            ["UserMessageBrush"] = dark ? "#373737" : "#EBF7FE",
            ["CodeBrush"] = dark ? "#202020" : "#F0F0F4",
            ["DangerBrush"] = dark ? "#FF9696" : "#A4262C",
            ["SuccessBrush"] = dark ? "#96FF96" : "#23783D"
        };
        colors["HoverBorderBrush"] = dark ? "#2B6EC9" : "#FDAF5F";
        colors["SelectedBorderBrush"] = dark ? "#2B6EC9" : "#7DA2CE";
        colors["SelectionTextBrush"] = dark ? "#FAFAFA" : "#1E1E28";
        // User themes use the same names as TopSolid's theme XML. Only relevant UI colors are imported.
        var names = new Dictionary<string, string>
        {
            ["global_dialog_back"] = "WindowBrush", ["global_controledit_background"] = "SurfaceBrush",
            ["global_dialog_border"] = "BorderBrush", ["global_dialog_text"] = "TextBrush",
            ["global_title_top"] = "TitleTopBrush", ["global_title_bottom"] = "TitleBottomBrush",
            ["global_title_text"] = "TitleTextBrush", ["global_treeview_activetop"] = "AccentBrush",
            ["global_controledit_watermarktext"] = "MutedTextBrush", ["global_activeitembackground_border"] = "HoverBorderBrush",
            ["tabcontrol_active_border"] = "SelectedBorderBrush"
        };
        if (snapshot.Colors != null)
            foreach (var pair in names)
                if (snapshot.Colors.TryGetValue(pair.Key, out var value)) colors[pair.Value] = value;

        foreach (var pair in colors)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pair.Value));
            brush.Freeze();
            app.Resources[pair.Key] = brush;
        }
        app.Resources["CaptionControlBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#0A0A0A" : "#E4E4EB"));
        app.Resources["CaptionGlyphBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#FFFFFF" : "#787878"));
        app.Resources["CaptionControlBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#FFFFFF" : "Transparent"));
        app.Resources["CaptionHoverBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#FFFFFF" : "#E39F58"));
        app.Resources["ToolbarBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#FFFFFF" : "#C8C8C8"));
        // Match the TopSolid title, toolbar and command highlights; the chat surfaces stay quiet.
        app.Resources["TitleGradientBrush"] = Gradient(colors["TitleTopBrush"], colors["TitleBottomBrush"]);
        app.Resources["CaptionHoverGradientBrush"] = Gradient("#FFD6B5", "#E39F58");
        app.Resources["CaptionPressedGradientBrush"] = Gradient("#FEC288", "#D2873E");
        string Palette(string name, string fallback) => snapshot.Colors?.GetValueOrDefault(name) ?? fallback;
        app.Resources["ToolbarGradientBrush"] = Gradient(Palette("iconsbar_toolstrip_background", dark ? "#0A0A0A" : "#FAFAFF"), Palette("iconsbar_background_right", dark ? "#161616" : "#E4E4EB"));
        app.Resources["RailGradientBrush"] = Gradient(Palette("iconsbar_background_left", dark ? "#0A0A0A" : "#FAFAFF"), Palette("iconsbar_background_right", dark ? "#111111" : "#E4E4EB"), horizontal: true);
        app.Resources["ButtonGradientBrush"] = Gradient(Palette("tabcontrol_up", dark ? "#3D3D3D" : "#FCFCFC"), Palette("tabcontrol_down", dark ? "#292929" : "#D9D9D9"));
        // TopSolid Dark uses blue command feedback. Classic retains the native peach/orange command highlight.
        app.Resources["ToolHoverGradientBrush"] = Gradient(dark ? "#0080D7" : "#FFFFFF", dark ? "#006EBB" : "#FFD9BB");
        app.Resources["ToolPressedGradientBrush"] = Gradient(dark ? "#006EBB" : "#FFD9BB", dark ? "#005A9E" : "#FDAF5F");
        app.Resources["SelectionGradientBrush"] = Gradient(Palette("tabcontrol_active_up", dark ? "#0080D7" : "#EBF7FE"), Palette("tabcontrol_active_down", dark ? "#006EBB" : "#9FD1ED"));
        // Installed 7.20 ThemeColors: Classic blue/grey gradient; Dark uses #3D3D3D
        // at both ends (the later native theme migration supersedes #353535).
        app.Resources["ViewportGradientBrush"] = Gradient(snapshot.Colors?.GetValueOrDefault("systemcolors_documentbackgroundtop") ?? (dark ? "#3D3D3D" : "#4A6597"),
            snapshot.Colors?.GetValueOrDefault("systemcolors_documentbackgroundbottom") ?? (dark ? "#3D3D3D" : "#E7E4E4"));
        // Native WPF popup/selection surfaces also need to use the application palette.
        app.Resources[SystemColors.WindowBrushKey] = app.Resources["SurfaceBrush"];
        app.Resources[SystemColors.WindowTextBrushKey] = app.Resources["TextBrush"];
        app.Resources[SystemColors.ControlBrushKey] = app.Resources["PanelBrush"];
        app.Resources[SystemColors.ControlTextBrushKey] = app.Resources["TextBrush"];
        app.Resources[SystemColors.HighlightBrushKey] = app.Resources["AccentBrush"];
        app.Resources[SystemColors.HighlightTextBrushKey] = app.Resources["AccentTextBrush"];
        app.Resources[SystemColors.GrayTextBrushKey] = app.Resources["MutedTextBrush"];
        app.Resources[SystemColors.MenuBrushKey] = app.Resources["SurfaceBrush"];
        app.Resources[SystemColors.MenuTextBrushKey] = app.Resources["TextBrush"];
        app.Resources[SystemColors.MenuBarBrushKey] = app.Resources["WindowBrush"];
        app.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = app.Resources["PanelBrush"];
        app.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = app.Resources["TextBrush"];
        foreach (Window window in app.Windows) ApplyWindow(window);
        if (!string.Equals(previousSignature, snapshot.Signature, StringComparison.Ordinal)) Changed?.Invoke(null, EventArgs.Empty);
    }

    private static LinearGradientBrush Gradient(string top, string bottom, bool horizontal = false)
    {
        var brush = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(top),
            (Color)ColorConverter.ConvertFromString(bottom), horizontal ? 0 : 90);
        brush.Freeze();
        return brush;
    }

    public static void ApplyWindow(Window window)
    {
        if (!TrackedWindows.TryGetValue(window, out _))
        {
            InitializeResources(window);
            window.SetResourceReference(FrameworkElement.StyleProperty, typeof(Window));
            window.WindowStyle = WindowStyle.None;
            WindowChrome.SetWindowChrome(window, new WindowChrome
            {
                CaptionHeight = 34, ResizeBorderThickness = window.ResizeMode == ResizeMode.NoResize ? new Thickness(0) : new Thickness(6),
                GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(3), UseAeroCaptionButtons = false
            });
            TrackedWindows.Add(window, new object());
            window.SourceInitialized += WindowSourceInitialized;
            if (window.IsInitialized) WorkAreaBounds.Attach(window);
        }
        SetTitleBar(window);
    }

    private static void WindowSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            SetTitleBar(window);
            WorkAreaBounds.Attach(window);
        }
    }

    private static void SetTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        try
        {
            var dark = Current.IsDark ? 1 : 0;
            _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            var caption = Current.Colors?.GetValueOrDefault("global_title_top") ?? (Current.IsDark ? "#0A0A0A" : "#464650");
            var captionText = Current.Colors?.GetValueOrDefault("global_title_text") ?? "#FAFAFA";
            var captionColor = ToColorRef(caption);
            var textColor = ToColorRef(captionText);
            var frame = Current.IsDark ? "#101827" : "#233149";
            var frameColor = ToColorRef(frame);
            _ = DwmSetWindowAttribute(handle, 34, ref frameColor, sizeof(int));
            _ = DwmSetWindowAttribute(handle, 35, ref captionColor, sizeof(int));
            _ = DwmSetWindowAttribute(handle, 36, ref textColor, sizeof(int));
        }
        catch (DllNotFoundException) { } // Older Windows retains a standard accessible title bar.
        catch (EntryPointNotFoundException) { }
    }

    private static int ToColorRef(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        return color.R | color.G << 8 | color.B << 16;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private static class WorkAreaBounds
    {
        private const int WmGetMinMaxInfo = 0x0024;
        private const uint MonitorDefaultToNearest = 0x00000002;

        public static void Attach(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero || HwndSource.FromHwnd(handle) is not { } source) return;
            source.AddHook(HandleMessage);
        }

        private static IntPtr HandleMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != WmGetMinMaxInfo || lParam == IntPtr.Zero) return IntPtr.Zero;
            var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero) return IntPtr.Zero;
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info)) return IntPtr.Zero;

            var bounds = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            bounds.MaxPosition = new NativePoint
            {
                X = info.WorkArea.Left - info.MonitorArea.Left,
                Y = info.WorkArea.Top - info.MonitorArea.Top
            };
            bounds.MaxSize = new NativePoint
            {
                X = info.WorkArea.Right - info.WorkArea.Left,
                Y = info.WorkArea.Bottom - info.WorkArea.Top
            };
            Marshal.StructureToPtr(bounds, lParam, false);
            handled = true;
            return IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MinMaxInfo
        {
            public NativePoint Reserved;
            public NativePoint MaxSize;
            public NativePoint MaxPosition;
            public NativePoint MinTrackSize;
            public NativePoint MaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect MonitorArea;
            public NativeRect WorkArea;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    }
}
