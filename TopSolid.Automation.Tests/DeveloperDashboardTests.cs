using TopSolid.Automation.AI.Studio.Diagnostics;

namespace TopSolid.Automation.Tests;

internal static class DeveloperDashboardTests
{
    public static async Task MeasuredTurnDurationsAndReadableDetails()
    {
        var log = new SessionLog();
        log.AddChat("You", "First turn");
        log.AddTrace("Timing", "Model request 1: 90.00 s");
        log.AddChat("Assistant", "Done");
        log.AddChat("You", "Second turn");
        log.AddTrace("Timing", "Model request 1: 1.25 s");
        log.AddTrace("Timing", "Model request 2: 0,75 s");
        log.AddTrace("Timing", "Tool topsolid_create_project: 2.00 s; user confirmation: 3.25 s");
        log.AddTrace("Timing", "Chat elapsed: 8.0 s (includes confirmation time)");
        log.AddTrace("Timing", "Model request 3: NaN s");
        log.AddTrace("Tool call", "topsolid_get_status {}");
        var metrics = DeveloperTurnMetrics.From(log.Snapshot());
        Check.Equal(2.0, metrics.ModelSeconds, "Dashboard mixed previous-turn or malformed model timing into latest turn");
        Check.Equal(2.0, metrics.ToolSeconds, "Tool processing must exclude user confirmation");
        Check.Equal(3.25, metrics.ConfirmationSeconds, "Measured confirmation wait was lost");
        Check.Equal(2, metrics.ModelRequests, "Unmeasured model phase counted as completed");
        Check.Equal(1, metrics.TimedToolCalls, "Untimed tool call must not fabricate a duration");
        log.AddChat("You", "Third turn");
        metrics = DeveloperTurnMetrics.From(log.Snapshot());
        Check.True(metrics.ModelSeconds == null && metrics.ToolSeconds == null && metrics.ConfirmationSeconds == null,
            "A fresh turn must show unmeasured phases, not previous-turn durations or guessed zeros");
        log.Clear();
        Check.Equal(0L, DeveloperTurnMetrics.From(log.Snapshot()).StartSequence, "Cleared session retained a turn");

        const string originalDate = "2026-09-17T23:00:00.1234567+03:00";
        var detail = DeveloperDetailFormatter.Format("topsolid_get_status {\"when\":\"" + originalDate + "\",\"ok\":true}");
        Check.True(detail.Contains(originalDate, StringComparison.Ordinal) && detail.Contains(Environment.NewLine), "JSON detail altered date value or failed to format");
        Check.Equal("Malformed {json", DeveloperDetailFormatter.Format("Malformed {json"), "Malformed JSON must remain readable");
        Check.Equal("{\"a\":1} trailing", DeveloperDetailFormatter.Format("{\"a\":1} trailing"), "Formatting must preserve trailing source text");
        var large = DeveloperDetailFormatter.Format(new string('x', 200_000));
        Check.True(large.Length < 121_000 && large.Contains("Display truncated", StringComparison.Ordinal), "Oversized detail was not bounded or visibly identified");
        await GpuUsageTests.Run();
    }
}
