using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Diagnostics;

/// <summary>Builds the operator-facing JSON snapshot without ever serializing API keys.</summary>
public static class LogExportBuilder
{
    public static JObject Build(
        AppSettings settings,
        string settingsFilePath,
        string? lastLoadWarning,
        string modelStatus,
        bool mcpConnected,
        bool mutationInFlight,
        IReadOnlyList<McpToolDefinition> tools,
        IReadOnlyList<IReadOnlyList<AiMessage>> conversations,
        SessionLogSnapshot currentSession,
        DiagnosticLog diagnosticLog)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settingsFilePath);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(conversations);
        ArgumentNullException.ThrowIfNull(currentSession);
        ArgumentNullException.ThrowIfNull(diagnosticLog);

        var root = new JObject
        {
            ["format"] = "TopSolid Automation AI Studio diagnostic export",
            ["schemaVersion"] = 2,
            ["exportedAtUtc"] = DateTimeOffset.UtcNow,
            ["application"] = new JObject
            {
                ["name"] = "TopSolid Automation AI Studio",
                ["version"] = typeof(LogExportBuilder).Assembly.GetName().Version?.ToString() ?? "unknown",
                ["processId"] = Environment.ProcessId,
                ["framework"] = RuntimeInformation.FrameworkDescription,
                ["os"] = RuntimeInformation.OSDescription,
                ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
                ["is64BitProcess"] = Environment.Is64BitProcess
            },
            ["configuration"] = Configuration(settings, settingsFilePath, lastLoadWarning, modelStatus),
            ["chat"] = SerializeChat(currentSession.Chat),
            ["conversations"] = new JArray(conversations.Select(turn => new JArray(turn.Select(SerializeMessage)))),
            ["mcp"] = new JObject
            {
                ["connected"] = mcpConnected,
                ["mutationInFlight"] = mutationInFlight,
                ["toolCount"] = tools.Count,
                ["tools"] = SerializeTools(tools),
                ["trace"] = SerializeTrace(currentSession.Trace)
            },
            ["automaticDiagnostics"] = new JObject
            {
                ["scope"] = "current application session",
                ["sessionId"] = diagnosticLog.SessionId,
                ["sessionStartedAtUtc"] = diagnosticLog.SessionStartedAtUtc,
                ["currentLogFile"] = diagnosticLog.CurrentLogFilePath,
                ["currentLog"] = diagnosticLog.ReadCurrentLog(),
                ["currentFallbackLogFile"] = diagnosticLog.CurrentFallbackLogFilePath,
                ["currentFallbackLog"] = DiagnosticLog.ReadFile(diagnosticLog.CurrentFallbackLogFilePath),
                ["allHistoryLogFile"] = diagnosticLog.AllLogFilePath,
                ["allHistoryFallbackLogFile"] = diagnosticLog.AllFallbackLogFilePath,
                ["mcpServerAllHistoryLogFile"] = DiagnosticLog.GetAllHistoryLogFilePath("mcp-server")
            },
            ["privacy"] = new JObject
            {
                ["apiKeys"] = "omitted and redacted",
                ["note"] = "Chat, tool arguments, TopSolid names and tool results may contain sensitive project information."
            }
        };

        // The structured configuration intentionally contains no key values. The
        // final pass also catches a key pasted into chat or returned by an endpoint.
        return JObject.Parse(diagnosticLog.Redact(root.ToString(Formatting.None)));
    }

    private static JObject Configuration(AppSettings settings, string settingsFilePath, string? lastLoadWarning, string modelStatus)
    {
        var profiles = new JObject();
        foreach (var profile in settings.CloudProfiles.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            profiles[profile.Key] = new JObject
            {
                ["baseUrl"] = profile.Value.BaseUrl,
                ["model"] = profile.Value.Model,
                ["apiKeyConfigured"] = !string.IsNullOrWhiteSpace(profile.Value.ApiKey)
            };
        }

        return new JObject
        {
            ["provider"] = settings.Provider,
            ["cloudService"] = settings.CloudService,
            ["cloudBaseUrl"] = settings.CloudBaseUrl,
            ["cloudModel"] = settings.CloudModel,
            ["cloudApiKeyConfigured"] = !string.IsNullOrWhiteSpace(settings.ApiKey),
            ["ollamaServerUrl"] = settings.OllamaServerUrl,
            ["ollamaModel"] = settings.OllamaModel,
            ["requestTimeoutMinutes"] = settings.RequestTimeoutMinutes,
            ["ollamaFastGptOss"] = settings.OllamaFastGptOss,
            ["devMode"] = settings.DevMode,
            ["appearanceMode"] = settings.AppearanceMode,
            ["interfaceLanguage"] = settings.InterfaceLanguage,
            ["responseLanguage"] = settings.ResponseLanguage,
            ["mcpServerPath"] = settings.McpServerPath,
            ["settingsFilePath"] = settingsFilePath,
            ["modelStatus"] = modelStatus,
            ["lastLoadWarning"] = lastLoadWarning,
            ["cloudProfiles"] = profiles
        };
    }

    private static JArray SerializeChat(IEnumerable<SessionChatEntry> entries)
        => new(entries.Select(entry => new JObject
        {
            ["sequence"] = entry.Sequence,
            ["timestampUtc"] = entry.TimestampUtc,
            ["role"] = entry.Role,
            ["text"] = entry.Text,
            ["elapsedMilliseconds"] = entry.ElapsedMilliseconds
        }));

    private static JArray SerializeTrace(IEnumerable<SessionTraceEntry> entries)
        => new(entries.Select(entry => new JObject
        {
            ["sequence"] = entry.Sequence,
            ["timestampUtc"] = entry.TimestampUtc,
            ["kind"] = entry.Kind,
            ["text"] = entry.Text
        }));

    private static JArray SerializeTools(IEnumerable<McpToolDefinition> tools)
        => new(tools.Select(tool => new JObject
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["requiresConfirmation"] = tool.RequiresConfirmation,
            ["inputSchema"] = tool.InputSchema.DeepClone(),
            ["annotations"] = tool.Annotations.DeepClone(),
            ["metadata"] = tool.Metadata.DeepClone()
        }));

    private static JObject SerializeMessage(AiMessage message)
    {
        var output = new JObject
        {
            ["role"] = message.Role,
            ["content"] = message.Content
        };
        if (message.ToolCallId != null) output["toolCallId"] = message.ToolCallId;
        if (message.ToolName != null) output["toolName"] = message.ToolName;
        if (message.Thinking != null) output["thinking"] = message.Thinking;
        if (message.ProviderContent != null) output["providerContent"] = message.ProviderContent.DeepClone();
        if (message.ExtraContent != null) output["extraContent"] = message.ExtraContent.DeepClone();
        if (message.ToolCalls.Count > 0)
        {
            output["toolCalls"] = new JArray(message.ToolCalls.Select(call => new JObject
            {
                ["id"] = call.Id,
                ["name"] = call.Name,
                ["arguments"] = call.Arguments.DeepClone(),
                ["argumentsError"] = call.ArgumentsError,
                ["rawArguments"] = call.RawArguments,
                ["extraContent"] = call.ExtraContent?.DeepClone()
            }));
        }
        return output;
    }
}
