using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Mcp.Server.AddIn.Protocol
{
    // Private HTTPS/WebSocket tunnel for this bundled MCP server, not an HTTP MCP endpoint.
    // Each authenticated socket owns a separate STA worker and its confirmation tickets.
    internal static class HttpsGateway
    {
        private const int RequestLimit = 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        internal static async Task Relay(TopSolidConnectionOptions options, TextReader input, TextWriter output)
        {
            options.Validate();
            var token = Environment.GetEnvironmentVariable(TopSolidConnectionOptions.TokenEnvironmentName);
            ValidateToken(token);
            using (var socket = new ClientWebSocket())
            using (var lifetime = new CancellationTokenSource())
            {
                // Keep normal Windows trust, host-name validation and TLS negotiation. Never bypass certificates.
                socket.Options.SetRequestHeader("Authorization", "Bearer " + token);
                using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                    await socket.ConnectAsync(options.GatewayUri(), deadline.Token).ConfigureAwait(false);
                var local = JsonConvert.DeserializeObject<TopSolidConnectionOptions>(JsonConvert.SerializeObject(options));
                local.Mode = "local"; local.Host = "";
                await Send(socket, JsonConvert.SerializeObject(local), lifetime.Token).ConfigureAwait(false);
                var sending = Task.Run(async () => {
                    string line;
                    while ((line = ReadLine(input, RequestLimit)) != null) await Send(socket, line, lifetime.Token).ConfigureAwait(false);
                });
                var receiving = Task.Run(async () => {
                    string line;
                    while ((line = await Receive(socket, GraphicPreviewQuality.MaximumRpcLineCharacters * 4, lifetime.Token).ConfigureAwait(false)) != null)
                    { output.WriteLine(line); output.Flush(); }
                });
                await Task.WhenAny(sending, receiving).ConfigureAwait(false);
                lifetime.Cancel(); socket.Abort();
                // stdin may still block when the server closes; process exit closes it. Observe both faults.
                Observe(sending); Observe(receiving);
                if (sending.IsFaulted) await sending.ConfigureAwait(false);
                if (receiving.IsFaulted) await receiving.ConfigureAwait(false);
            }
        }

        internal static void ValidateToken(string token)
        {
            if (token == null || token.Length < 32 || token.Length > 256 ||
                !System.Text.RegularExpressions.Regex.IsMatch(token, @"\A[a-zA-Z0-9_+/=-]+\z"))
                throw new ArgumentException("The HTTPS gateway requires an access token of at least 32 characters.");
        }

        internal static async Task Serve(string prefix)
        {
            if (!Uri.TryCreate(prefix, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                uri.AbsolutePath != "/topsolid/" || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                throw new ArgumentException("Gateway URL must be https://host:port/topsolid/.");
            var secret = Environment.GetEnvironmentVariable("TOPSOLID_GATEWAY_TOKEN");
            ValidateToken(secret);
            using (var listener = new HttpListener())
            using (var slots = new SemaphoreSlim(8, 8))
            {
                listener.Prefixes.Add(prefix); listener.Start();
                Console.Error.WriteLine("TopSolid HTTPS gateway listening at " + prefix);
                while (listener.IsListening)
                {
                    var context = await listener.GetContextAsync().ConfigureAwait(false);
                    if (context.Request.Url.AbsolutePath != "/topsolid/" || context.Request.HttpMethod != "GET" ||
                        !context.Request.IsSecureConnection || context.Request.Headers["Origin"] != null ||
                        !Authorized(context.Request.Headers["Authorization"], secret))
                    { context.Response.StatusCode = 401; context.Response.Close(); continue; }
                    if (!context.Request.IsWebSocketRequest) { context.Response.StatusCode = 400; context.Response.Close(); continue; }
                    if (!slots.Wait(0)) { context.Response.StatusCode = 503; context.Response.Close(); continue; }
                    Observe(Task.Run(async () => {
                        try { await ServeSession(context).ConfigureAwait(false); }
                        catch (Exception error) { Console.Error.WriteLine("Gateway session ended: " + error.GetType().Name); }
                        finally { slots.Release(); }
                    }));
                }
            }
        }

        internal static bool Authorized(string header, string secret)
        {
            if (header == null || header.Length > 300) return false;
            using (var hash = SHA256.Create())
            {
                var expected = hash.ComputeHash(Utf8.GetBytes("Bearer " + secret));
                var actual = hash.ComputeHash(Utf8.GetBytes(header));
                var difference = 0;
                for (var i = 0; i < actual.Length; i++) difference |= expected[i] ^ actual[i];
                return difference == 0;
            }
        }

        private static async Task ServeSession(HttpListenerContext context)
        {
            var accepted = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
            using (var socket = accepted.WebSocket) await ServeSocket(socket).ConfigureAwait(false);
        }

        internal static async Task ServeSocket(WebSocket socket)
        {
            using (var lifetime = new CancellationTokenSource())
            {
                TopSolidConnectionOptions options;
                using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    var json = await Receive(socket, 8192, deadline.Token).ConfigureAwait(false);
                    options = JsonConvert.DeserializeObject<TopSolidConnectionOptions>(json ?? "null");
                    if (options == null || options.Mode != "local") throw new ArgumentException("Gateway sessions must target local TopSolid instances.");
                    options.Validate();
                }
                var start = new ProcessStartInfo(typeof(HttpsGateway).Assembly.Location) {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Utf8, StandardErrorEncoding = Utf8,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };
                start.EnvironmentVariables[TopSolidConnectionOptions.EnvironmentName] = JsonConvert.SerializeObject(options);
                start.EnvironmentVariables.Remove(TopSolidConnectionOptions.TokenEnvironmentName);
                start.EnvironmentVariables.Remove("TOPSOLID_GATEWAY_TOKEN");
                using (var child = Process.Start(start))
                {
                    child.StandardInput.AutoFlush = true;
                    Console.Error.WriteLine("Gateway worker started: " + child.Id);
                    var errors = Task.Run(async () => {
                        while (await child.StandardError.ReadLineAsync().ConfigureAwait(false) != null) { }
                    });
                    var incoming = Task.Run(async () => {
                        try
                        {
                            string line;
                            while ((line = await Receive(socket, RequestLimit, lifetime.Token).ConfigureAwait(false)) != null)
                                await child.StandardInput.WriteLineAsync(line).ConfigureAwait(false);
                        }
                        finally { child.StandardInput.Close(); }
                    });
                    var outgoing = Task.Run(async () => {
                        string line;
                        while ((line = ReadLine(child.StandardOutput, GraphicPreviewQuality.MaximumRpcLineCharacters)) != null)
                            await Send(socket, line, lifetime.Token).ConfigureAwait(false);
                    });
                    await Task.WhenAny(incoming, outgoing).ConfigureAwait(false);
                    lifetime.Cancel(); socket.Abort();
                    try { await incoming.ConfigureAwait(false); } catch { }
                    // EOF is queued after any accepted request. Let commit/rollback finish; never kill a worker mid-change.
                    try { child.StandardInput.Close(); } catch (IOException) { }
                    try { await outgoing.ConfigureAwait(false); } catch { }
                    await Task.Run(() => child.WaitForExit()).ConfigureAwait(false);
                    await errors.ConfigureAwait(false);
                    Console.Error.WriteLine("Gateway worker completed: " + child.Id);
                }
            }
        }

        internal static string ReadLine(TextReader reader, int maximum)
        {
            var line = new StringBuilder(); int next;
            while ((next = reader.Read()) != -1)
            {
                if (next == '\n') return line.ToString().TrimEnd('\r');
                if (line.Length >= maximum) throw new InvalidDataException("Gateway message exceeded its size limit.");
                line.Append((char)next);
            }
            return line.Length == 0 ? null : line.ToString();
        }
        private static Task Send(WebSocket socket, string text, CancellationToken token) =>
            socket.SendAsync(new ArraySegment<byte>(Utf8.GetBytes(text)), WebSocketMessageType.Text, true, token);
        private static async Task<string> Receive(WebSocket socket, int maximum, CancellationToken token)
        {
            var buffer = new byte[16384];
            using (var bytes = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) return null;
                    if (result.MessageType != WebSocketMessageType.Text || bytes.Length + result.Count > maximum)
                        throw new InvalidDataException("Invalid gateway message.");
                    bytes.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
                var text = Utf8.GetString(bytes.ToArray());
                if (text.IndexOfAny(new[] { '\n', '\r' }) >= 0) throw new InvalidDataException("Gateway messages must be single-line JSON.");
                return text;
            }
        }
        private static void Observe(Task task) => task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
