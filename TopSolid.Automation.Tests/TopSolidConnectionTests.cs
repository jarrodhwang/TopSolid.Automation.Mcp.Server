using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class TopSolidConnectionTests
{
    internal static Task Settings()
    {
        var folder = Path.Combine(Path.GetTempPath(), "TopSolid.Connection.Tests", Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(folder);
        var secret = new string('a', 48);
        try
        {
            var settings = new AppSettings { TopSolidConnection = new() { Mode = "https", Host = "cad.example.test", Port = 9443,
                Selection = "instance", ExpectedVersion = "7.20", ProcessId = 42, StartTimeUtcTicks = 1234 }, TopSolidGatewayToken = secret };
            store.Save(settings);
            var text = File.ReadAllText(store.FilePath);
            Check.True(!text.Contains(secret) && !JsonConvert.SerializeObject(settings).Contains(secret), "Gateway secret leaked through serialization");
            var loaded = store.Load();
            Check.Equal(secret, loaded.TopSolidGatewayToken, "Gateway token DPAPI round trip");
            Check.Equal(42, loaded.TopSolidConnection.ProcessId, "Selected PID lost");
            Check.Equal(1234L, loaded.TopSolidConnection.StartTimeUtcTicks, "Process lifetime lost");
            Check.Equal("wss://cad.example.test:9443/topsolid/", loaded.TopSolidConnection.GatewayUri().AbsoluteUri, "Gateway route");
            var tampered = JObject.Parse(text); tampered["topSolidConnection"]!["Host"] = "different.example.test";
            File.WriteAllText(store.FilePath, tampered.ToString());
            Check.Equal("", store.Load().TopSolidGatewayToken, "Token must not move between endpoints");
            tampered["topSolidConnection"] = "invalid";
            File.WriteAllText(store.FilePath, tampered.ToString());
            Reject(store.Load().TopSolidConnection);
            Check.True(store.LastLoadWarning != null, "Invalid target must remain blocked and visible");
            tampered.Remove("topSolidConnection"); tampered.Remove("topSolidGatewayCredential");
            File.WriteAllText(store.FilePath, tampered.ToString());
            Check.Equal("local", store.Load().TopSolidConnection.Mode, "Existing settings must default to local");
            foreach (var host in new[] { "https://cad.example.test", "cad.example.test:443", "cad.example.test/path", "user@cad.example.test" })
                Reject(new() { Mode = "https", Host = host });
            Reject(new() { Mode = "tcp", Host = "192.168.1.2", Port = 0 });
            Reject(new() { Mode = "tcp", Host = "192.168.1.2", Port = 65536 });
            Reject(new() { Selection = "instance", ProcessId = 42 });
            Reject(new() { Selection = "pipe", PipeName = "../host" });
            Reject(new() { ExpectedVersion = "7.20.400" });
            Console.WriteLine("PASS TopSolid connection settings, endpoint validation, encrypted token and legacy migration.");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        return Task.CompletedTask;
    }
    private static void Reject(TopSolidConnectionOptions options)
    {
        try { options.Validate(); } catch (ArgumentException) { return; }
        throw new Exception("Invalid connection accepted: " + JsonConvert.SerializeObject(options));
    }

    internal static async Task Live(string server)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(55));
        await using var client = new StdioMcpClient();
        await client.ConnectAsync(server, new TopSolidConnectionOptions(), "", deadline.Token);
        var instances = await client.GetConnectionInstancesAsync(deadline.Token);
        Console.WriteLine(JsonConvert.SerializeObject(instances, Formatting.Indented));
        if (instances.Count > 1)
        {
            var ambiguous = await client.CallToolAsync("topsolid_get_status", new(), deadline.Token);
            Check.True(!AI.Studio.Connections.TopSolidConnectionStatus.IsConnected(ambiguous), "Automatic mode chose an arbitrary instance");
        }
        var target = instances.Where(i => i.Supported).OrderByDescending(i => i.Version, StringComparer.Ordinal).FirstOrDefault()
            ?? throw new Exception("A running TopSolid 7.18 or newer instance is required for the live check.");
        var options = new TopSolidConnectionOptions { Selection = "instance", ProcessId = target.ProcessId,
            StartTimeUtcTicks = target.StartTimeUtcTicks, ExpectedVersion = target.Version };
        await client.ConnectAsync(server, options, "", deadline.Token);
        var result = await client.CallToolAsync("topsolid_get_status", new(), deadline.Token);
        Check.True(AI.Studio.Connections.TopSolidConnectionStatus.IsConnected(result), "Selected live instance did not connect: " + JsonConvert.SerializeObject(result));
        Console.WriteLine("PASS selected live TopSolid " + target.Version + " instance, PID " + target.ProcessId);
        var license = await client.GetLicenseStatusAsync(deadline.Token);
        Check.True(license.CanStart, "Selected instance license is unavailable");
        options.StartTimeUtcTicks++;
        await client.ConnectAsync(server, options, "", deadline.Token);
        result = await client.CallToolAsync("topsolid_get_status", new(), deadline.Token);
        Check.True(!AI.Studio.Connections.TopSolidConnectionStatus.IsConnected(result), "Stale process identity was accepted");
        options.StartTimeUtcTicks--; options.ExpectedVersion = "7.99";
        await client.ConnectAsync(server, options, "", deadline.Token);
        result = await client.CallToolAsync("topsolid_get_status", new(), deadline.Token);
        Check.True(!AI.Studio.Connections.TopSolidConnectionStatus.IsConnected(result), "Wrong version was accepted");
        Console.WriteLine("PASS ambiguous target, stale PID lifetime and mismatched version rejected. Read-only checks only.");
    }
}
