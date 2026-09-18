using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Mcp;

/// <summary>One owned console process; newline-delimited MCP JSON-RPC on its standard streams.</summary>
public sealed class StdioMcpClient : IConfirmableMcpClient, IGraphicPreviewClient, IAsyncDisposable
{
    public const string ProtocolVersion = "2025-03-26";
    private readonly SemaphoreSlim requestGate = new(1, 1);
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JObject>> pending = new();
    private Process? process;
    private Task? stdoutPump;
    private Task? stderrPump;
    private long nextId;
    private volatile bool initialized;
    private volatile bool mutationInFlight;
    private volatile bool mutationOutcomeUncertain;
    public event Action<string>? Diagnostic;
    public event Action? ConnectionChanged;
    public IReadOnlyList<McpToolDefinition> Tools { get; private set; } = [];
    public bool IsConnected => initialized && process is { HasExited: false };
    public bool IsMutationInFlight => mutationInFlight;

    public async Task ConnectAsync(string executablePath, CancellationToken cancellationToken)
    {
        await lifecycleGate.WaitAsync(cancellationToken);
        try { await ConnectCoreAsync(executablePath, cancellationToken); }
        finally { lifecycleGate.Release(); }
    }

    private async Task ConnectCoreAsync(string executablePath, CancellationToken cancellationToken)
    {
        await DisconnectCoreAsync();
        mutationOutcomeUncertain = false;
        var fullPath = Path.GetFullPath(executablePath.Trim());
        if (!File.Exists(fullPath) || !fullPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("Select the built TopSolid MCP server executable.", fullPath);
        var start = new ProcessStartInfo(fullPath)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = Path.GetDirectoryName(fullPath)!
        };
        var child = Process.Start(start) ?? throw new IOException("The MCP server did not start.");
        process = child;
        child.StandardInput.AutoFlush = true;
        stdoutPump = ReadOutputAsync(child);
        stderrPump = ReadErrorsAsync(child);
        try
        {
            var response = await RequestAsync("initialize", new JObject
            {
                ["protocolVersion"] = ProtocolVersion,
                ["capabilities"] = new JObject(),
                ["clientInfo"] = new JObject { ["name"] = "TopSolid Automation AI Studio", ["version"] = "0.5.19" }
            }, cancellationToken);
            if ((string?)response["protocolVersion"] != ProtocolVersion || response["capabilities"]?["tools"] is not JObject)
                throw new IOException("The MCP server does not support the required protocol/tools capability.");
            await child.StandardInput.WriteLineAsync(new JObject
            {
                ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized"
            }.ToString(Formatting.None).AsMemory(), cancellationToken);
            var tools = new List<McpToolDefinition>();
            string? cursor = null;
            var seenCursors = new HashSet<string>(StringComparer.Ordinal);
            do
            {
                var parameters = new JObject();
                if (cursor != null) parameters["cursor"] = cursor;
                var list = await RequestAsync("tools/list", parameters, cancellationToken);
                if (list["tools"] is not JArray entries) throw new IOException("Invalid MCP tool list.");
                foreach (var entry in entries)
                {
                    var tool = entry.ToObject<McpToolDefinition>() ?? throw new IOException("Invalid MCP tool.");
                    if (string.IsNullOrWhiteSpace(tool.Name) || (string?)tool.InputSchema["type"] != "object" || tools.Any(t => t.Name == tool.Name))
                        throw new IOException("Invalid or duplicate MCP tool definition.");
                    tools.Add(tool);
                    if (tools.Count > 256) throw new IOException("The MCP server returned too many tools.");
                }
                cursor = (string?)list["nextCursor"];
                if (cursor != null && !seenCursors.Add(cursor)) throw new IOException("MCP tool pagination repeated a cursor.");
            } while (cursor != null);
            Tools = tools.AsReadOnly();
            initialized = true;
            if (child.HasExited) throw new IOException("The MCP server exited during startup.");
            ConnectionChanged?.Invoke();
        }
        catch { await DisconnectCoreAsync(); throw; }
    }

    public async Task<McpToolResult> CallToolAsync(string name, JObject arguments, CancellationToken cancellationToken)
        => await CallToolCoreAsync(name, arguments, null, cancellationToken);

