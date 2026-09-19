using System.Windows.Media;

namespace TopSolid.Automation.AI.Studio.Preview;

/// <summary>Exact RGB values from the user's TopSolidColor1, 2 and 4 captures.</summary>
internal static class TopSolidPreviewPalette
{
    internal static readonly Color Surface = Color.FromRgb(192, 192, 192);
    internal static readonly Color Edge = Colors.Black;

    // HelixToolkit's Direct3D swap chain needs an opaque clear color. The WPF
    // fallback still receives the complete ViewportGradientBrush; these colors
    // are the native TopSolid upper viewport color, so the GPU path never falls
    // back to its default black clear surface.
    internal static Color ViewportClear(bool dark) => dark ? Color.FromRgb(61, 61, 61) : Color.FromRgb(74, 101, 151);

    internal static Color Toolpath(ToolpathColorRole role) => role switch
    {
        ToolpathColorRole.Feed => Color.FromRgb(255, 255, 0),
        ToolpathColorRole.ReducedFeed => Color.FromRgb(255, 255, 128),
        ToolpathColorRole.LeadIn => Color.FromRgb(251, 126, 20),
        ToolpathColorRole.LeadOut => Color.FromRgb(4, 129, 235),
        ToolpathColorRole.Stock => Color.FromRgb(255, 255, 128),
        ToolpathColorRole.Finish => Color.FromRgb(128, 128, 255),
        ToolpathColorRole.LinkIn => Color.FromRgb(145, 165, 221),
        ToolpathColorRole.LinkOut => Color.FromRgb(110, 90, 34),
        ToolpathColorRole.Point => Color.FromRgb(255, 0, 128),
        ToolpathColorRole.Rapid => Color.FromRgb(0, 255, 0),
        ToolpathColorRole.MaximumFeed => Color.FromRgb(0, 0, 255),
        // Unknown is deliberately neutral: a color must not invent a machining classification.
        _ => Surface
    };

    internal static Color Sketch(SketchColorRole role) => role switch
    {
        SketchColorRole.Overconstrained => Color.FromRgb(255, 0, 0),
        SketchColorRole.FullyConstrained => Color.FromRgb(0, 0, 255),
        SketchColorRole.Fixed => Color.FromRgb(80, 80, 80),
        SketchColorRole.Unconstrainable => Color.FromRgb(187, 187, 0),
        SketchColorRole.Preconstrained => Colors.Black,
        SketchColorRole.Underconstrained => Color.FromRgb(255, 0, 255),
        _ => Surface
    };
}

internal enum ToolpathColorRole { Unknown, Feed, ReducedFeed, LeadIn, LeadOut, Stock, Finish, LinkIn, LinkOut, Point, Rapid, MaximumFeed }
internal enum SketchColorRole { Unknown, Overconstrained, FullyConstrained, Fixed, Unconstrainable, Preconstrained, Underconstrained }
