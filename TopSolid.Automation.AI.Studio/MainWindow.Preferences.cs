using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio;

public partial class MainWindow
{
    private void InitializePreferences()
    {
        AppearanceModeBox.SelectedValue = settings.AppearanceMode;
        InterfaceLanguageBox.SelectedValue = settings.InterfaceLanguage;
        ResponseLanguageBox.SelectedValue = settings.ResponseLanguage;
        Settings.PreviewDefaults.Current = settings.PreviewDefaults;
        PreviewEdgesBox.IsChecked = settings.PreviewDefaults.Edges;
        PreviewPartBox.IsChecked = settings.PreviewDefaults.Part;
        PreviewStockBox.IsChecked = settings.PreviewDefaults.Stock;
        PreviewMachineBox.IsChecked = settings.PreviewDefaults.Machine;
        SettingsNavigation.SelectedValue = "ai";
        ShowSettingsSection("ai");
        BindKnownStrings(this);
        StudioStrings.Changed += InterfaceStringsChanged;
        Closed += (_, _) => StudioStrings.Changed -= InterfaceStringsChanged;
    }

    // Bind fixed labels once. Editable values, model IDs, paths and conversation data remain untouched.
    private static void BindKnownStrings(DependencyObject root)
    {
        if (root is FrameworkElement element)
        {
            void Bind(DependencyProperty property)
            {
                if (element.ReadLocalValue(property) is string original && StudioStrings.KeyForText(original) is { } key)
                    element.SetResourceReference(property, "Ui." + key);
            }
            if (element is TextBlock) Bind(TextBlock.TextProperty);
            if (element is ContentControl && !(element is ComboBoxItem { Tag: string language } &&
                language is "en" or "ko" or "ja" or "zh" or "fr" or "de" or "es" or "pt")) Bind(ContentControl.ContentProperty);
            Bind(ToolTipProperty);
            Bind(AutomationProperties.NameProperty);
            if (element is Button { Tag: string }) Bind(TagProperty);
        }
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) BindKnownStrings(child);
    }

    private void InterfaceStringsChanged() => OnUi(() =>
    {
        if (closing) return;
        StudioStrings.InitializeResources(this);
        UpdateThemeStatus();
        UpdateStatus();
        UpdateConnectionIndicator();
        SettingsFeedback.Text = "";
        EndpointBox.ToolTip = EndpointBox.IsReadOnly ? StudioStrings.Text("Service URL is filled automatically. Choose Custom OpenAI-compatible to use another endpoint.") : null;
        ApiKeyBox.ToolTip = visibleProvider == "Ollama" ? null : StudioStrings.Text(AI.CloudServices.Get(settings.CloudService).KeyHint);
        PermissionBox.ToolTip = Chat.PermissionPolicy.Description(permissionMode);
        if (cadMenu != null) RefreshContextModes();
        RenderAttachments();
        RefreshChatPresentation();
    });

    private void SettingsSection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (AiSettingsSection == null || SettingsNavigation.SelectedValue is not string section) return;
        ShowSettingsSection(section);
    }

    private void ShowSettingsSection(string section)
    {
        AiSettingsSection.Visibility = section == "ai" ? Visibility.Visible : Visibility.Collapsed;
        TopSolidSettingsSection.Visibility = section == "topsolid" ? Visibility.Visible : Visibility.Collapsed;
        AppearanceSettingsSection.Visibility = section == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        LanguageSettingsSection.Visibility = section == "languages" ? Visibility.Visible : Visibility.Collapsed;
        DeveloperSettingsSection.Visibility = section == "developer" ? Visibility.Visible : Visibility.Collapsed;
        CamColorsSettingsSection.Visibility = section == "camColors" ? Visibility.Visible : Visibility.Collapsed;
        SettingsScroller.ScrollToTop();
    }

    private void Appearance_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loading || AppearanceModeBox.SelectedValue is not string mode) return;
        settings.AppearanceMode = mode;
        themeFollower?.SetMode(mode);
        UpdateThemeStatus();
    }

    private void PreviewDefaults_Changed(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        settings.PreviewDefaults = new(PreviewEdgesBox.IsChecked == true, PreviewPartBox.IsChecked == true,
            PreviewStockBox.IsChecked == true, PreviewMachineBox.IsChecked == true);
        Settings.PreviewDefaults.Current = settings.PreviewDefaults;
    }

    private void InterfaceLanguage_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loading || InterfaceLanguageBox.SelectedValue is not string language) return;
        settings.InterfaceLanguage = language;
        StudioStrings.Apply(language);
    }

    private void ResponseLanguage_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loading || ResponseLanguageBox.SelectedValue is not string language) return;
        settings.ResponseLanguage = language;
        if (session != null) session.ResponseLanguage = language;
    }
}
