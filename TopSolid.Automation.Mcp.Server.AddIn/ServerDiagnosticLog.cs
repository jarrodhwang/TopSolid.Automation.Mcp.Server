using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.Mcp.Server.AddIn
{
    /// <summary>Crash-safe server diagnostics. MCP protocol data never goes to stdout.</summary>
    internal static class ServerDiagnosticLog
    {
        private static readonly object Gate = new object();

        public static string CurrentLogFilePath
        {
            get
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(root)) root = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(root, "TopSolid.Automation.AI.Studio", "Logs",
                    $"mcp-server-{DateTime.Now:yyyy-MM-dd}.log");
            }
        }

        public static void Write(string level, string eventName, string message, Exception exception = null)
        {
            var record = new JObject
            {
                ["timestampUtc"] = DateTime.UtcNow.ToString("O"),
                ["level"] = level,
                ["event"] = eventName,
                ["processId"] = Process.GetCurrentProcess().Id,
                ["message"] = message
            };
            if (exception != null)
            {
                record["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name;
                record["exception"] = exception.ToString();
            }
            var line = record.ToString(Formatting.None);
            lock (Gate)
            {
                try
                {
                    var path = CurrentLogFilePath;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite,
                               4096, FileOptions.WriteThrough))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.WriteLine(line);
                        writer.Flush();
                        stream.Flush(true);
                    }
                }
                catch { /* Preserve the protocol/error path when storage is unavailable. */ }
            }
            try { Console.Error.WriteLine(line); }
            catch { /* Never replace a server failure with a logging failure. */ }
        }
    }
}