    public async Task<TopSolidLicenseStatus> GetLicenseStatusAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected) throw new IOException("MCP is disconnected.");
        var response = await RequestAsync("topsolid/licenseStatus", new JObject(), cancellationToken);
        // Strictly validate the gate fields before deserialization: strings/numbers are not booleans.
        if (response["schemaVersion"]?.Type != JTokenType.Integer || (int?)response["schemaVersion"] != 1 ||
            response["requiredModule"]?.Type != JTokenType.Integer || (int?)response["requiredModule"] != TopSolidLicenseStatus.KernelBaseModule ||
            response["requiredLicenseValid"]?.Type != JTokenType.Boolean || response["licenses"] is not JArray licenses || licenses.Any(l => l is not JObject))
            throw new IOException("The MCP server returned an incomplete license verification.");
        return response.ToObject<TopSolidLicenseStatus>() ?? throw new IOException("Invalid license status.");
    }

    public async Task<JObject> GetGraphicPreviewAsync(JObject target, CancellationToken cancellationToken)
    {
        if (!IsConnected) throw new IOException("MCP is disconnected.");
        var request = RequestAsync("topsolid/graphicPreview", (JObject)target.DeepClone(), cancellationToken, drainAfterSend: true);
        try { return await request.WaitAsync(cancellationToken); }
        catch (OperationCanceledException)
        {
            // Switching a card/closing a dialog abandons display only. Drain the native read while holding
            // the request gate so it cannot overtake an approved change or destroy its confirmation token.
            _ = request.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
    }

    public async Task<JObject> PrepareToolAsync(string name, JObject arguments, CancellationToken cancellationToken)
    {
        if (!IsConnected) throw new IOException("MCP is disconnected.");
        if (!Tools.Any(t => t.Name == name && t.RequiresConfirmation)) throw new IOException("No discovered change tool with that name.");
        return await RequestAsync("topsolid/prepare", new JObject { ["name"] = name, ["arguments"] = arguments.DeepClone() }, cancellationToken);
    }

    public async Task<McpToolResult> CallConfirmedToolAsync(string name, JObject arguments, string confirmationToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(confirmationToken)) return McpToolResult.Error("User confirmation is required.");
        // Prevent Disconnect/Dispose from terminating the server inside a TopSolid modification.
        await lifecycleGate.WaitAsync(cancellationToken);
        try { return await CallToolCoreAsync(name, arguments, confirmationToken, cancellationToken); }
        finally { lifecycleGate.Release(); }
    }

    private async Task<McpToolResult> CallToolCoreAsync(string name, JObject arguments, string? confirmationToken, CancellationToken cancellationToken)
    {
        if (!IsConnected) throw new IOException("MCP is disconnected. Connect the server and try again.");
        if (!Tools.Any(t => t.Name == name)) return McpToolResult.Error("The requested tool was not discovered from this MCP server.");
        var elapsed = Stopwatch.StartNew();
        try
        {
            var parameters = new JObject { ["name"] = name, ["arguments"] = arguments.DeepClone() };
            if (confirmationToken != null) parameters["_meta"] = new JObject { ["confirmationToken"] = confirmationToken };
            var response = await RequestAsync("tools/call", parameters, cancellationToken, confirmationToken != null);
            if (response["content"] is not JArray) throw new IOException("Invalid MCP tool result: missing content.");
            return response.ToObject<McpToolResult>() ?? throw new IOException("Invalid MCP tool result.");
        }
        catch (McpRequestException ex)
        {
            // A valid JSON-RPC rejection (e.g. invalid tool arguments) can be corrected by the model.
            // Transport failures still end the turn so a broken process is never reported as healthy.
            return McpToolResult.Error(ex.Message);
        }
        finally { Diagnostic?.Invoke($"MCP {name}: {elapsed.Elapsed.TotalSeconds:F3} s"); }
    }

    private async Task<JObject> RequestAsync(string method, JObject parameters, CancellationToken cancellationToken, bool mutation = false, bool drainAfterSend = false)
    {
        await requestGate.WaitAsync(cancellationToken);
        var id = Interlocked.Increment(ref nextId);
        var completion = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var child = process;
            if (child == null || child.HasExited) throw new IOException("The MCP server is not running.");
            cancellationToken.ThrowIfCancellationRequested();
            pending[id] = completion;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(drainAfterSend ? CancellationToken.None : cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            if (mutation) { mutationInFlight = true; ConnectionChanged?.Invoke(); }
            JObject envelope;
            try
            {
                await child.StandardInput.WriteLineAsync(new JObject
                {
                    ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters
                }.ToString(Formatting.None).AsMemory(), mutation ? CancellationToken.None : timeout.Token);
                // Vendor modification calls cannot safely be cancelled. Let commit/rollback finish.
                envelope = mutation ? await completion.Task : await completion.Task.WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                // TopSolid remoting calls cannot reliably be interrupted. Discard this owned session.
                StopProcess(child);
                if (cancellationToken.IsCancellationRequested) throw;
                throw new TimeoutException("MCP request timed out after 30 seconds. Reconnect the server to retry.");
            }
            catch (Exception ex) when (mutation)
            {
                mutationOutcomeUncertain = true;
                throw new IOException("The connection failed during a CAD change. Its outcome is unknown. Inspect TopSolid before retrying. The server was not forcibly stopped.", ex);
            }
            if (envelope["error"] is JObject error)
                throw new McpRequestException($"MCP {method}: {(string?)error["message"] ?? "Protocol error"} ({error["code"]}).");
            return envelope["result"] as JObject ?? throw new IOException("MCP response has no result object.");
        }
        finally
        {
            pending.TryRemove(id, out _);
            if (mutation) { mutationInFlight = false; ConnectionChanged?.Invoke(); }
            requestGate.Release();
        }
    }

    private async Task ReadOutputAsync(Process child)
    {
        Exception failure = new IOException("MCP server exited. Reconnect to continue.");
        try
        {
            await foreach (var line in ReadLinesAsync(child.StandardOutput, GraphicPreviewQuality.MaximumRpcLineCharacters))
            {
                var message = JObject.Parse(line);
                if ((string?)message["jsonrpc"] != "2.0") throw new IOException("Invalid MCP JSON-RPC version.");
                if (message["method"] != null)
                {
                    // This client advertises no server-to-client request capabilities.
                    if (message["id"] != null) throw new IOException("Unexpected server-to-client MCP request.");
                    continue;
                }
                if (message["id"]?.Type == JTokenType.Integer && pending.TryGetValue((long)message["id"]!, out var waiter))
                    waiter.TrySetResult(message);
            }
        }
        catch (Exception ex) { failure = new IOException("MCP transport failed: " + ex.Message, ex); }
        finally
        {
            if (ReferenceEquals(process, child))
            {
                initialized = false;
                Tools = [];
                foreach (var waiter in pending.Values) waiter.TrySetException(failure);
                StopProcess(child);
                ConnectionChanged?.Invoke();
            }
        }
    }

    private async Task ReadErrorsAsync(Process child)
    {
        try
        {
            await foreach (var line in ReadLinesAsync(child.StandardError, 8192))
                Diagnostic?.Invoke(line.Length <= 2000 ? line : line[..2000]);
        }
        catch (IOException) { StopProcess(child); }
        catch (ObjectDisposedException) { }
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(StreamReader reader, int maximumCharacters)
    {
        var buffer = new char[4096];
        var line = new StringBuilder();
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
        {
            var offset = 0;
            while (offset < count)
            {
                var newline = Array.IndexOf(buffer, '\n', offset, count - offset);
                var length = (newline < 0 ? count : newline) - offset;
                if (line.Length + length > maximumCharacters) throw new IOException("MCP output exceeded the line size limit.");
                line.Append(buffer, offset, length);
                offset += length;
                if (newline >= 0)
                {
                    yield return line.ToString().TrimEnd('\r');
                    line.Clear();
                    offset++;
                }
            }
        }
        if (line.Length > 0) yield return line.ToString().TrimEnd('\r');
    }

    private void StopProcess(Process child)
    {
        if (mutationInFlight || mutationOutcomeUncertain) return;
        try { if (!child.HasExited) child.Kill(); } // Never stop TopSolid or other processes.
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    public async Task DisconnectAsync()
    {
        await lifecycleGate.WaitAsync();
        try { await DisconnectCoreAsync(); }
        finally { lifecycleGate.Release(); }
    }

    private async Task DisconnectCoreAsync()
    {
        if (mutationInFlight) throw new InvalidOperationException("Wait for the CAD change to finish before disconnecting.");
        initialized = false;
        Tools = [];
        var child = process;
        if (child == null) return;
        try
        {
            try { child.StandardInput.Close(); } catch (IOException) { }
            try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(1)); }
            catch (TimeoutException) { StopProcess(child); }
            if (stdoutPump != null) await stdoutPump.WaitAsync(TimeSpan.FromSeconds(2));
            if (stderrPump != null) await stderrPump.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (TimeoutException) { StopProcess(child); }
        finally
        {
            process = null;
            child.Dispose();
            foreach (var waiter in pending.Values) waiter.TrySetException(new IOException("MCP disconnected."));
            ConnectionChanged?.Invoke();
        }
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();

    internal sealed class McpRequestException(string message) : IOException(message);
}
