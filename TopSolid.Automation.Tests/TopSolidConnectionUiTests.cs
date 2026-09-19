using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TopSolid.Automation.AI.Studio;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Connections;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class TopSolidConnectionUiTests
{
    internal static Task Run()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(async () => {
                Application? app = null; MainWindow? window = null;
                try
                {
                    RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
                    app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/TopSolid.Automation.AI.Studio;component/Appearance/TopSolidStyles.xaml", UriKind.Relative) });
                    StudioStrings.Apply("ko");
                    window = new MainWindow(autoConnect: false) { ShowInTaskbar = false, ShowActivated = false, Left = -18000, Top = -18000,
                        WindowStartupLocation = WindowStartupLocation.Manual, Width = 1040, Height = 920 };
                    ((CheckBox)window.FindName("DevModeBox")).IsChecked = false;
                    window.Show();
                    ((Button)window.FindName("SettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    ((ListBox)window.FindName("SettingsNavigation")).SelectedValue = "topsolid";
                    ((ComboBox)window.FindName("InterfaceLanguageBox")).SelectedValue = "ko";
                    var editor = (TopSolidConnectionEditor)window.FindName("TopSolidEditor");
                    var output = Path.GetFullPath("artifacts/topsolid-connection-review"); Directory.CreateDirectory(output);
                    foreach (var dark in new[] { false, true })
                    {
                        TopSolidTheme.Apply(new TopSolidThemeSnapshot(dark, dark ? "Dark" : "Light", "Connection settings fixture"));
                        foreach (var mode in new[] { "local", "tcp", "https" })
                        {
                            editor.Load(new AppSettings { TopSolidConnection = new TopSolidConnectionOptions { Mode = mode, Host = "192.168.10.20", Port = mode == "tcp" ? 8090 : 443,
                                Selection = "instance", ExpectedVersion = "7.20", ProcessId = 1234, StartTimeUtcTicks = 4567 } });
                            window.UpdateLayout(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                            Check.True(((StackPanel)window.FindName("TopSolidSettingsSection")).IsVisible, "TopSolid settings menu did not show the editor");
                            Check.True(((Grid)editor.FindName("RemoteFields")).Visibility == (mode == "local" ? Visibility.Collapsed : Visibility.Visible), "Remote fields visibility");
                            var width = (int)window.ActualWidth; var height = (int)window.ActualHeight;
                            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
                            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                            using var stream = File.Create(Path.Combine(output, mode + (dark ? "-dark" : "-light") + "-ko.png")); encoder.Save(stream);
                        }
                    }
                    ((TextBox)editor.FindName("PortBox")).Text = "";
                    ((ComboBox)editor.FindName("SelectionBox")).SelectedValue = "auto";
                    ((PasswordBox)editor.FindName("TokenBox")).Password = new string('a', 48);
                    Check.Equal(443, editor.Read().Port, "Empty HTTPS port did not use 443");
                    ((TextBox)editor.FindName("HostBox")).Text = "another.example.test";
                    Check.Equal("", editor.GatewayToken, "Endpoint change retained a gateway credential");
                    Check.True(((ComboBox)editor.FindName("InstanceBox")).SelectedItem == null, "Endpoint change retained a remote PID");
                    var blocked = new LicenseWindow { Owner = window, ShowInTaskbar = false, ShowActivated = false, Left = -18000, Top = -18000,
                        WindowStartupLocation = WindowStartupLocation.Manual };
                    try
                    {
                        blocked.EnableConnectionSettings(() => true);
                        blocked.ShowResult(new TopSolidLicenseStatus(), closeApplication: true); blocked.Show();
                        var retry = blocked.WaitForRetry();
                        var button = (Button)typeof(LicenseWindow).GetField("connectionSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(blocked)!;
                        Check.True(button.IsVisible, "Startup connection failure hid settings recovery");
                        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Check.True(await retry, "Saved startup settings did not request another gated check");
                    }
                    finally { blocked.Close(); }
                    Console.WriteLine("PASS TopSolid connection settings UI, Korean light/dark captures, HTTPS defaults and endpoint isolation.");
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally { window?.Close(); app?.Shutdown(); dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
}
