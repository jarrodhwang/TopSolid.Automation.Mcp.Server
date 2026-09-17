using System.Windows;
using System.Windows.Threading;
using TopSolid.Automation.AI.Studio.Diagnostics;

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
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            DiagnosticLog.Write("info", "application.starting");
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
