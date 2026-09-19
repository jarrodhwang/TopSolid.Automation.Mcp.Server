using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;

namespace TopSolid.Automation.Tests;

internal static class ProcessTests
{
    // Optional real-process transport check. No CAD data is created or changed;
    // status may successfully report that TopSolid is absent or unavailable.
    public static async Task ServerHandshakeAndStatus(string executablePath)
    {
        await using var client = new StdioMcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await client.ConnectAsync(executablePath, timeout.Token);
        Check.True(client.IsConnected, "MCP initialize did not establish a connection");
        Check.True(client.Tools.Count >= 90, "Expanded domain tools were not discovered");
        ToolExposureTests.VerifyCreationWorkflow(client.Tools.ToArray());
        SketchReliabilityTests.VerifyCatalog(client.Tools.ToArray());
        ToolExposureTests.VerifyParameterWorkflows(client.Tools.ToArray());
        ToolExposureTests.VerifyCamWorkflows(client.Tools.ToArray());
        ToolExposureTests.VerifyModelingWorkflows(client.Tools.ToArray());
        Check.Equal(64, client.Tools.Count(t => t.RequiresConfirmation), "All 64 change tools should require confirmation");
        foreach (var name in new[] { "topsolid_get_status", "topsolid_get_active_document", "topsolid_get_document_info" })
            Check.True(client.Tools.Any(tool => tool.Name == name), $"Missing discovered tool: {name}");
        var status = await client.CallToolAsync("topsolid_get_status", new JObject(), timeout.Token);
        Check.True(status.Content.Count > 0, "TopSolid status has no content");
        Check.True(!status.IsError, "Status should report availability as data");
        var statusData = status.StructuredContent ?? JObject.Parse((string)status.Content[0]["text"]!);
        Check.True(statusData["connected"]?.Type == JTokenType.Boolean, "TopSolid status must report a boolean connection state");
        Check.Equal(statusData["connected"]!.Value<bool>(),
            TopSolid.Automation.AI.Studio.Connections.TopSolidConnectionStatus.IsConnected(status),
            "Studio readiness disagrees with the live server status");
        Console.WriteLine("Live TopSolid connection: " + statusData["connected"]);
        if (statusData["connected"]!.Value<bool>())
        {
            var documentResult = await client.CallToolAsync("topsolid_get_active_document", new JObject(), timeout.Token);
            var data = PdmInventory.Data(documentResult);
            if (!documentResult.IsError && data?["document"] is JObject)
            {
                var sources = new QuestionSources();
                sources.Capture("live-document", "topsolid_get_active_document", new JObject(), documentResult);
                var question = sources.Create(new JObject { ["question"] = "Choose the document", ["kind"] = "select", ["itemKind"] = "document",
                    ["sources"] = new JArray(new JObject { ["toolCallId"] = "live-document", ["path"] = "/document" }) });
                Check.Equal((string?)data["document"]!["name"], question.Choices.Single().Label, "Live document name did not reach question card");
                Check.True(JToken.DeepEquals(data["document"], question.Answer(selectedKeys: [question.Choices[0].Key]).Data["selected"]![0]!["value"]), "Live document choice changed its native identity");
                Console.WriteLine("Live read-only document question: name and exact identity verified.");
            }
        }
        await RealToolLoop(client, ollama: false, timeout.Token);
        await RealToolLoop(client, ollama: true, timeout.Token);
        var unknown = await client.CallToolAsync("invented_cad_function", new JObject(), timeout.Token);
        Check.True(unknown.IsError, "Undiscovered tools should be rejected");
        var invalid = await client.CallToolAsync("topsolid_get_status", new JObject { ["unexpected"] = true }, timeout.Token);
        Check.True(invalid.IsError, "Server must reject unexpected tool arguments");
        var write = await client.CallToolAsync("topsolid_create_rectangle2d", new JObject { ["documentId"] = "unapproved-target", ["placement"] = "2d", ["width"] = 20, ["height"] = 10 }, timeout.Token);
        Check.True(write.IsError && write.Content.ToString().Contains("confirmation", StringComparison.OrdinalIgnoreCase), "Modeling bypassed explicit confirmation");
        var reference = await client.CallToolAsync("topsolid_search_api_reference", new JObject { ["query"] = "ISketches2D.CreateSketchIn2D" }, timeout.Token);
        Check.True(!reference.IsError && reference.Content.ToString().Contains("CreateSketchIn2D", StringComparison.Ordinal), "Offline API reference lookup failed");
        var confirmations = 0;
        using (var invalidProvider = new FakeAiProvider { Reply = (round, _, _) => Task.FromResult(round == 1
            ? FakeAiProvider.ToolReply(new AiToolCall { Id = "invalid-proposal", Name = "topsolid_create_circle2d",
                Arguments = new JObject { ["documentId"] = "never-accessed", ["placement"] = "xy", ["radius"] = -1 } })
            : new AiReply { Content = "The invalid geometry was rejected before confirmation." }) })
        {
            var session = new ChatSession(invalidProvider, client) { ConfirmChangeAsync = (_, _) => { confirmations++; return Task.FromResult(false); } };
            await session.SendAsync("Exercise a rejected circle sketch proposal without modifying TopSolid.", timeout.Token);
            Check.Equal(0, confirmations, "An invalid preview reached the confirmation UI");
            Check.True(invalidProvider.Requests.Last().Any(m => m.Role == "tool" && m.Content.Contains("not prepared or executed", StringComparison.Ordinal)),
                "A prepare rejection ended the chat instead of returning an actionable tool error to the model");
        }
        await client.DisconnectAsync();
        Check.True(!client.IsConnected, "Disconnect did not clear connection status");
        Check.Equal(0, client.Tools.Count, "Disconnect should clear discovered tools");
        await Check.ThrowsAsync<IOException>(() => client.CallToolAsync("topsolid_get_status", new JObject(), CancellationToken.None));
    }

