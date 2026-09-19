using System;
using System.IO;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void Connections()
        {
            Check(TopSolidInstances.ParsePipe("\"C:\\Program Files\\TopSolid.exe\" -pipeName cad-720-a") == "cad-720-a", "Pipe command-line discovery failed");
            Check(TopSolidInstances.ParsePipe("TopSolid.exe -pipeName \"cad-720-b\"") == "cad-720-b", "Quoted pipe discovery failed");
            Check(TopSolidInstances.ParsePipe("TopSolid.exe") == "", "Default instance pipe changed");
            var target = new ConnectionTarget(new TopSolidConnectionOptions { Mode = "tcp", Host = "192.168.0.2", Port = 8090, ExpectedVersion = "7.20" });
            target.Verify(42, 720000000);
            Throws<InvalidOperationException>(() => target.Verify(43, 720000000));
            Throws<InvalidOperationException>(() => target.Verify(42, 721000000));
            var secret = new string('a', 48);
            HttpsGateway.ValidateToken(secret);
            Throws<ArgumentException>(() => HttpsGateway.ValidateToken("short"));
            Check(HttpsGateway.Authorized("Bearer " + secret, secret), "Gateway rejected valid auth");
            Check(!HttpsGateway.Authorized("Bearer " + new string('b', 48), secret), "Gateway accepted invalid auth");
            Check(!HttpsGateway.Authorized(null, secret), "Gateway accepted anonymous auth");
            Check(HttpsGateway.ReadLine(new StringReader("abc\r\n"), 4) == "abc", "CRLF relay framing");
            Throws<InvalidDataException>(() => HttpsGateway.ReadLine(new StringReader("abcde"), 4));
            Console.WriteLine("PASS connection target pinning, pipe parsing, gateway authentication and message bounds.");
        }
    }
}
