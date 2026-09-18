using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace TopSolid.Automation.AI.Studio.Preview;

/// <summary>Small 2D orientation/scale overlay; redrawn only when the camera or viewport changes.</summary>
internal sealed class ViewportCompass : FrameworkElement
{
    private Vector3D right, up;
    private double width;
    internal void Update(Vector3D direction, double cameraWidth)
    {
        right = Vector3D.CrossProduct(new Vector3D(0, 0, 1), direction); right.Normalize(); up = Vector3D.CrossProduct(direction, right);
        width = cameraWidth; InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth < 180 || ActualHeight < 100 || width <= 0) return;
        var origin = new Point(48, ActualHeight - 45);
        foreach (var axis in new[] { (new Vector3D(1, 0, 0), Brushes.Red, "X"), (new Vector3D(0, 1, 0), Brushes.LimeGreen, "Y"), (new Vector3D(0, 0, 1), Brushes.Blue, "Z") })
        {
            var end = origin + new Vector(Vector3D.DotProduct(axis.Item1, right) * 29, -Vector3D.DotProduct(axis.Item1, up) * 29);
            dc.DrawLine(new Pen(axis.Item2, 2), origin, end); dc.DrawEllipse(axis.Item2, null, end, 2.5, 2.5);
            Label(dc, axis.Item3, end + new Vector(3, -12), Brushes.Black);
        }
        dc.DrawEllipse(Brushes.DarkOrange, new Pen(Brushes.DimGray, .5), origin, 3, 3);
        var desired = width * Math.Min(95, ActualWidth / 4) / ActualWidth; var magnitude = Math.Pow(10, Math.Floor(Math.Log10(desired)));
        var number = desired / magnitude; var mm = (number >= 5 ? 5 : number >= 2 ? 2 : 1) * magnitude;
        var pixels = mm / width * ActualWidth; var x = ActualWidth - 18 - pixels; var y = ActualHeight - 20;
        dc.DrawRectangle(Brushes.DarkOrange, new Pen(Brushes.Black, .8), new Rect(x, y, pixels, 6));
        Label(dc, mm >= 1000 ? (mm / 1000).ToString("G3", CultureInfo.CurrentCulture) + " m" : mm.ToString("G3", CultureInfo.CurrentCulture) + " mm", new Point(x, y - 17), Brushes.Black);
    }
    private void Label(DrawingContext dc, string text, Point at, Brush brush) => dc.DrawText(new FormattedText(text, CultureInfo.CurrentUICulture,
        FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip), at);
}
