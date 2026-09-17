using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Diagnostics;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class DiagnosticLogTests
{
    public static Task PersistentLogAndExportRedaction()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TopSolid.Automation.Tests", Guid.NewGuid().ToString("N"));
        var log = new DiagnosticLog(directory, "test-session");
        try
        {
            const string secret = "diagnostic-test-secret-42";
            log.SetSecrets([secret]);
            log.Write("info", "test.started", "The secret is " + secret,
                new JObject { ["value"] = secret });
            log.WriteException("test.failure", new InvalidOperationException("Failure includes " + secret));

            Check.True(!string.Equals(log.AllLogFilePath, log.CurrentLogFilePath, StringComparison.OrdinalIgnoreCase),
                "All-history and current-session log paths must be separate");
            var allHistory = File.ReadAllText(log.AllLogFilePath);
            var current = log.ReadCurrentLog() ?? "";
            foreach (var persisted in new[] { allHistory, current })
            {
                Check.True(persisted.Contains("test.started", StringComparison.Ordinal), "Diagnostic event was not persisted");
                Check.True(persisted.Contains("InvalidOperationException", StringComparison.Ordinal), "Exception type was not persisted");
                Check.True(persisted.Contains("Failure includes", StringComparison.Ordinal), "Exception details were not persisted");
                Check.True(!persisted.Contains(secret, StringComparison.Ordinal), "Diagnostic log exposed a configured secret");
            }
            const string historicalOnlyMarker = "historical-only-marker";
            File.AppendAllText(log.AllLogFilePath, historicalOnlyMarker + Environment.NewLine);

            var session = new SessionLog();
            session.AddChat("You", "What happened? " + secret);
            session.AddChat("Assistant", "Returned the verified result.", 1234.5);
            session.AddTrace("Tool result", "TopSolid returned a result.");
            var settings = new AppSettings
            {
                CloudBaseUrl = "https://example.test/v1",
                CloudModel = "test-model",
                ApiKey = secret,
                McpServerPath = "C:\\Test\\Mcp.Server.exe"
            };
            settings.CloudProfiles["backup"] = new CloudProfile
            {
                BaseUrl = "https://backup.example.test/v1",
                Model = "backup-model",
                ApiKey = secret
            };
            var tool = new McpToolDefinition
            {
                Name = "topsolid_get_status",
                Description = "Read status",
                InputSchema = JObject.Parse("{\"type\":\"object\"}"),
                Annotations = new JObject { ["readOnlyHint"] = true }
            };
            var message = new AiMessage
            {
                Role = "user",
                Content = "Investigate " + secret,
                ToolCalls = [new AiToolCall { Id = "call-1", Name = tool.Name, Arguments = new JObject() }]
            };
            var export = LogExportBuilder.Build(settings, "C:\\Test\\settings.json", "none", "ready", true, false,
                [tool], [[message]], session.Snapshot(), log);
            var json = export.ToString();
            Check.True(!json.Contains(secret, StringComparison.Ordinal), "Export exposed a configured secret");
            Check.True(!json.Contains(historicalOnlyMarker, StringComparison.Ordinal),
                "Export included content from the separate all-history log");
            Check.Equal("backup-model", (string?)export["configuration"]?["cloudProfiles"]?["backup"]?["model"],
                "Export omitted profile configuration");
            Check.Equal(true, (bool?)export["configuration"]?["cloudApiKeyConfigured"],
                "Export did not report key presence separately");
            Check.Equal(log.CurrentLogFilePath, (string?)export["automaticDiagnostics"]?["currentLogFile"],
                "Export did not identify the current-session log");
            Check.True(((string?)export["automaticDiagnostics"]?["currentLog"] ?? "").Contains("test.started", StringComparison.Ordinal),
                "Export did not include the current-session log from startup to now");
            Check.Equal(log.AllLogFilePath, (string?)export["automaticDiagnostics"]?["allHistoryLogFile"],
                "Export did not identify the separate all-history log");
            Check.True(export["automaticDiagnostics"]?["studioLogTail"] == null,
                "Export included the legacy Studio daily-log tail");
            Check.True(export["automaticDiagnostics"]?["mcpServerLogTail"] == null,
                "Export included the MCP all-history log tail");
            Check.Equal(1, ((JArray?)export["mcp"]?["tools"])?.Count ?? 0, "Export omitted MCP tool definitions");
            Check.Equal("What happened? [redacted]", (string?)export["chat"]?[0]?["text"],
                "Export did not redact current chat");
            Check.Equal(1234.5, (double?)export["chat"]?[1]?["elapsedMilliseconds"], "Export must retain measured response time");
            Check.Equal("topsolid_get_status", (string?)export["conversations"]?[0]?[0]?["toolCalls"]?[0]?["name"],
                "Export omitted conversation tool calls");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        return Task.CompletedTask;
    }
}
