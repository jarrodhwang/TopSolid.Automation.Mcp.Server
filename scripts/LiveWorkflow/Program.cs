using System.Diagnostics;
using System.Text;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Settings;

// Run from the repository root. This uses only explicit local Ollama configuration;
// saved application settings and cloud credentials are never read.
var model = args.Length > 0 ? args[0] : "qwen3.5:9b-q4_K_M";
var server = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(
    "TopSolid.Automation.AI.Studio", "bin", "Release", "net10.0-windows", "McpServer",
    "TopSolid.Automation.Mcp.Server.AddIn.exe"));
var logPath = Path.GetFullPath(args.Length > 2 ? args[2] : Path.Combine("artifacts", "live-smoke", "live-result.txt"));
Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
using var log = new StreamWriter(logPath, append: false, new UTF8Encoding(false)) { AutoFlush = true };
var logGate = new object();
void Write(string kind, string text)
{
    lock (logGate)
    {
        var line = $"[{DateTime.UtcNow:O}] {kind}: {text}";
        Console.WriteLine(line);
        log.WriteLine(line);
    }
}

Write("Configuration", $"Ollama http://localhost:11434; model={model}; server={server}");
var settings = new AppSettings
{
    Provider = AppSettings.OllamaProvider,
    OllamaServerUrl = "http://localhost:11434",
    OllamaModel = model,
    McpServerPath = server
};
await using var mcp = new StdioMcpClient();
mcp.Diagnostic += message => Write("MCP diagnostic", message);
try
{
    using var connectDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
    await mcp.ConnectAsync(server, connectDeadline.Token);
    Write("Discovered tools", string.Join(", ", mcp.Tools.Select(tool => tool.Name)));
    using var provider = ProviderFactory.Create(settings);
    var session = new ChatSession(provider, mcp);
    var calledTools = new List<string>();
    session.Trace += trace =>
    {
        if (trace.Kind == "Tool call") calledTools.Add(trace.Text.Split(' ', 2)[0]);
        Write(trace.Kind, trace.Text);
    };

    var cases = new[]
    {
        (Prompt: "Am I connected to TopSolid? Use the available tool to check.", ExpectedTool: "topsolid_get_status"),
        (Prompt: "What document is currently open? Use the available TopSolid tool to check the active document now.", ExpectedTool: "topsolid_get_active_document")
    };
    foreach (var test in cases)
    {
        calledTools.Clear();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var timer = Stopwatch.StartNew();
        Write("User", test.Prompt);
        var answer = await session.SendAsync(test.Prompt, deadline.Token);
        Write("Assistant", answer);
        if (!calledTools.Contains(test.ExpectedTool, StringComparer.Ordinal))
            throw new InvalidOperationException($"The model answered without the expected tool {test.ExpectedTool}.");
        Write("PASS", $"{test.ExpectedTool}; elapsed={timer.Elapsed.TotalSeconds:F1}s");
    }
    Write("COMPLETE", "Both user -> actual Ollama -> MCP -> TopSolid Automation -> model-answer cases passed.");
    return 0;
}
catch (Exception exception)
{
    Write("FAILED", exception.GetType().Name + ": " + exception.Message);
    return 1;
}