    private static async Task RealToolLoop(StdioMcpClient client, bool ollama, CancellationToken cancellationToken)
    {
        var function = new JObject { ["name"] = "topsolid_get_status",
            ["arguments"] = ollama ? new JObject() : new JValue("{}") };
        var call = new JObject { ["id"] = "actual-process-status", ["type"] = "function", ["function"] = function };
        var toolMessage = new JObject { ["role"] = "assistant", ["content"] = "", ["tool_calls"] = new JArray(call) };
        var finalMessage = new JObject { ["role"] = "assistant", ["content"] = "Status received from TopSolid." };
        string Envelope(JObject message) => ollama
            ? new JObject { ["done"] = true, ["message"] = message }.ToString()
            : new JObject { ["choices"] = new JArray(new JObject { ["finish_reason"] = "stop", ["message"] = message }) }.ToString();
        var handler = new ScriptedHandler().Json(Envelope(toolMessage)).Json(Envelope(finalMessage));
        using IAiProvider provider = ollama
            ? new OllamaProvider("http://localhost:11434", "fake-model", handler)
            : new OpenAiCompatibleProvider("https://example.test/v1", "", "fake-model", handler);
        var answer = await new ChatSession(provider, client).SendAsync("Am I connected to TopSolid?", cancellationToken);
        Check.Equal("Status received from TopSolid.", answer, "Real-process tool-loop answer");
        var returned = ((JArray)handler.Requests[1].Body!["messages"]!).Single(m => (string?)m["role"] == "tool");
        var toolResult = JObject.Parse((string)returned["content"]!);
        var evidence = toolResult["structuredContent"] as JObject ?? JObject.Parse((string)toolResult["content"]![0]!["text"]!);
        Check.True(evidence["connected"]?.Type == JTokenType.Boolean,
            "Real server status evidence did not traverse back into the model HTTP request");
    }
}
