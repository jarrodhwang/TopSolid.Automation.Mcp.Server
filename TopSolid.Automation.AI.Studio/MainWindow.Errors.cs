using System.Windows;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio;

public partial class MainWindow
{
    private string latestError = "";
    private string latestErrorDetails = "";

    private void SetErrorReview(Exception error)
    {
        // Never retain an unredacted exception in a UI field, including in developer mode.
        latestError = diagnosticLog.Redact(StudioStrings.Text(error.Message));
        latestErrorDetails = diagnosticLog.Redact(error.ToString());
        ErrorIcon.Source = SettingsErrorIcon.Source = TopSolidIcons.Get("error");
        ErrorButton.Visibility = Visibility.Visible;
    }

    private void Error_Click(object sender, RoutedEventArgs e)
    {
        if (latestError.Length == 0) return;
        var window = new ErrorWindow(responsePresenter.Present(latestError, settings.DevMode), settings.DevMode ? latestErrorDetails : null) { Owner = this };
        window.ShowDialog();
    }

    private void ClearErrorReview()
    {
        latestError = latestErrorDetails = "";
        ErrorButton.Visibility = Visibility.Collapsed;
    }
}
