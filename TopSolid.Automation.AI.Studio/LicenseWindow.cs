using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio;

/// <summary>Startup progress, blocked-start explanation and read-only license inspection share one native dialog.</summary>
public sealed class LicenseWindow : Window
{
    private readonly TextBlock message = new() { FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock note = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) };
    private readonly TextBlock checkedAt = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
    private readonly ProgressBar progress = new() { IsIndeterminate = true, Height = 5, Margin = new Thickness(0, 4, 0, 12) };
    private readonly ListBox licenses = new() { MaxHeight = 360, Margin = new Thickness(0, 4, 16, 0), VerticalAlignment = VerticalAlignment.Top };
    private readonly Grid details = new();
    private readonly Button close = new() { IsCancel = true, IsDefault = true, MinWidth = 110 };
    private readonly TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TopSolidLicenseStatus? snapshot;
    private bool exitAfterClose;
    internal Task ClosedTask => closed.Task;
    internal TopSolidLicenseStatus? Snapshot => snapshot;

    public LicenseWindow()
    {
        StudioStrings.InitializeResources(this);
        SetResourceReference(TitleProperty, "Ui.License.Title");
        Icon = TopSolidIcons.Get("license");
        Width = 540; Height = 270; MinWidth = 500; MinHeight = 250; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        TopSolidTheme.ApplyWindow(this);
        var root = new DockPanel();
        var title = new TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold };
        title.SetResourceReference(TextBlock.TextProperty, "Ui.License.Title");
        var header = DialogLayout.Toolbar(DialogLayout.Heading(Icon, title)); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        DialogLayout.Command(close, "cancel"); close.Click += (_, _) => Close(); actions.Children.Add(close);
        var footer = DialogLayout.Footer(actions); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var body = new StackPanel();
        body.Children.Add(message); body.Children.Add(note); body.Children.Add(progress);
        var inspection = new Grid();
        inspection.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        inspection.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        inspection.Children.Add(licenses); Grid.SetColumn(details, 1); inspection.Children.Add(details);
        body.Children.Add(inspection); body.Children.Add(checkedAt);
        note.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
        checkedAt.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
        root.Children.Add(DialogLayout.Body(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled })); Content = root;
        licenses.SelectionChanged += (_, _) => RenderDetails();
        StudioStrings.Changed += Render;
        Closed += (_, _) => { StudioStrings.Changed -= Render; closed.TrySetResult(); };
        Render();
    }

    public void ShowResult(TopSolidLicenseStatus status, bool closeApplication = false)
    {
        if (snapshot == null)
        {
            var center = new Point(Left + Width / 2, Top + Height / 2);
            Width = 820; Height = 620; MinWidth = 660; MinHeight = 390; ResizeMode = ResizeMode.CanResize;
            if (IsVisible) { Left = center.X - Width / 2; Top = center.Y - Height / 2; }
        }
        snapshot = status; exitAfterClose = closeApplication; Render();
    }

    private void Render()
    {
        var selected = (licenses.SelectedItem as ListBoxItem)?.Tag as TopSolidLicenseInfo;
        var checking = snapshot == null;
        progress.Visibility = checking ? Visibility.Visible : Visibility.Collapsed;
        progress.IsIndeterminate = checking;
        close.Content = StudioStrings.Get(checking ? "Common.Cancel" : exitAfterClose ? "License.Exit" : "Common.Close");
        message.Text = StudioStrings.Get(checking ? "License.Checking" : snapshot!.CanStart ? "License.Allowed" :
            snapshot.RequiredLicenseValid == false ? "License.Denied" : "License.Unavailable");
        note.Text = StudioStrings.Get(checking ? "License.CheckingNote" : snapshot!.CanStart ? "License.Required" :
            snapshot.RequiredLicenseValid == false ? "License.DeniedNote" : "License.UnavailableNote");
        checkedAt.Text = snapshot?.CheckedAtUtc > DateTimeOffset.MinValue ? StudioStrings.Get("License.CheckedAt", snapshot.CheckedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)) : "";
        checkedAt.Visibility = string.IsNullOrEmpty(checkedAt.Text) ? Visibility.Collapsed : Visibility.Visible;
        licenses.Items.Clear();
        if (snapshot != null)
        {
            var entries = snapshot.Licenses.AsEnumerable();
            // Package licenses may omit an individual Kernel Base row. Display its separate,
            // authoritative module check without inventing a user, active flag or expiration date.
            if (!snapshot.Licenses.Any(l => l.Module == TopSolidLicenseStatus.KernelBaseModule))
                entries = entries.Prepend(new TopSolidLicenseInfo { Module = TopSolidLicenseStatus.KernelBaseModule,
                    Name = "TopSolid'Kernel Base", Valid = snapshot.RequiredLicenseValid });
            foreach (var entry in entries.OrderByDescending(l => l.Module == TopSolidLicenseStatus.KernelBaseModule).ThenBy(l => l.Name))
            {
                var row = new DockPanel { LastChildFill = true, Margin = new Thickness(4) };
                row.Children.Add(new Image { Source = TopSolidIcons.Get(TypeIcon(entry.Type)), Width = 22, Height = 22, Margin = new Thickness(0, 0, 8, 0) });
                var validity = new TextBlock { Text = Validity(entry.Valid), Margin = new Thickness(12, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(validity, Dock.Right); row.Children.Add(validity);
                row.Children.Add(new TextBlock { Text = LicenseName(entry), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
                licenses.Items.Add(new ListBoxItem { Content = row, Tag = entry, HorizontalContentAlignment = HorizontalAlignment.Stretch });
            }
        }
        licenses.Visibility = licenses.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(details, licenses.Items.Count > 0 ? 1 : 0);
        Grid.SetColumnSpan(details, licenses.Items.Count > 0 ? 1 : 2);
        licenses.SelectedItem = licenses.Items.OfType<ListBoxItem>().FirstOrDefault(item => ReferenceEquals(item.Tag, selected)) ?? licenses.Items.OfType<ListBoxItem>().FirstOrDefault();
        RenderDetails();
    }

    private void RenderDetails()
    {
        details.Children.Clear(); details.RowDefinitions.Clear(); details.ColumnDefinitions.Clear();
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
        details.ColumnDefinitions.Add(new ColumnDefinition());
        if (snapshot == null) return;
        Field("License.HostVersion", Value(snapshot.HostVersion));
        if ((licenses.SelectedItem as ListBoxItem)?.Tag is not TopSolidLicenseInfo item)
        { Field("License.Details", StudioStrings.Get("License.None")); return; }
        Field("License.Name", LicenseName(item));
        Field("License.Validity", Validity(item.Valid));
        if (!snapshot.Licenses.Contains(item))
        {
            Field("License.Details", StudioStrings.Get(snapshot.DetailsAvailable ? "License.ModuleDetailsUnavailable" : "License.DetailsUnavailable"));
            return;
        }
        Field("License.Expiration", item.ExpirationDate?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture) ?? StudioStrings.Get("License.NoExpiration"));
        Field("License.Active", StudioStrings.Get(item.Active ? "License.Yes" : "License.No"));
        Field("License.Type", StudioStrings.Get("License.Type." + (item.Type is "Standalone" or "Floating" or "ClsStandalone" or "ClsFloating" or "ClsUser" ? item.Type : "Unknown")));
        Field("License.User", Value(item.LicenseUser));
        Field("License.LicensedTo", Value(item.LicensedTo));
        Field("License.Status", Value(item.Status));
        Field("License.Version", Value(item.Version));
    }

    private void Field(string key, string value)
    {
        var row = details.RowDefinitions.Count;
        details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var label = new TextBlock { Text = StudioStrings.Get(key), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 12, 5) };
        label.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
        var text = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 5) };
        Grid.SetRow(label, row); Grid.SetRow(text, row); Grid.SetColumn(text, 1); details.Children.Add(label); details.Children.Add(text);
    }
    private static string LicenseName(TopSolidLicenseInfo entry) => string.IsNullOrWhiteSpace(entry.Name) ?
        entry.Module == TopSolidLicenseStatus.KernelBaseModule ? "TopSolid'Kernel Base" : StudioStrings.Get("License.Unnamed") : entry.Name;
    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? StudioStrings.Get("License.NotProvided") : value;
    private static string Validity(bool? value) => StudioStrings.Get(value == true ? "License.Valid" : value == false ? "License.Invalid" : "License.NotVerified");
    private static string TypeIcon(string? type) => type switch { "Standalone" or "ClsStandalone" => "license-standalone", "Floating" or "ClsFloating" => "license-floating", "ClsUser" => "license-user", _ => "license" };
}
