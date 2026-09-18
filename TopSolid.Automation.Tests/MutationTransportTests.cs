using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Mcp;

namespace TopSolid.Automation.Tests;

// The child is this harness in a dedicated fixture mode. It never loads the TopSolid SDK.
internal static class MutationTransportTests
{
    public static async Task Run()
    {
        foreach (var broken in new[] { false, true })
        {
            var receipt = Path.Combine(Path.GetTempPath(), "topsolid-mcp-fixture-" + Guid.NewGuid().ToString("N") + ".txt");
            await using var client = new StdioMcpClient();
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            client.Diagnostic += text => { if (text == "fixture modification started") started.TrySetResult(); };
            var original = Environment.GetEnvironmentVariable("TOPSOLID_MCP_TEST_FIXTURE");
            var originalReceipt = Environment.GetEnvironmentVariable("TOPSOLID_MCP_TEST_RECEIPT");
            var originalBroken = Environment.GetEnvironmentVariable("TOPSOLID_MCP_TEST_BROKEN");
            try
            {
                Environment.SetEnvironmentVariable("TOPSOLID_MCP_TEST_FIXTURE", "1");
                Environment.SetEnvironmentVariable("TOPSOLID_MCP_TEST_RECEIPT", receipt);
                Environment.SetEnvironmentVariable("TOPSOLID_MCP_TEST_BROKEN", broken ? "1" : "0");
                await client.ConnectAsync(Path.Combine(AppContext.BaseDirectory, "TopSolid.Automation.Tests.exe"), CancellationToken.None);
            }
            finally
            {
                Environment.SetEnvironmentVariable("TOPSOLID_MCP_TEST_FIXTURE", original);
                Environment.SetEnvironmentVariable("TOPSOLID_MCP_TEST_RECEIPT", originalReceipt);
                Environment.SetEnvironmentVariable("TOPSOLID_MCP_TEST_BROKEN", originalBroken);
            }
            try
            {
                using var cancellation = new CancellationTokenSource();
                var change = client.CallConfirmedToolAsync("fixture_change", new JObject(), "fixture-token", cancellation.Token);
                await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Check.True(client.IsMutationInFlight, "Mutation state was not exposed while the fixture was active");
                cancellation.Cancel();
                var disconnect = client.DisconnectAsync();
                if (broken)
                {
                    var error = await Check.ThrowsAsync<IOException>(() => change);
                    Check.True(error.Message.Contains("outcome is unknown", StringComparison.Ordinal), "Broken write transport did not report uncertainty");
                }
                else
                {
                    Check.True(!disconnect.IsCompleted, "Disconnect raced the active mutation");
                    Check.True(!(await change).IsError, "Cancellation killed a dispatched mutation");
                }
                await disconnect;
                Check.True(File.Exists(receipt) && File.ReadAllText(receipt) == "fixture completed", "The mutation process was killed before its cleanup completed");
                Check.True(!client.IsConnected && !client.IsMutationInFlight, "Client did not finish disconnecting");
            }
            finally { if (File.Exists(receipt)) File.Delete(receipt); }
        }
    }

    public static async Task<int> RunFixture()
    {
        Console.InputEncoding = new UTF8Encoding(false); Console.OutputEncoding = new UTF8Encoding(false);
        while (await Console.In.ReadLineAsync() is { } line)
        {
            var request = JObject.Parse(line); if (request["id"] == null) continue;
            JObject result;
            switch ((string?)request["method"])
            {
                case "initialize": result = new JObject { ["protocolVersion"] = StdioMcpClient.ProtocolVersion, ["capabilities"] = new JObject { ["tools"] = new JObject() } }; break;
                case "tools/list": result = new JObject { ["tools"] = new JArray(new JObject { ["name"] = "fixture_change", ["inputSchema"] = new JObject { ["type"] = "object" }, ["annotations"] = new JObject { ["readOnlyHint"] = false } }) }; break;
                case "topsolid/graphicPreview":
                    await Console.Error.WriteLineAsync("fixture preview started"); await Task.Delay(250);
                    result = new JObject { ["status"] = "unsupported", ["documentId"] = request["params"]!["documentId"]!.DeepClone() };
                    if ((string?)request["params"]!["documentId"] == "large-preview")
                        result["data"] = new string('A', (TopSolid.Automation.Mcp.Contracts.GraphicPreviewQuality.MaximumStlBytes + 2) / 3 * 4);
                    break;
                case "tools/call":
                    await Console.Error.WriteLineAsync("fixture modification started");
                    if (Environment.GetEnvironmentVariable("TOPSOLID_MCP_TEST_BROKEN") == "1") await Console.Out.WriteLineAsync("{malformed-fixture-response");
                    await Task.Delay(1500);
                    await File.WriteAllTextAsync(Environment.GetEnvironmentVariable("TOPSOLID_MCP_TEST_RECEIPT")!, "fixture completed");
                    if (Environment.GetEnvironmentVariable("TOPSOLID_MCP_TEST_BROKEN") == "1") return 0;
                    result = new JObject { ["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = "fixture completed" }), ["isError"] = false }; break;
                default: result = new JObject(); break;
            }
            await Console.Out.WriteLineAsync(new JObject { ["jsonrpc"] = "2.0", ["id"] = request["id"]!.DeepClone(), ["result"] = result }.ToString(Formatting.None));
        }
        return 0;
    }
}
