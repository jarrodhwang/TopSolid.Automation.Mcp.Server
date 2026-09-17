namespace TopSolid.Automation.AI.Studio.Diagnostics;

public sealed record SessionChatEntry(long Sequence, DateTimeOffset TimestampUtc, string Role, string Text, double? ElapsedMilliseconds = null);

public sealed record SessionTraceEntry(long Sequence, DateTimeOffset TimestampUtc, string Kind, string Text);

public sealed class SessionLogSnapshot
{
    public IReadOnlyList<SessionChatEntry> Chat { get; init; } = [];
    public IReadOnlyList<SessionTraceEntry> Trace { get; init; } = [];
}

/// <summary>Bounded in-memory copy of what the operator can export from the current window.</summary>
public sealed class SessionLog
{
    private const int MaximumEntries = 2_000;
    private const int MaximumCharacters = 600_000;
    private readonly object gate = new();
    private readonly List<SessionChatEntry> chat = [];
    private readonly List<SessionTraceEntry> trace = [];
    private long nextSequence;
    private int characters;

    public void AddChat(string role, string text, double? elapsedMilliseconds = null)
    {
        lock (gate)
        {
            chat.Add(new SessionChatEntry(++nextSequence, DateTimeOffset.UtcNow, role, text, elapsedMilliseconds));
            characters += role.Length + text.Length;
            Trim();
        }
    }

    public void AddTrace(string kind, string text)
    {
        lock (gate)
        {
            trace.Add(new SessionTraceEntry(++nextSequence, DateTimeOffset.UtcNow, kind, text));
            characters += kind.Length + text.Length;
            Trim();
        }
    }

    public SessionLogSnapshot Snapshot()
    {
        lock (gate)
        {
            return new SessionLogSnapshot
            {
                Chat = chat.ToArray(),
                Trace = trace.ToArray()
            };
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            chat.Clear();
            trace.Clear();
            characters = 0;
        }
    }

    private void Trim()
    {
        while ((chat.Count + trace.Count > MaximumEntries || characters > MaximumCharacters) &&
               (chat.Count > 0 || trace.Count > 0))
        {
            var oldestChat = chat.Count == 0 ? long.MaxValue : chat[0].Sequence;
            var oldestTrace = trace.Count == 0 ? long.MaxValue : trace[0].Sequence;
            if (oldestChat <= oldestTrace)
            {
                characters -= chat[0].Role.Length + chat[0].Text.Length;
                chat.RemoveAt(0);
            }
            else
            {
                characters -= trace[0].Kind.Length + trace[0].Text.Length;
                trace.RemoveAt(0);
            }
        }
    }
}
