using System.Windows.Input;
using System.Windows;

namespace TopSolid.Automation.AI.Studio.Preview;

internal enum PreviewDrag { None, Orbit, Pan }

internal static class PreviewNavigation
{
    internal static PreviewDrag Gesture(MouseButton button, ModifierKeys modifiers) => button switch
    {
        MouseButton.Middle => PreviewDrag.Orbit,
        MouseButton.Right when (modifiers & ModifierKeys.Control) != 0 => PreviewDrag.Orbit,
        MouseButton.Right => PreviewDrag.Pan,
        _ => PreviewDrag.None
    };

    // TopSolid turns the viewed part with the pointer. Horizontal camera motion
    // is inverse to the screen drag, while vertical motion follows the screen
    // drag's Y direction (WPF Y grows down). Keep the axes explicit so a future
    // renderer cannot silently reintroduce the old vertical inversion.
    internal static (double Horizontal, double Vertical) OrbitDelta(Vector delta) => (-delta.X * .008, delta.Y * .008);
}
