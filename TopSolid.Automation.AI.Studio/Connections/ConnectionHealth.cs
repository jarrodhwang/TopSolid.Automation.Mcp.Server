using System.Net;
using System.Net.Http;
using TopSolid.Automation.AI.Studio.AI;

namespace TopSolid.Automation.AI.Studio.Connections;

public enum ConnectionSeverity { Ready, Pending, Warning, Error }
public sealed record ConnectionCondition(string Component, ConnectionSeverity Severity, string MessageKey, DateTimeOffset? PendingSince = null);

/// <summary>UI-owned status facts. Only message keys cross into the user-facing dialog.</summary>
public sealed class ConnectionHealth
{
    private readonly Dictionary<string, ConnectionCondition> conditions = new();
    public event Action? Changed;
    public IReadOnlyList<ConnectionCondition> Conditions => conditions.Values.ToArray();
    public ConnectionSeverity Severity => conditions.Count == 0 ? ConnectionSeverity.Pending : conditions.Values.Max(c => c.Severity);
    public bool HasProblems => Severity >= ConnectionSeverity.Warning;
    public void Set(string component, ConnectionSeverity severity, string key)
    {
        var next = new ConnectionCondition(component, severity, key);
        if (conditions.GetValueOrDefault(component) == next) return;
        conditions[component] = next; Changed?.Invoke();
    }
    public void Pending(string component, string key, DateTimeOffset? started = null)
    {
        if (conditions.GetValueOrDefault(component)?.PendingSince is { } previous && (started == null || started == previous)) return;
        conditions[component] = new(component, ConnectionSeverity.Pending, key, started ?? DateTimeOffset.UtcNow);
        Changed?.Invoke();
    }
    public void Remove(string component) { if (conditions.Remove(component)) Changed?.Invoke(); }
    public void FinishPending(string component)
    {
        if (conditions.GetValueOrDefault(component)?.PendingSince != null) Remove(component);
    }
    public void Tick(DateTimeOffset now, TimeSpan slowAfter)
    {
        var changed = false;
        foreach (var entry in conditions.Values.ToArray())
            if (entry.PendingSince is { } since && now - since >= slowAfter && entry.Severity == ConnectionSeverity.Pending)
            {
                conditions[entry.Component] = entry with { Severity = ConnectionSeverity.Warning,
                    MessageKey = entry.Component switch { "Mcp" => "Health.McpSlow", "TopSolid" => "Health.TopSolidStarting", _ => "Health.AiSlow" } };
                changed = true;
            }
        if (changed) Changed?.Invoke();
    }
    public void CancelPending()
    {
        foreach (var entry in conditions.Values.Where(c => c.PendingSince != null || c.Severity == ConnectionSeverity.Pending).ToArray())
            Set(entry.Component, ConnectionSeverity.Warning, "Health.Cancelled");
    }
    public static (ConnectionSeverity Severity, string Key) AiFailure(Exception error) => error switch
    {
        AiProviderException { StatusCode: HttpStatusCode.TooManyRequests } => (ConnectionSeverity.Warning, "Health.AiLimit"),
        AiProviderException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } => (ConnectionSeverity.Error, "Health.AiAuthentication"),
        AiProviderException { StatusCode: HttpStatusCode.NotFound } => (ConnectionSeverity.Error, "Health.AiUnavailable"),
        AiProviderException e when e.StatusCode is HttpStatusCode.RequestTimeout || (int?)e.StatusCode >= 500 => (ConnectionSeverity.Warning, "Health.AiTemporary"),
        TimeoutException or OperationCanceledException => (ConnectionSeverity.Warning, "Health.AiSlow"),
        HttpRequestException => (ConnectionSeverity.Warning, "Health.AiNetwork"),
        ArgumentException => (ConnectionSeverity.Error, "Health.AiConfiguration"),
        _ => (ConnectionSeverity.Error, "Health.AiFailure")
    };
}
