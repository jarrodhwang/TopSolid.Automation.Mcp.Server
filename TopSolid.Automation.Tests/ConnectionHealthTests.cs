using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Connections;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.Tests;

internal static class ConnectionHealthTests
{
    public static Task Run()
    {
        foreach (var (json, expected) in new[]
        {
            ("{\"connected\":true}", true), ("{\"connected\":false}", false),
            ("{\"connected\":\"true\"}", false), ("{\"connected\":null}", false),
            ("{}", false), ("invalid JSON", false), ("[]", false)
        })
        {
            var result = new McpToolResult { Content = new JArray(new JObject { ["type"] = "text", ["text"] = json }) };
            Check.Equal(expected, TopSolidConnectionStatus.IsConnected(result), "Text status readiness: " + json);
            result.StructuredContent = new JObject { ["connected"] = false };
            Check.True(!TopSolidConnectionStatus.IsConnected(result), "Text overrode structured disconnected status");
            result.StructuredContent["connected"] = true;
            Check.True(TopSolidConnectionStatus.IsConnected(result), "Structured connected status was missed");
            result.IsError = true;
            Check.True(!TopSolidConnectionStatus.IsConnected(result), "Error result was reported as connected");
        }
        var health = new ConnectionHealth();
        var start = DateTimeOffset.UtcNow;
        health.Pending("Mcp", "Health.McpChecking", start);
        health.Set("TopSolid", ConnectionSeverity.Pending, "Health.TopSolidWaiting");
        health.Pending("Ai", "Health.AiChecking", start);
        health.Tick(start.AddSeconds(7), TimeSpan.FromSeconds(8));
        Check.Equal(ConnectionSeverity.Pending, health.Severity, "Fast checks should retain normal spinning status");
        health.Tick(start.AddSeconds(8), TimeSpan.FromSeconds(8));
        Check.Equal(ConnectionSeverity.Warning, health.Severity, "Slow connection did not become a warning");
        Check.Equal("Health.TopSolidWaiting", health.Conditions.Single(c => c.Component == "TopSolid").MessageKey, "Waiting for MCP was misreported as TopSolid starting");
        health.Set("Mcp", ConnectionSeverity.Error, "Health.McpUnavailable");
        Check.Equal(ConnectionSeverity.Error, health.Severity, "A warning hid a crucial error");
        health.CancelPending();
        Check.True(health.Conditions.All(c => c.PendingSince == null && c.Severity != ConnectionSeverity.Pending), "Cancellation left a pending/spinning service");
        Check.Equal(ConnectionSeverity.Error, health.Severity, "Cancellation erased the diagnosed MCP failure");
        foreach (var component in new[] { "Mcp", "TopSolid", "Ai" }) health.Set(component, ConnectionSeverity.Ready, "Health." + component + "Ready");
        Check.Equal(ConnectionSeverity.Ready, health.Severity, "Complete connections did not recover");
        health.Set("AiRuntime", ConnectionSeverity.Warning, "Health.AiLimit");
        health.Set("Ai", ConnectionSeverity.Ready, "Health.AiReady");
        health.FinishPending("AiRuntime");
        Check.Equal(ConnectionSeverity.Warning, health.Severity, "Discovery or a stale completion cleared inference quota warning");
        health.Remove("AiRuntime");
        health.Pending("AiRuntime", "Health.AiPending", start);
        health.Tick(start.AddSeconds(9), TimeSpan.FromSeconds(8));
        Check.Equal("Health.AiSlow", health.Conditions.Single(c => c.Component == "AiRuntime").MessageKey, "Pending inference was not reported");
        health.Pending("AiRuntime", "Health.AiPending", start.AddSeconds(10));
        health.Tick(start.AddSeconds(11), TimeSpan.FromSeconds(8));
        Check.Equal(ConnectionSeverity.Pending, health.Severity, "A new model round inherited the previous round's slow timer");
        health.FinishPending("AiRuntime");
        Check.Equal(ConnectionSeverity.Ready, health.Severity, "Completed model request left a stale pending warning");

        foreach (var (error, expected) in new (Exception, ConnectionSeverity)[]
        {
            (new AiProviderException("raw private details", HttpStatusCode.TooManyRequests), ConnectionSeverity.Warning),
            (new AiProviderException("raw private details", HttpStatusCode.Unauthorized), ConnectionSeverity.Error),
            (new AiProviderException("raw private details", HttpStatusCode.Forbidden), ConnectionSeverity.Error),
            (new AiProviderException("raw private details", HttpStatusCode.ServiceUnavailable), ConnectionSeverity.Warning),
            (new HttpRequestException("private endpoint"), ConnectionSeverity.Warning),
            (new TimeoutException(), ConnectionSeverity.Warning),
            (new ArgumentException(), ConnectionSeverity.Error)
        })
        {
            var problem = ConnectionHealth.AiFailure(error);
            Check.Equal(expected, problem.Severity, "Connection severity classification");
            Check.True(!problem.Key.Contains(error.Message, StringComparison.Ordinal), "Raw exception leaked into user-facing state");
        }
        var original = StudioStrings.CurrentLanguage;
        try
        {
            foreach (var key in StudioStrings.Keys.Where(k => k.StartsWith("Health.", StringComparison.Ordinal)))
            {
                StudioStrings.Apply("en"); var english = StudioStrings.Get(key);
                StudioStrings.Apply("ko"); var korean = StudioStrings.Get(key);
                Check.True(english != key && korean != key, "Missing connection translation: " + key);
                if (key != "Health.Component.TopSolid") Check.True(english != korean, "Untranslated Korean connection text: " + key);
            }
        }
        finally { StudioStrings.Apply(original); }
        return Task.CompletedTask;
    }
}
