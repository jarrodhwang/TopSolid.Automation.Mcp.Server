using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.IO;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio;

public partial class MainWindow
{
    private MenuItem filesMenu = null!, cadMenu = null!, camMenu = null!;
    private void InitializeContextModes()
    {
        filesMenu = new MenuItem { Icon = ModeIcon("attachment") };
        filesMenu.Click += Attach_Click;
        cadMenu = new MenuItem { Header = "CAD", IsCheckable = true, Icon = ModeIcon("sketch") };
        camMenu = new MenuItem { Header = "CAM", IsCheckable = true, Icon = ModeIcon("operation") };
        cadMenu.SetResourceReference(ForegroundProperty, "CadModeBrush");
        camMenu.SetResourceReference(ForegroundProperty, "CamModeBrush");
        cadMenu.Click += CadMode_Click; camMenu.Click += CamMode_Click;
        AttachButton.ContextMenu = new ContextMenu { PlacementTarget = AttachButton, Placement = PlacementMode.Top,
            Items = { filesMenu, cadMenu, camMenu } };
        CadModeIcon.Source = TopSolidIcons.Get("sketch"); CamModeIcon.Source = TopSolidIcons.Get("operation");
        RefreshContextModes();
    }
    private static Image ModeIcon(string key) => new() { Source = TopSolidIcons.Get(key), Width = 16, Height = 16 };
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (operation == null && !loadingAttachments && !closing) AttachButton.ContextMenu.IsOpen = true;
    }
    private void CadMode_Click(object sender, RoutedEventArgs e) => ChangeContextMode(cad: true);
    private void CamMode_Click(object sender, RoutedEventArgs e) => ChangeContextMode(cad: false);
    private void ChangeContextMode(bool cad)
    {
        if (operation != null || loadingAttachments || closing) { RefreshContextModes(); return; }
        var next = settings.ContextOptions.Snapshot();
        if (cad) next.Cad = !next.Cad; else next.Cam = !next.Cam;
        try
        {
            settingsStore.SaveContextOptions(next);
            settings.ContextOptions = next;
            if (session != null) session.ContextOptions = next.Snapshot();
            RecordTrace("Context", $"CAD={next.Cad}; CAM={next.Cam}");
        }
        catch (Exception error) { ShowError(error); }
        RefreshContextModes(); MessageBox.Focus();
    }
    private void RefreshContextModes()
    {
        filesMenu.Header = StudioStrings.Get("Context.Files");
        cadMenu.IsChecked = settings.ContextOptions.Cad; camMenu.IsChecked = settings.ContextOptions.Cam;
        CadModeButton.Visibility = cadMenu.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        CamModeButton.Visibility = camMenu.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        CamPaletteName.Text = settings.CamColorStandard.Name + (settings.CamColorStandard.Id==CamColorStandard.BuiltInId?"":" · v" + settings.CamColorStandard.Version);
    }
    private void EditCamPalette_Click(object sender, RoutedEventArgs e)
    {
        if (operation != null) return;
        var dialog = new CamColorPaletteWindow(settings.CamColorStandard) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;
        try
        {
            settingsStore.SaveCamColorStandard(dialog.Result);
            settings.CamColorStandard = dialog.Result;
            if (session != null) session.ColorStandard = dialog.Result.Snapshot();
            RefreshContextModes();
        }
        catch (Exception error) { ShowError(error); }
    }
    private Task<CamColorPlan?> ReviewCamColors(CamColorPlan plan, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var dialog = new CamColorReviewWindow(plan, mcp) { Owner = this };
        using var cancel = token.Register(() => Dispatcher.BeginInvoke(() => dialog.Close()));
        var accepted = dialog.ShowDialog() == true;
        token.ThrowIfCancellationRequested();
        return Task.FromResult(accepted ? dialog.Result : null);
    }
    private void EditCamMethods_Click(object sender, RoutedEventArgs e)
    {
        if (operation != null) return;
        var dialog = new CamMethodCatalogWindow(settings.CamMethods, mcp, ConfirmChange) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;
        try { settingsStore.SaveCamMethods(dialog.Result); settings.CamMethods = dialog.Result; if (session != null) session.CamMethods = dialog.Result.Select(m => m.Snapshot()).ToArray(); }
        catch (Exception error) { ShowError(error); }
    }
    private void ConfigureCamAutomation()
    {
        if (session == null) return;
        session.CamMethods = settings.CamMethods.Select(m => m.Snapshot()).ToArray();
        var store = new CamAutomationStore(Path.GetDirectoryName(settingsStore.FilePath)!);
        try { session.PreparedCamPlan = store.Load(); }
        catch (Exception error) { RecordTrace("CAM plan", error.Message); }
        session.SaveCamPlan = store.Save;
        session.ReviewCamAutomationAsync = ReviewCamAutomation;
        session.ConfirmCamMethodsAsync = ConfirmCamMethods;
    }
    private Task<CamAutomationPlan?> ReviewCamAutomation(CamAutomationPlan plan, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var dialog = new CamAutomationReviewWindow(plan, settings.CamMethods, mcp) { Owner = this };
        using var cancel = token.Register(() => Dispatcher.BeginInvoke(() => dialog.Close()));
        var accepted = dialog.ShowDialog() == true; token.ThrowIfCancellationRequested();
        return Task.FromResult(accepted ? dialog.Result : null);
    }
    private Task<CamAutomationPlan?> ConfirmCamMethods(CamAutomationPlan plan, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var dialog = new CamMethodExecutionWindow(plan) { Owner = this };
        using var cancel = token.Register(() => Dispatcher.BeginInvoke(() => dialog.Close()));
        activityAwaitingApproval = true;
        try { var accepted = dialog.ShowDialog() == true; token.ThrowIfCancellationRequested(); return Task.FromResult(accepted ? dialog.Result : null); }
        finally { activityAwaitingApproval = false; }
    }
}
