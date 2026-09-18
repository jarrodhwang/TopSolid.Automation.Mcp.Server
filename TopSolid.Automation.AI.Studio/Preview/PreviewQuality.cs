using System.IO;
using System.Windows.Media;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Preview;

internal static class PreviewQuality
{
    internal const double LinearToleranceMm = GraphicPreviewQuality.LinearToleranceMm;
    internal const double AngularToleranceDegrees = GraphicPreviewQuality.AngularToleranceDegrees;
    internal const double EdgeWidth = 1; // Device-independent screen pixel, including after zoom.
    internal static readonly Color DefaultColor = Color.FromRgb(192, 192, 192);

    internal static int CircleSegments(double radius)
    {
        // Sagitta <= 0.05 mm and adjacent normals <= 5 degrees; preserve cardinal extrema.
        var chordAngle = 2 * Math.Acos(Math.Clamp(1 - LinearToleranceMm / radius, -1, 1));
        var step = Math.Min(AngularToleranceDegrees * Math.PI / 180, chordAngle);
        var required = Math.Ceiling(2 * Math.PI / step / 4) * 4;
        if (!double.IsFinite(required) || required > PreviewScene.MaximumTriangles / 4)
            throw new InvalidDataException("The requested preview tolerance exceeds the geometry budget.");
        return Math.Max(4, (int)required);
    }
}
