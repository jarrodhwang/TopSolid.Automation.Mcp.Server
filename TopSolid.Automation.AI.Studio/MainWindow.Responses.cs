using System.Windows;
using TopSolid.Automation.AI.Studio.Chat;

namespace TopSolid.Automation.AI.Studio;

public partial class MainWindow
{
    private readonly FriendlyResponsePresenter responsePresenter = new();
    private long lastPresentedChatSequence;

    private void RefreshChatPresentation()
    {
        ChatBox.Clear();
        lastPresentedChatSequence = 0;
        AppendPendingChat();
    }

    private void AppendPendingChat()
    {
        var entries = sessionLog.Snapshot().Chat;
        EmptyState.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var entry in entries.Where(e => e.Sequence > lastPresentedChatSequence))
        {
            var userAuthored = entry.Role.Equals("You", StringComparison.OrdinalIgnoreCase) || entry.Role.Equals("User", StringComparison.OrdinalIgnoreCase);
            ChatBox.AppendMessage(entry.Role, userAuthored ? entry.Text : responsePresenter.Present(
                entry.Role == "Assistant" ? CamDisplay.Text(entry.Text) : entry.Text, settings.DevMode), entry.ElapsedMilliseconds);
            lastPresentedChatSequence = entry.Sequence;
        }
    }
}
