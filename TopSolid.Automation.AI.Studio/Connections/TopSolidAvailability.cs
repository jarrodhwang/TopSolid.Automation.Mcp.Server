using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace TopSolid.Automation.AI.Studio.Connections;

internal enum TopSolidAvailability { NotFound, NotRunning, Starting, NotResponding, Unknown }

internal static class TopSolidInstallation
{
    // Best-effort local evidence only. A successful MCP host query always wins,
    // including installations outside these conventional locations.
    internal static TopSolidAvailability Inspect()
    {
        try
        {
            var running = Process.GetProcessesByName("TopSolid");
            if (running.Length > 0)
            {
                try { return running.Any(p => p.Responding) ? TopSolidAvailability.Starting : TopSolidAvailability.NotResponding; }
                finally { foreach (var process in running) process.Dispose(); }
            }
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }.Distinct())
            {
                var directory = Path.Combine(root, "TOPSOLID");
                if (Directory.Exists(directory) && Directory.EnumerateDirectories(directory, "TopSolid 7*")
                    .Any(path => File.Exists(Path.Combine(path, "bin", "TopSolid.exe")))) return TopSolidAvailability.NotRunning;
            }
            foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    using var root = RegistryKey.OpenBaseKey(hive, view);
                    using var app = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\TopSolid.exe");
                    if (app?.GetValue("") is string executable && File.Exists(executable.Trim('"'))) return TopSolidAvailability.NotRunning;
                    using var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                    foreach (var name in uninstall?.GetSubKeyNames() ?? [])
                    {
                        using var entry = uninstall!.OpenSubKey(name);
                        if (entry?.GetValue("DisplayName") is not string label || !label.StartsWith("TopSolid", StringComparison.OrdinalIgnoreCase)) continue;
                        if (entry.GetValue("InstallLocation") is string location &&
                            (File.Exists(Path.Combine(location, "TopSolid.exe")) || File.Exists(Path.Combine(location, "bin", "TopSolid.exe"))))
                            return TopSolidAvailability.NotRunning;
                    }
                }
            return TopSolidAvailability.NotFound;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { return TopSolidAvailability.Unknown; }
    }
}
