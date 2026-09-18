using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using TopSolid.Automation.AI.Studio.Appearance;

namespace TopSolid.Automation.AI.Studio;

public partial class MainWindow
{
    private bool activityAwaitingApproval;
    private string? activityKey;

    // Phases use localized product language. Tool arguments and execution IDs never enter this surface.
    private void SetActivity(string? key, string icon = "app", bool waitingForUser = false)
    {
        ActivityPanel.Visibility = key == null ? Visibility.Collapsed : Visibility.Visible;
        ActivityProgress.IsIndeterminate = key != null && !waitingForUser;
        ActivityProgress.Visibility = waitingForUser ? Visibility.Collapsed : Visibility.Visible;
        ActivityIcon.Source = TopSolidIcons.Get(icon);
        if (key == activityKey) return;
        activityKey = key;
        if (key == null) ActivityText.Text = "";
        else ActivityText.SetResourceReference(TextBlock.TextProperty, "Ui." + key);
        if (key != null && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            (UIElementAutomationPeer.FromElement(ActivityText) ?? UIElementAutomationPeer.CreatePeerForElement(ActivityText))?
                .RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void UpdateActivityFromTrace(string kind, string text, CancellationTokenSource? source)
    {
        if (source == null || !ReferenceEquals(operation, source) || closing || activityAwaitingApproval) return;
        if (source.IsCancellationRequested) { SetActivity("Activity.Cancelling", "status"); return; }
        if (kind == "Model" && text.StartsWith("Request ", StringComparison.Ordinal))
            SetActivity("Activity.Thinking");
        else if (kind == "Tool call")
        {
            var tool = text.Split(' ', 2)[0];
            if (tool.Contains("parameter", StringComparison.OrdinalIgnoreCase)) SetActivity("Activity.Parameters", "parameter");
            else if (tool.Contains("cam", StringComparison.OrdinalIgnoreCase) || tool.Contains("operation", StringComparison.OrdinalIgnoreCase)) SetActivity("Activity.Operations", "operation");
            else SetActivity("Activity.TopSolid", "document");
        }
        else if (kind is "Tool result" or "Tool error" or "CAD change")
            SetActivity("Activity.PreparingResponse");
    }
}
