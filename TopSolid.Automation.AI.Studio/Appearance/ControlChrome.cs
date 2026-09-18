using System.Windows;

namespace TopSolid.Automation.AI.Studio.Appearance;

/// <summary>Shared shape and selection state for native tool buttons and the chat composer.</summary>
public static class ControlChrome
{
    public static readonly DependencyProperty LightCaptionControlsProperty = DependencyProperty.RegisterAttached(
        "LightCaptionControls", typeof(bool), typeof(ControlChrome), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    public static bool GetLightCaptionControls(DependencyObject target) => (bool)target.GetValue(LightCaptionControlsProperty);
    public static void SetLightCaptionControls(DependencyObject target, bool value) => target.SetValue(LightCaptionControlsProperty, value);

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
        "CornerRadius", typeof(CornerRadius), typeof(ControlChrome), new FrameworkPropertyMetadata(new CornerRadius(3)));
    public static CornerRadius GetCornerRadius(DependencyObject target) => (CornerRadius)target.GetValue(CornerRadiusProperty);
    public static void SetCornerRadius(DependencyObject target, CornerRadius value) => target.SetValue(CornerRadiusProperty, value);

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.RegisterAttached(
        "IsSelected", typeof(bool), typeof(ControlChrome), new FrameworkPropertyMetadata(false));
    public static bool GetIsSelected(DependencyObject target) => (bool)target.GetValue(IsSelectedProperty);
    public static void SetIsSelected(DependencyObject target, bool value) => target.SetValue(IsSelectedProperty, value);
}
