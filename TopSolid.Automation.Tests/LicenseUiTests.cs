using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TopSolid.Automation.AI.Studio;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class LicenseUiTests
{
    internal static async Task Run(MainWindow owner, Action<Window, string> render)
    {
        var beforeLanguage = StudioStrings.CurrentLanguage; var beforeTheme = TopSolidTheme.Current;
        var window = new LicenseWindow { Owner = owner, ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
        try
        {
            StudioStrings.Apply("en"); window.Show(); await Layout(window);
            Check.True(Descendants<ProgressBar>(window).Single().IsIndeterminate, "License query has no loading feedback");
            Check.Equal(1, Descendants<WindowTitleBar>(window).Count(), "License dialog does not use shared native chrome");
            render(window, "license-checking.png");
            var status = LicenseTests.Fixture();
            status.Licenses.Add(new() { Name = "TopSolid'Cam", Module = 2000, Version = "7.20", Type = "ClsUser", Active = true, Valid = true });
            foreach (var language in new[] { "en", "ko" })
            {
                StudioStrings.Apply(language);
                foreach (var dark in new[] { false, true })
                {
                    TopSolidTheme.Apply(new(dark, "License fixture", "UI test"));
                    window.ShowResult(status); await Layout(window);
                    Check.True(!Descendants<ProgressBar>(window).Single().IsIndeterminate, "Completed license query kept animating");
                    Check.Equal(status.Licenses[0], (Descendants<ListBox>(window).Single().SelectedItem as ListBoxItem)?.Tag, "Kernel Base is not selected first");
                    var texts = Descendants<TextBlock>(window).Select(t => t.Text).ToArray();
                    foreach (var key in new[] { "License.HostVersion", "License.Validity", "License.Expiration", "License.Active", "License.Type", "License.User", "License.Status", "License.Version" })
                        Check.True(texts.Contains(StudioStrings.Get(key)), "Missing license field: " + key);
                    Check.True(texts.Contains("Workshop user") && texts.Any(t => t.Contains("2027-12-31")), "User or expiry not displayed");
                    render(window, $"license-valid-{language}-{(dark ? "dark" : "light")}.png");
                }
            }
            Descendants<ListBox>(window).Single().SelectedIndex = 1;
            Check.True(Descendants<TextBlock>(window).Any(t => t.Text == StudioStrings.Get("License.NoExpiration")), "Missing expiration was falsely shown as perpetual");
            window.ShowResult(LicenseTests.Fixture(false), closeApplication: true); await Layout(window);
            Check.True(Descendants<Button>(window).Any(b => Equals(b.Content, StudioStrings.Get("License.Exit"))), "Blocked startup does not explain closing Studio");
            render(window, "license-invalid-ko.png");
            window.ShowResult(new TopSolidLicenseStatus(), closeApplication: true); await Layout(window);
            Check.True(Descendants<TextBlock>(window).Any(t => t.Text == StudioStrings.Get("License.Unavailable")), "Connection failure is presented as an expired license");
            render(window, "license-unavailable-ko.png");
            var package = LicenseTests.Fixture(); package.Licenses[0].Module = 0; package.Licenses[0].Name = "TopSolid'Design Pro"; package.Licenses[0].Valid = null;
            window.ShowResult(package); await Layout(window);
            Check.True(Descendants<TextBlock>(window).Any(t => t.Text == StudioStrings.Get("License.ModuleDetailsUnavailable")), "Packaged entitlement fabricated Kernel Base metadata");
            Check.True(!Descendants<TextBlock>(window).Any(t => t.Text == "Workshop user"), "A package's user was falsely assigned to Kernel Base");
            render(window, "license-packages-ko.png");
            window.ShowResult(new TopSolidLicenseStatus(), closeApplication: true); await Layout(window);
            window.Width = window.MinWidth; window.Height = window.MinHeight; await Layout(window);
            Check.True(Descendants<Button>(window).Where(b => Equals(b.Content, StudioStrings.Get("License.Exit"))).All(b => b.ActualWidth >= 100), "Small license dialog clipped the close action");
        }
        finally { window.Close(); StudioStrings.Apply(beforeLanguage); TopSolidTheme.Apply(beforeTheme); }
        Check.True(window.ClosedTask.IsCompleted, "Closing a license dialog did not release the startup wait");
    }
    private static async Task Layout(Window window) { window.UpdateLayout(); await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle); }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); if (child is T item) yield return item; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
}
