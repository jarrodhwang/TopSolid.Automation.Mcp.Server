using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TopSolid.Automation.AI.Studio.Diagnostics;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Licensing;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Settings;

namespace TopSolid.Automation.AI.Studio
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static DiagnosticLog DiagnosticLog { get; } = new();

        public App()
        {
            // Use the Windows-selected Direct3D adapter (NVIDIA/AMD/Intel), retaining WPF's device-loss/software fallback.
            // Offscreen fixture applications set SoftwareOnly explicitly; production never does.
            RenderOptions.ProcessRenderMode = RenderMode.Default;
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            DiagnosticLog.Write("info", "application.starting");
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            using var cancellation = new CancellationTokenSource();
            var client = new StdioMcpClient();
            LicenseWindow? startup = null;
            TopSolidThemeFollower? theme = null;
            var handedOff = false;
            try
            {
                var settings = new SettingsStore().Load();
                StudioStrings.Apply(settings.InterfaceLanguage);
                startup = new LicenseWindow();
                startup.EnableConnectionSettings(() => new Connections.TopSolidConnectionWindow(new SettingsStore().Load(), new SettingsStore()) { Owner = startup }.ShowDialog() == true);
                theme = new TopSolidThemeFollower(startup, settings.AppearanceMode);
                startup.Closed += (_, _) => { if (!handedOff) cancellation.Cancel(); };
                startup.Show();
                await Dispatcher.Yield(DispatcherPriority.ContextIdle);
                var retry = false;
                do
                {
                    retry = false;
                    settings = new SettingsStore().Load();
                    startup.ShowChecking();
                    var path = AppSettings.ResolveServerPath(settings.McpServerPath, AppContext.BaseDirectory);
                    await LicenseStartup.RunAsync(async token =>
                    {
                        await client.ConnectAsync(path, settings.TopSolidConnection, settings.TopSolidGatewayToken, token);
                        return await client.GetLicenseStatusAsync(token);
                    }, status =>
                    {
                        DiagnosticLog.Write("info", "license.startupAllowed", "TopSolid Kernel Base validity confirmed.");
                        var window = new MainWindow(autoConnect: true, connectedClient: client, licenseStatus: status);
                        MainWindow = window;
                        window.Show();
                        handedOff = true;
                        ShutdownMode = ShutdownMode.OnMainWindowClose;
                        startup.Close();
                    }, async status =>
                    {
                        DiagnosticLog.Write("warning", "license.startupBlocked", status.RequiredLicenseValid == false ? "Kernel Base license is not valid." : "Kernel Base license could not be verified.");
                        startup.ShowResult(status, closeApplication: true);
                        // Release the read-only helper before waiting for the user to dismiss the reason.
                        await client.DisposeAsync();
                        retry = await startup.WaitForRetry();
                    }, cancellation.Token);
                } while (retry && !cancellation.IsCancellationRequested);
            }
            catch (Exception error)
            {
                DiagnosticLog.WriteException("application.startupFailed", error, "Studio startup failed");
                startup?.Close();
                new ErrorWindow(StudioStrings.Get("License.StartupFailed")).ShowDialog();
            }
            finally
            {
                theme?.Dispose();
                if (!handedOff) await client.DisposeAsync();
            }
            if (!handedOff) Shutdown(1);
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
        {
            DiagnosticLog.WriteException("application.dispatcherCrash", args.Exception,
                "Unhandled WPF dispatcher exception");
        }

        private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is Exception exception)
                DiagnosticLog.WriteException("application.crash", exception, "Unhandled application exception");
            else
                DiagnosticLog.Write("error", "application.crash", "Unhandled application exception: " + args.ExceptionObject);
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
        {
            DiagnosticLog.WriteException("application.unobservedTaskException", args.Exception,
                "Unobserved task exception");
            args.SetObserved();
        }
    }

}
