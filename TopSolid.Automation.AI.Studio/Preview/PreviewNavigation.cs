using System.Windows.Input;

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
}
