using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

    public static readonly DependencyProperty SuppressTextSelectionProperty = DependencyProperty.RegisterAttached(
        "SuppressTextSelection", typeof(bool), typeof(ControlChrome), new FrameworkPropertyMetadata(false, OnSuppressTextSelectionChanged));
    public static bool GetSuppressTextSelection(DependencyObject target) => (bool)target.GetValue(SuppressTextSelectionProperty);
    public static void SetSuppressTextSelection(DependencyObject target, bool value) => target.SetValue(SuppressTextSelectionProperty, value);

    private static readonly HashSet<TextBox> ProtectedTextBoxes = [];

    private static void OnSuppressTextSelectionChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ComboBox comboBox) return;
        if ((bool)args.NewValue)
        {
            comboBox.Loaded += ModelComboBox_Loaded;
            comboBox.Unloaded += ModelComboBox_Unloaded;
            if (comboBox.IsLoaded) ProtectEditableTextBox(comboBox);
        }
        else
        {
            comboBox.Loaded -= ModelComboBox_Loaded;
            comboBox.Unloaded -= ModelComboBox_Unloaded;
            RemoveEditableTextBox(comboBox);
        }
    }

    private static void ModelComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox comboBox) ProtectEditableTextBox(comboBox);
    }

    private static void ModelComboBox_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox comboBox) RemoveEditableTextBox(comboBox);
    }

    private static void ProtectEditableTextBox(ComboBox comboBox)
    {
        comboBox.ApplyTemplate();
        if (comboBox.Template.FindName("PART_EditableTextBox", comboBox) is not TextBox textBox || !ProtectedTextBoxes.Add(textBox)) return;
        textBox.PreviewMouseMove += ProtectedTextBox_MouseMove;
        textBox.PreviewKeyDown += ProtectedTextBox_KeyDown;
        textBox.SelectionChanged += ProtectedTextBox_SelectionChanged;
        DataObject.AddCopyingHandler(textBox, ProtectedTextBox_Copying);
        textBox.Unloaded += ProtectedTextBox_Unloaded;
    }

    private static void RemoveEditableTextBox(ComboBox comboBox)
    {
        if (comboBox.Template.FindName("PART_EditableTextBox", comboBox) is TextBox textBox)
            RemoveEditableTextBox(textBox);
    }

    private static void ProtectedTextBox_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox) RemoveEditableTextBox(textBox);
    }

    private static void RemoveEditableTextBox(TextBox textBox)
    {
        if (!ProtectedTextBoxes.Remove(textBox)) return;
        textBox.PreviewMouseMove -= ProtectedTextBox_MouseMove;
        textBox.PreviewKeyDown -= ProtectedTextBox_KeyDown;
        textBox.SelectionChanged -= ProtectedTextBox_SelectionChanged;
        DataObject.RemoveCopyingHandler(textBox, ProtectedTextBox_Copying);
        textBox.Unloaded -= ProtectedTextBox_Unloaded;
    }

    private static void ProtectedTextBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) e.Handled = true;
    }

    private static void ProtectedTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key is Key.A or Key.C or Key.X)
            e.Handled = true;
    }

    private static void ProtectedTextBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.SelectionLength > 0)
            textBox.Select(textBox.SelectionStart, 0);
    }

    private static void ProtectedTextBox_Copying(object sender, DataObjectCopyingEventArgs e)
    {
        e.CancelCommand();
    }
}
