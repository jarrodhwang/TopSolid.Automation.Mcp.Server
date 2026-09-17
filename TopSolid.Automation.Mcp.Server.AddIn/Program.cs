using System;
using System.IO;
using System.Text;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;
using TopSolid.Automation.Mcp.Server.AddIn.Tools;

namespace TopSolid.Automation.Mcp.Server.AddIn
{
    internal static class Program
    {
        // All Automation calls, including Connect and Disconnect, stay on this one STA.
        [STAThread]
        private static int Main(string[] args)
        {
            Console.InputEncoding = new UTF8Encoding(false);
            Console.OutputEncoding = new UTF8Encoding(false);
            using (var protocolOutput = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true })
            {
                // A vendor diagnostic written to Console.Out must not corrupt the MCP wire.
                Console.SetOut(Console.Error);
                if (args.Length != 0)
                {
                    ServerDiagnosticLog.Write("error", "server.invalidArguments",
                        "Usage: TopSolid.Automation.Mcp.Server.AddIn.exe (MCP over stdin/stdout; no arguments)");
                    return 2;
                }
                try
                {
                    ServerDiagnosticLog.Write("info", "server.starting", "Starting TopSolid Automation MCP server.");
                    using (var automation = new AutomationGateway())
                        new StdioMcpServer(Console.In, protocolOutput, new ToolRegistry(automation)).Run();
                    return 0;
                }
                catch (IOException)
                {
                    ServerDiagnosticLog.Write("info", "server.stdioClosed", "MCP stdio pipe closed.");
                    return 0;
                } // Closed pipe: normal stdio client shutdown.
                catch (Exception ex)
                {
                    ServerDiagnosticLog.Write("error", "server.crash", "MCP server stopped unexpectedly.", ex);
                    return 1;
                }
            }
        }
    }
}
