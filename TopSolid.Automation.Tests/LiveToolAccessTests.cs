using System.Diagnostics;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class LiveToolAccessTests
{
    public static async Task Run(string server, string model)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        await using var native = new StdioMcpClient();
        await native.ConnectAsync(server, deadline.Token);
        using var provider = new OllamaProvider("http://localhost:11434", model, requestTimeout: TimeSpan.FromMinutes(4));
        var guarded = new ReadOnlySession(native);
        var traces = new JArray(); var confirmations = 0;
        var session = new ChatSession(provider, guarded) { ConfirmChangeAsync = (_, _) => { confirmations++; return Task.FromResult(false); } };
        session.Trace += t => { traces.Add(new JObject { ["kind"] = t.Kind, ["text"] = t.Text }); if (t.Kind is "Timing" or "Model" or "Tool call") Console.WriteLine(t.Kind + ": " + t.Text); };
        var watch = Stopwatch.StartNew();
        const string request = "create part file name \"Local AI Made this part\" under the project \"AI Made this project\" , and create circle sketch in it";
        string? answer = null;
        try
        {
            answer = await session.SendAsync(request, deadline.Token);
            var lower = answer.ToLowerInvariant();
            Check.True(!new[] { "no tool", "don't have a tool", "does not include a command", "not part of the mcp catalog", "creation tool is unavailable" }.Any(lower.Contains), "Model still denied the registered creation capability: " + answer);
            Check.True(session.GetConversationSnapshot().SelectMany(t => t).Any(m => m.Role == "assistant"), "No completed model answer");
            Console.WriteLine($"Live {model}: {watch.Elapsed.TotalSeconds:F2}s, {guarded.Reads} reads, {confirmations} declined previews, zero native writes.\n{answer}");
        }
        finally
        {
            Directory.CreateDirectory("artifacts/log-review-0.5.2");
            File.WriteAllText("artifacts/log-review-0.5.2/live-model-access.json", new JObject {
                ["model"] = model, ["request"] = request, ["seconds"] = watch.Elapsed.TotalSeconds, ["answer"] = answer,
                ["readCalls"] = guarded.Reads, ["declinedPreviews"] = confirmations, ["nativeWrites"] = 0, ["trace"] = traces }.ToString());
        }
    }

    private sealed class ReadOnlySession(StdioMcpClient native) : IConfirmableMcpClient
    {
        public int Reads;
        public bool IsConnected => native.IsConnected;
        public IReadOnlyList<McpToolDefinition> Tools => native.Tools;
        public Task<McpToolResult> CallToolAsync(string name, JObject arguments, CancellationToken token)
        {
            if (Tools.Any(t => t.Name == name && t.RequiresConfirmation)) throw new InvalidOperationException("Live validation is read-only.");
            Reads++; return native.CallToolAsync(name, arguments, token);
        }
        public Task<JObject> PrepareToolAsync(string name, JObject arguments, CancellationToken token) => native.PrepareToolAsync(name, arguments, token);
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject arguments, string confirmationToken, CancellationToken token)
            => throw new InvalidOperationException("Native execution is disabled in this validation harness.");
    }
}
