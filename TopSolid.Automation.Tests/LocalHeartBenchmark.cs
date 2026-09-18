using System.Diagnostics;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

// Explicit opt-in: real local inference, real MCP tool discovery, synthetic read
// receipts and a declined preview. It cannot issue ANY native write or native read
// except status. This measures model planning, not native geometry execution.
internal static class LocalHeartBenchmark
{
    internal static async Task Run(string server, string model)
    {
        await using var discovery = new StdioMcpClient();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        await discovery.ConnectAsync(server, deadline.Token);
        var catalog = discovery.Tools.ToArray();
        var status = await discovery.CallToolAsync("topsolid_get_status", new JObject(), deadline.Token);
        Directory.CreateDirectory("artifacts/sketch-reliability-0.5.6");
        var evidence = new JArray();
        try
        {
            for (var run = 1; run <= 2; run++)
            {
                var fixture = new ContextFixture(catalog); var trace = new JArray(); var proposals = new JArray();
                using var provider = new OllamaProvider("http://localhost:11434", model, requestTimeout: TimeSpan.FromMinutes(4));
                var session = new ChatSession(provider, fixture) { ConfirmChangeAsync = (preview, _) => { proposals.Add(preview.DeepClone()); return Task.FromResult(false); } };
                session.Trace += t => { trace.Add(new JObject { ["kind"] = t.Kind, ["text"] = t.Text }); if (t.Kind is "Timing" or "Model" or "Model metrics" or "Tool call") Console.WriteLine(t.Kind + ": " + t.Text); };
                const string prompt = "프로젝트 이름은 'Recorded project', 파트 문서 이름은 'Recorded part'야. 그 파트에 하트 스케치를 그려줘. XY 평면, 가로 100mm 세로 80mm, 중심은 월드 좌표 (10,20,30)mm, 회전 0도. 섹션은 만들지 마. 저장은 하지 마.";
                var timer = Stopwatch.StartNew();
                var row = new JObject { ["run"] = run, ["model"] = model, ["nativeWrites"] = 0, ["contextSource"] = "synthetic document fixture; actual TopSolid state is not used", ["prompt"] = prompt, ["trace"] = trace, ["proposals"] = proposals };
                evidence.Add(row);
                try {
                    row["answer"] = await session.SendAsync(prompt, deadline.Token);
                    Check.Equal(1, proposals.Count, "Expected one valid heart proposal, declined by the fixture");
                    Check.Equal(1, fixture.Reads, "Named document lookup should take one MCP read");
                    Check.Equal(0, fixture.Writes, "A benchmark tried a native write");
                    Check.True((string)row["answer"]! == "Change declined. No changes were made.", "Benchmark must terminate at declined confirmation");
                }
                finally { row["seconds"] = timer.Elapsed.TotalSeconds; row["contextReads"] = fixture.Reads; }
                Console.WriteLine($"Benchmark {run}: {timer.Elapsed.TotalSeconds:F2}s to declined proposal; synthetic context; zero native writes.");
            }
        }
        finally { File.WriteAllText("artifacts/sketch-reliability-0.5.6/local-heart-benchmark.json", new JObject { ["actualTopSolidStatus"] = JObject.FromObject(status), ["runs"] = evidence }.ToString()); }
    }

    private sealed class ContextFixture(McpToolDefinition[] tools) : IConfirmableMcpClient
    {
        public int Reads, Writes;
        public bool IsConnected => true;
        public IReadOnlyList<McpToolDefinition> Tools => tools;
        public Task<McpToolResult> CallToolAsync(string name, JObject arguments, CancellationToken token)
        {
            Check.Equal("topsolid_get_modeling_context", name, "Unexpected read: benchmark expects one combined context lookup");
            Check.Equal("Recorded project", (string?)arguments["projectName"], "Wrong project");
            Check.Equal("Recorded part", (string?)arguments["documentName"], "Wrong part");
            Reads++;
            return Task.FromResult(new McpToolResult { StructuredContent = JObject.Parse("{complete:true,projectMatches:[{name:'Recorded project',pdmObjectId:'fixture-project'}],documents:[{name:'Recorded part',pdmObjectId:'fixture-part',documentId:'fixture-revision',extension:'.TopPrt',documentType:'TopSolid.Cad.Design.DB.Documents.PartDocument',isOpen:true}],canChooseUniqueDocument:true,errors:[]}") });
        }
        public Task<JObject> PrepareToolAsync(string name, JObject arguments, CancellationToken token)
        {
            Check.Equal("topsolid_create_heart_sketch", name, "Expected compact server-planned heart");
            Check.Equal("fixture-revision", (string?)arguments["documentId"], "Model invented a document ID");
            Check.Equal("xy", (string?)arguments["placement"], "Wrong plane");
            Check.Equal(100.0, (double?)arguments["width"], "Wrong width"); Check.Equal(80.0, (double?)arguments["height"], "Wrong height");
            foreach (var pair in new[] { ("x", 10.0), ("y", 20.0), ("z", 30.0) }) Check.Equal(pair.Item2, (double?)arguments["center"]?[pair.Item1], "Wrong world center");
            Check.Equal(0.0, (double?)arguments["rotationDegrees"], "Wrong orientation");
            var properties = (JObject)tools.Single(t => t.Name == name).InputSchema["properties"]!;
            Check.True(arguments.Properties().All(p => properties[p.Name] != null), "Unexpected heart/section argument");
            return Task.FromResult(new JObject { ["toolName"] = name, ["arguments"] = arguments.DeepClone(), ["confirmationToken"] = "fixture-only-not-a-real-token",
                ["target"] = new JObject { ["documentId"] = "fixture-revision", ["createsSections"] = false, ["fixture"] = true } });
        }
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject arguments, string confirmationToken, CancellationToken token)
        { Writes++; throw new InvalidOperationException("Native writes are disabled in the benchmark."); }
    }
}
