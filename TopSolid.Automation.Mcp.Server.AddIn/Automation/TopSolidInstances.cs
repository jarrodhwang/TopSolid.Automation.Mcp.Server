using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    internal static class TopSolidInstances
    {
        internal static List<TopSolidInstanceInfo> Discover()
        {
            var commands = new Dictionary<int, string>();
            try
            {
                using (var search = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='TopSolid.exe'"))
                {
                    search.Options.Timeout = TimeSpan.FromSeconds(4);
                    using (var results = search.Get())
                        foreach (ManagementObject row in results)
                            using (row) if (row["CommandLine"] is string command) commands[Convert.ToInt32(row["ProcessId"])] = command;
                }
            }
            catch (Exception error) when (error is ManagementException || error is UnauthorizedAccessException || error is COMException)
            { /* Missing command lines remain unknown; never guess a pipe. */ }
            var items = new List<TopSolidInstanceInfo>();
            foreach (var process in Process.GetProcessesByName("TopSolid"))
                using (process)
                {
                    try
                    {
                        if (process.SessionId != Process.GetCurrentProcess().SessionId) continue;
                        var version = process.MainModule.FileVersionInfo;
                        var pipeKnown = commands.TryGetValue(process.Id, out var command);
                        items.Add(new TopSolidInstanceInfo {
                            ProcessId = process.Id, StartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks,
                            Version = version.FileMajorPart + "." + version.FileMinorPart,
                            Supported = version.FileMajorPart > 7 || (version.FileMajorPart == 7 && version.FileMinorPart >= 18),
                            Title = process.MainWindowTitle, PipeKnown = pipeKnown, PipeName = pipeKnown ? ParsePipe(command) : ""
                        });
                    }
                    catch (Exception error) when (error is InvalidOperationException || error is System.ComponentModel.Win32Exception || error is ArgumentException) { }
                }
            return items.OrderByDescending(i => i.Version, StringComparer.Ordinal).ThenBy(i => i.ProcessId).ToList();
        }

        internal static string ParsePipe(string command)
        {
            int count;
            var argv = CommandLineToArgvW(command, out count);
            if (argv == IntPtr.Zero) throw new InvalidOperationException("Cannot read the TopSolid command line.");
            try
            {
                for (var i = 1; i < count - 1; i++)
                    if (string.Equals(Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size)), "-pipeName", StringComparison.OrdinalIgnoreCase))
                        return Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, (i + 1) * IntPtr.Size));
                return "";
            }
            finally { LocalFree(argv); }
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CommandLineToArgvW(string command, out int count);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    }
}
