using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio.Preview;

internal sealed partial class GraphicPreviewPane
{
    private readonly ToggleButton remainingStockToggle = new(), originalStockToggle = new();
    private readonly Button machineSettings = new();
    private readonly ToggleButton partToggle = new();
    private bool showPart = true;
    private CamStockMode stockMode = CamStockMode.Remaining;
    private bool updatingStockButtons;
    internal CamStockMode StockMode => stockMode;
    internal bool HasOriginalStock => camScenes?.Original != null;

    private void AddMachineSettings(Panel tools)
    {
        machineSettings.Content = "▾"; machineSettings.Width = 20; machineSettings.Height = 32;
        machineSettings.Padding = new Thickness(0); machineSettings.Margin = new Thickness(-2, 2, 2, 2);
        machineSettings.Visibility = Visibility.Collapsed;
        machineSettings.ToolTip = StudioStrings.Get("Preview.MachineElements");
        AutomationProperties.SetName(machineSettings, StudioStrings.Get("Preview.MachineElements"));
        machineSettings.Click += (_, _) =>
        {
            if (camScenes?.MachineElements is not { } elements) return;
            var dialog = new MachineElementsWindow(elements) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true || camScenes?.MachineElements != elements) return;
            elements.Apply(dialog.Hidden);
            if (!showMachine) SetMachineVisible(true); else PresentCamLayers(fit: false);
        };
        tools.Children.Add(machineSettings);
    }

    private void AddStockControls(Panel tools)
    {
        partToggle.Width = partToggle.Height = 32; partToggle.Margin = new Thickness(2); partToggle.Padding = new Thickness(3);
        partToggle.Content = new Image { Source = TopSolidIcons.Get("part"), Width = 24, Height = 24 };
        partToggle.ToolTip = StudioStrings.Get("Preview.Part"); AutomationProperties.SetName(partToggle, StudioStrings.Get("Preview.Part"));
        partToggle.IsChecked = showPart; partToggle.Visibility = Visibility.Collapsed;
        partToggle.Checked += (_, _) => { showPart = true; PresentCamLayers(false); };
        partToggle.Unchecked += (_, _) => { showPart = false; PresentCamLayers(false); };
        tools.Children.Add(partToggle);
        Add(remainingStockToggle, "part", "Preview.RemainingStock", CamStockMode.Remaining);
        Add(originalStockToggle, "shape", "Preview.OriginalStock", CamStockMode.Original);
        void Add(ToggleButton button, string icon, string key, CamStockMode value)
        {
            button.Width = button.Height = 32; button.Margin = new Thickness(2); button.Padding = new Thickness(3);
            button.Content = new Image { Source = TopSolidIcons.Get(icon), Width = 24, Height = 24 };
            button.Visibility = Visibility.Collapsed; button.ToolTip = StudioStrings.Get(key);
            AutomationProperties.SetName(button, StudioStrings.Get(key));
            button.Checked += (_, _) => { if (!updatingStockButtons) SetStockMode(value); };
            button.Unchecked += (_, _) => { if (!updatingStockButtons && stockMode == value) SetStockMode(CamStockMode.Part); };
            tools.Children.Add(button);
        }
    }
    private void UpdateStockControls()
    {
        partToggle.Visibility = camScenes == null ? Visibility.Collapsed : Visibility.Visible;
        machineSettings.Visibility = camScenes == null ? Visibility.Collapsed : Visibility.Visible;
        machineSettings.IsEnabled = camScenes?.MachineElements?.Roots.Count > 0;
        updatingStockButtons = true;
        try
        {
            remainingStockToggle.Visibility = originalStockToggle.Visibility = camScenes == null ? Visibility.Collapsed : Visibility.Visible;
            remainingStockToggle.IsEnabled = camScenes?.HasRemaining == true;
            originalStockToggle.IsEnabled = camScenes?.Original != null;
            remainingStockToggle.IsChecked = stockMode == CamStockMode.Remaining && remainingStockToggle.IsEnabled;
            originalStockToggle.IsChecked = stockMode == CamStockMode.Original && originalStockToggle.IsEnabled;
            originalStockToggle.ToolTip = StudioStrings.Get(originalStockToggle.IsEnabled ? "Preview.OriginalStock" : "Preview.OriginalStockUnavailable");
        }
        finally { updatingStockButtons = false; }
    }
    internal void SetStockMode(CamStockMode value)
    {
        if (camScenes == null || value == CamStockMode.Original && camScenes.Original == null || value == CamStockMode.Remaining && !camScenes.HasRemaining) return;
        stockMode = value; UpdateStockControls(); PresentCamLayers(fit: false);
    }
    private void PresentCamLayers(bool fit)
    {
        if (camScenes == null) return;
        ShowScene(camScenes.Select(showMachine, stockMode, showPart), fit);
        if (toolpathScene != null) gpu?.ShowToolpath(toolpathScene.Geometry);
        UpdateCamera();
    }
}
