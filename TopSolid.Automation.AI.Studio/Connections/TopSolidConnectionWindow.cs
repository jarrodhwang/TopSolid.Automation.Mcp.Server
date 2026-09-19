using System.Windows;
using System.Windows.Controls;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Settings;

namespace TopSolid.Automation.AI.Studio.Connections;

// Settings remain reachable before the license gate. Only an affirmative live check opens Studio.
internal sealed class TopSolidConnectionWindow : Window
{
    internal TopSolidConnectionWindow(AppSettings settings, SettingsStore store)
    {
        TopSolidTheme.InitializeResources(this); StudioStrings.InitializeResources(this);
        SetResourceReference(TitleProperty, "Ui.Nav.TopSolidConnection");
        Width = 680; Height = 770; MinWidth = 500; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        TopSolidTheme.ApplyWindow(this);
        var editor = new TopSolidConnectionEditor { Margin = new Thickness(24), ServerPath = () => AppSettings.ResolveServerPath(settings.McpServerPath, AppContext.BaseDirectory) };
        editor.Load(settings);
        var root = new DockPanel();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16) };
        var cancel = new Button { IsCancel = true, Margin = new Thickness(0, 0, 10, 0) }; cancel.SetResourceReference(ContentProperty, "Ui.Common.Cancel");
        var save = new Button(); save.SetResourceReference(ContentProperty, "Ui.TsConnection.SaveRetry");
        cancel.Click += (_, _) => Close();
        editor.WorkingChanged += () => save.IsEnabled = !editor.IsWorking;
        save.Click += (_, _) => {
            try
            {
                settings.TopSolidConnection = editor.Read(); settings.TopSolidGatewayToken = editor.GatewayToken;
                store.Save(settings); DialogResult = true;
            }
            catch (Exception error) { new ErrorWindow(StudioStrings.Text(error.Message)) { Owner = this }.ShowDialog(); }
        };
        actions.Children.Add(cancel); actions.Children.Add(save); DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
        root.Children.Add(new ScrollViewer { Content = editor, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = root;
    }
}
