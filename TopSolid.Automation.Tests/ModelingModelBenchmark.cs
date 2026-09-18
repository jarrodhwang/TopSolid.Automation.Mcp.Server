using System.Diagnostics;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

// Opt-in inference only. Synthetic document state and a declined proposal:
// no real user document content is sent to the model and no CAD call can run.
internal static class ModelingModelBenchmark
{
    internal static async Task Run(string executable, string mode)
    {
        if (mode is not ("cloud" or "ollama")) throw new ArgumentException("Choose cloud or ollama.");
        var store = new SettingsStore(); var savedBytes = File.ReadAllBytes(store.FilePath); var settings = store.Load();
        settings.Provider = mode == "cloud" ? AppSettings.OpenAiProvider : AppSettings.OllamaProvider;
        settings.RequestTimeoutMinutes = 3;
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        await using var discovery = new StdioMcpClient(); await discovery.ConnectAsync(executable, deadline.Token);
        var runs = new JArray();
        try {
            foreach (var method in new[] { "extrude", "revolve" }) {
                var fixture = new Fixture(discovery.Tools.ToArray(), method); var trace = new JArray();
                using var provider = ProviderFactory.Create(settings);
                var session = new ChatSession(provider, fixture) { ConfirmChangeAsync = (_, _) => Task.FromResult(false) };
                session.Trace += e => { trace.Add(new JObject { ["kind"] = e.Kind, ["text"] = e.Text }); if (e.Kind is "Timing" or "Model" or "Model metrics") Console.WriteLine(e.Kind + ": " + e.Text); };
                var prompt = method == "extrude" ? "In the current opened part create a cylinder: diameter 100mm, height 350mm, base center (0,0,0), along +Z, using extrusion. Apply red to the cylinder. Use absolute values, no parameters and no save."
                    : "현재 열린 파트에 원기둥을 회전(revolve) 방식으로 만들어줘. 지름 100mm, 높이 350mm, 바닥 중심 (0,0,0), 축 +Z, 빨간색. 절대값으로 만들고 파라미터 생성 및 저장은 하지 마.";
                var timer = Stopwatch.StartNew(); var row = new JObject { ["method"] = method, ["provider"] = settings.Provider, ["model"] = mode == "cloud" ? settings.CloudModel : settings.OllamaModel,
                    ["prompt"] = prompt, ["trace"] = trace, ["nativeWrites"] = 0, ["context"] = "synthetic document; proposal declined, no native geometry validation" }; runs.Add(row);
                try {
                    row["answer"] = await session.SendAsync(prompt, deadline.Token);
                    Check.Equal(1, fixture.Proposals, "Expected one semantically correct cylinder proposal");
                    Check.Equal(1, fixture.Reads, "Cylinder should require one active-document read");
                    Check.Equal("Change declined. No changes were made.", (string?)row["answer"], "Benchmark must stop at declined proposal");
                    row["passed"] = true;
                } finally { row["seconds"] = timer.Elapsed.TotalSeconds; row["documentReads"] = fixture.Reads; row["proposals"] = fixture.Proposals; row["arguments"] = fixture.Arguments; }
                Console.WriteLine($"{mode}/{method}: {timer.Elapsed.TotalSeconds:F2}s to correct declined proposal; no native writes.");
            }
        } finally {
            Directory.CreateDirectory("artifacts/modeling-0.5.8"); File.WriteAllText("artifacts/modeling-0.5.8/" + mode + "-model-benchmark.json", runs.ToString());
            Check.True(savedBytes.SequenceEqual(File.ReadAllBytes(store.FilePath)), "Benchmark changed saved settings");
        }
    }
    private sealed class Fixture(McpToolDefinition[] tools, string method) : IConfirmableMcpClient
    {
        internal int Reads, Proposals;
        internal JObject? Arguments;
        public bool IsConnected => true;
        public IReadOnlyList<McpToolDefinition> Tools => tools;
        public Task<McpToolResult> CallToolAsync(string name, JObject args, CancellationToken token)
        {
            Check.Equal("topsolid_get_active_document", name, "Cylinder benchmark expects only the active document lookup"); Reads++;
            return Task.FromResult(new McpToolResult { StructuredContent = JObject.Parse("{connected:true,hasActiveDocument:true,document:{documentId:'fixture-revision',name:'Fixture part'}}") });
        }
        public Task<JObject> PrepareToolAsync(string name, JObject args, CancellationToken token)
        {
            Check.Equal("topsolid_create_cylinder", name, "Wrong primitive/tool; cylinder must not become a box or Color parameter");
            // Match ordinary server schema failures so the existing bounded
            // invalid-proposal repair loop is exercised, not bypassed by this fixture.
            foreach (var required in new[] { "documentId", "diameter" })
                if (args[required] == null || args[required]!.Type == JTokenType.Null) throw new StdioMcpClient.McpRequestException("arguments." + required + " is required. No action was prepared or executed.");
            if (args["replaceShapes"] is JArray { Count: 0 }) throw new StdioMcpClient.McpRequestException("replaceShapes cannot be empty. Omit replaceShapes entirely for ordinary creation. No action was prepared or executed.");
            Check.Equal("fixture-revision", (string?)args["documentId"], "Invented document identifier");
            Check.Equal(method, (string?)args["method"] ?? "extrude", "Wrong native operation");
            var scale = (string?)args["units"] == "m" ? 1 : (string?)args["units"] == "cm" ? .01 : .001;
            Check.True(Math.Abs(((double?)args["diameter"] ?? 0)*scale-.1) < 1e-12, "Wrong cylinder diameter");
            Check.True(Math.Abs(((double?)args["height"] ?? 0)*scale-.35) < 1e-12, "Wrong cylinder height");
            Check.True(JToken.DeepEquals(args["color"], JObject.Parse("{name:'red'}")) || JToken.DeepEquals(args["color"], JObject.Parse("{r:255,g:0,b:0}")), "Actual red color was omitted");
            Check.True(args["heightParameter"] == null && args["replaceShapes"] == null, "Unrequested parameters or replacement");
            if (args["origin"] is JObject origin) Check.True(origin.Properties().All(p => (double)p.Value == 0), "Wrong cylinder origin");
            if (args["axisDirection"] is JObject axis) Check.True((double)axis["x"]! == 0 && (double)axis["y"]! == 0 && (double)axis["z"]! > 0, "Wrong cylinder axis");
            var properties = (JObject)tools.Single(t => t.Name == name).InputSchema["properties"]!;
            Check.True(args.Properties().All(p => properties[p.Name] != null), "Invented cylinder argument");
            Proposals++; Arguments = (JObject)args.DeepClone();
            return Task.FromResult(new JObject { ["toolName"] = name, ["arguments"] = args.DeepClone(), ["confirmationToken"] = "fixture-token-never-submitted", ["target"] = new JObject { ["documentId"] = "fixture-revision", ["fixture"] = true } });
        }
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject args, string token, CancellationToken cancellationToken) => throw new InvalidOperationException("Native writes are disabled in this benchmark.");
    }
}
