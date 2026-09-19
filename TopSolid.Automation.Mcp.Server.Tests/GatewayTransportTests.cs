using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Automation.Mcp.Server.AddIn.Protocol;

namespace TopSolid.Automation.Mcp.Server.Tests
{
    internal static partial class Program
    {
        private static void GatewayTransport()
        {
            using (var socket = new ScriptedGatewaySocket())
            {
                var run = HttpsGateway.ServeSocket(socket);
                Check(run.Wait(TimeSpan.FromSeconds(20)), "Gateway worker did not close after EOF");
                var responses = socket.Responses;
                Check(responses.Count == 2, "Gateway dropped or duplicated a response");
                Check((string)responses[0]["result"]["protocolVersion"] == "2025-03-26", "Gateway initialize response changed");
                Check(((JArray)responses[1]["result"]["tools"]).Count > 100, "Gateway did not tunnel the real worker catalog");
                Console.WriteLine("PASS authenticated-session transport: isolated real STA worker, initialize, notifications, tools/list and graceful EOF.");
            }
        }

        private sealed class ScriptedGatewaySocket : WebSocket
        {
            private readonly Queue<byte[]> incoming = new Queue<byte[]>();
            private readonly TaskCompletionSource<bool> receivedCatalog = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal readonly List<JObject> Responses = new List<JObject>();
            private WebSocketState state = WebSocketState.Open;
            internal ScriptedGatewaySocket()
            {
                incoming.Enqueue(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new TopSolidConnectionOptions { Selection = "pipe", PipeName = "fixture-no-host" })));
                incoming.Enqueue(Encoding.UTF8.GetBytes(new JObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "initialize", ["params"] = new JObject {
                    ["protocolVersion"] = "2025-03-26", ["capabilities"] = new JObject(), ["clientInfo"] = new JObject { ["name"] = "gateway-test", ["version"] = "1" } } }.ToString(Formatting.None)));
                incoming.Enqueue(Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}"));
                incoming.Enqueue(Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}"));
            }
            public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken token)
            {
                if (incoming.Count > 0)
                {
                    var bytes = incoming.Dequeue(); Array.Copy(bytes, 0, buffer.Array, buffer.Offset, bytes.Length);
                    return new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, true);
                }
                using (token.Register(() => receivedCatalog.TrySetCanceled())) await receivedCatalog.Task.ConfigureAwait(false);
                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
            }
            public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool end, CancellationToken token)
            {
                var message = JObject.Parse(Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count));
                Responses.Add(message);
                if ((int?)message["id"] == 2) receivedCatalog.TrySetResult(true);
                return Task.CompletedTask;
            }
            public override void Abort() { state = WebSocketState.Aborted; }
            public override void Dispose() { state = WebSocketState.Closed; }
            public override Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken token) { state = WebSocketState.Closed; return Task.CompletedTask; }
            public override Task CloseOutputAsync(WebSocketCloseStatus status, string description, CancellationToken token) => CloseAsync(status, description, token);
            public override WebSocketCloseStatus? CloseStatus => null;
            public override string CloseStatusDescription => null;
            public override WebSocketState State => state;
            public override string SubProtocol => null;
        }
    }
}
