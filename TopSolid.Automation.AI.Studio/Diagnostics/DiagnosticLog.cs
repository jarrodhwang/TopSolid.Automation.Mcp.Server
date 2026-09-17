using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.AI.Studio.Diagnostics;

/// <summary>
/// Small synchronous diagnostic sink used by the desktop client and its crash handlers.
/// Each record is flushed immediately so a later process crash does not lose the last event.
/// </summary>
public sealed class DiagnosticLog
{
    private const int MaximumTailCharacters = 200_000;
    private readonly object gate = new();
    private readonly string logDirectory;
    private readonly DateTimeOffset sessionStartedAt;
    private readonly string allLogFilePath;
    private readonly string currentLogFilePath;
    private readonly string allFallbackLogFilePath;
    private readonly string currentFallbackLogFilePath;
    private readonly List<string> secrets = [];
    private bool writeFailureReported;

    public DiagnosticLog(string? directory = null, string? sessionId = null)
    {
        logDirectory = string.IsNullOrWhiteSpace(directory) ? GetDefaultLogDirectory() : Path.GetFullPath(directory);
        SessionId = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId;
        sessionStartedAt = DateTimeOffset.Now;
        allLogFilePath = Path.Combine(logDirectory, GetAllHistoryLogFileName("studio", sessionStartedAt));
        currentLogFilePath = Path.Combine(logDirectory,
            GetSessionLogFileName("studio", sessionStartedAt, SessionId, Environment.ProcessId));

        var fallbackDirectory = Path.Combine(Path.GetTempPath(), "TopSolid.Automation.AI.Studio", "Logs");
        allFallbackLogFilePath = Path.Combine(fallbackDirectory, GetAllHistoryLogFileName("studio", sessionStartedAt));
        currentFallbackLogFilePath = Path.Combine(fallbackDirectory,
            GetSessionLogFileName("studio", sessionStartedAt, SessionId, Environment.ProcessId));
    }

    public string SessionId { get; }
    public string LogDirectory => logDirectory;
    public DateTimeOffset SessionStartedAtUtc => sessionStartedAt.ToUniversalTime();
    public string AllLogFilePath => allLogFilePath;
    public string CurrentLogFilePath => currentLogFilePath;
    public string AllFallbackLogFilePath => allFallbackLogFilePath;
    public string CurrentFallbackLogFilePath => currentFallbackLogFilePath;

    public static string GetDefaultLogDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = AppContext.BaseDirectory;
        return Path.Combine(root, "TopSolid.Automation.AI.Studio", "Logs");
    }

    public static string GetAllHistoryLogFilePath(string prefix, DateTimeOffset? timestamp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return Path.Combine(GetDefaultLogDirectory(), GetAllHistoryLogFileName(prefix, timestamp ?? DateTimeOffset.Now));
    }

    // Kept for callers compiled against the first diagnostics implementation.
    // This method refers to the daily all-history file, not a per-session file.
    public static string GetCurrentLogFilePath(string prefix, DateTimeOffset? timestamp = null)
        => GetAllHistoryLogFilePath(prefix, timestamp);

    public static string GetAllHistoryLogFileName(string prefix, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return $"{prefix}-{timestamp:yyyy-MM-dd}.log";
    }

    // Legacy name retained for source/API compatibility; the daily file is the
    // all-history sink. Per-session callers should use GetSessionLogFileName.
    public static string GetLogFileName(string prefix, DateTimeOffset timestamp)
        => GetAllHistoryLogFileName(prefix, timestamp);

    public static string GetSessionLogFileName(string prefix, DateTimeOffset timestamp, string sessionId, int processId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var safeSessionId = string.Concat(sessionId.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        return $"{prefix}-current-{timestamp:yyyyMMdd-HHmmss}-{processId}-{safeSessionId}.log";
    }

    /// <summary>Replaces credentials before any diagnostic text is written.</summary>
    public void SetSecrets(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        lock (gate)
        {
            secrets.Clear();
            secrets.AddRange(values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(value => value.Length));
        }
    }

    public string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        lock (gate)
        {
            foreach (var secret in secrets)
            {
                text = text.Replace(secret, "[redacted]", StringComparison.Ordinal);
                text = text.Replace(Uri.EscapeDataString(secret), "[redacted]", StringComparison.Ordinal);

                // JSON logs contain escaped string values. This also protects keys
                // containing characters such as a quote or a line break.
                var escaped = JsonConvert.ToString(secret);
                if (escaped.Length >= 2)
                    text = text.Replace(escaped[1..^1], "[redacted]", StringComparison.Ordinal);
            }
            return text;
        }
    }

    public void Write(string level, string eventName, string? message = null, JObject? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(level);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        var record = new JObject
        {
            ["timestampUtc"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ["level"] = level,
            ["event"] = eventName,
            ["processId"] = Environment.ProcessId,
            ["sessionId"] = SessionId,
            ["applicationVersion"] = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown"
        };
        if (message != null) record["message"] = message;
        if (data != null) record["data"] = data.DeepClone();

        var line = Redact(record.ToString(Formatting.None));
        lock (gate)
        {
            Append(AllLogFilePath, line, AllFallbackLogFilePath);
            if (!string.Equals(AllLogFilePath, CurrentLogFilePath, StringComparison.OrdinalIgnoreCase))
                Append(CurrentLogFilePath, line, CurrentFallbackLogFilePath);
        }
    }

    public void WriteException(string eventName, Exception exception, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var data = new JObject
        {
            ["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name,
            ["exception"] = exception.ToString()
        };
        Write("error", eventName, message ?? exception.Message, data);
    }

    public string? ReadCurrentTail(int maximumCharacters = MaximumTailCharacters)
    {
        lock (gate) return ReadTailFile(CurrentLogFilePath, maximumCharacters);
    }

    /// <summary>Reads the complete current application-session log from startup to now.</summary>
    public string? ReadCurrentLog()
    {
        lock (gate) return ReadFile(CurrentLogFilePath);
    }

    public static string? ReadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public static string? ReadTailFile(string path, int maximumCharacters = MaximumTailCharacters)
    {
        if (string.IsNullOrWhiteSpace(path) || maximumCharacters <= 0 || !File.Exists(path)) return null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var start = Math.Max(0, stream.Length - maximumCharacters * 4L);
            stream.Position = start;
            using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
            var text = reader.ReadToEnd();
            return text.Length <= maximumCharacters ? text : text[^maximumCharacters..];
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private void Append(string path, string line, string fallbackPath)
    {
        lock (gate)
        {
            if (TryAppend(path, line)) return;

            // A locked or unavailable LocalAppData directory should not make
            // diagnostics disappear. The fallback is also visible to the caller
            // through the standard error stream during a crash.
            if (TryAppend(fallbackPath, line)) return;

            if (writeFailureReported) return;
            writeFailureReported = true;
            try { Console.Error.WriteLine("TopSolid AI diagnostic log could not be written."); }
            catch { /* A failing crash handler must never throw again. */ }
        }
    }

    private static bool TryAppend(string path, string line)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory)) return false;
            Directory.CreateDirectory(directory);
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite,
                4096, FileOptions.WriteThrough);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.WriteLine(line);
            writer.Flush();
            stream.Flush(flushToDisk: true);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (ArgumentException) { return false; }
    }
}
