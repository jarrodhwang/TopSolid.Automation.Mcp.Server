using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Preview;

namespace TopSolid.Automation.Tests;

internal static class GraphicPreviewUiTests
{
    private sealed class Client : IGraphicPreviewClient
    {
        internal int Calls;
        internal Func<JObject, CancellationToken, Task<JObject>>? Handler;
        public Task<JObject> GetGraphicPreviewAsync(JObject target, CancellationToken token)
        { Calls++; return Handler?.Invoke(target, token) ?? Task.FromResult(GraphicPreviewTests.Result((string)target["documentId"]!)); }
    }

    internal static async Task Run(Window owner, Action<Window, string> render)
    {
        var originalLanguage = StudioStrings.CurrentLanguage; var originalTheme = TopSolidTheme.Current;
        try
        {
            foreach (var dark in new[] { false, true })
            {
                StudioStrings.Apply(dark ? "ko" : "en"); TopSolidTheme.Apply(new(dark, "Preview fixture", "UI tests"));
                var client = new Client(); var proposal = GraphicPreviewTests.CylinderProposal(); var snapshot = proposal.DeepClone();
                var dialog = Position(new ChangeConfirmationWindow(proposal, previewClient: client), owner);
                try
                {
                    dialog.Show(); var pane = Descendants<GraphicPreviewPane>(dialog).Single(); await Until(() => pane.Scene != null, dialog);
                    Check.True(pane.ActualWidth > 360 && pane.ActualHeight > 250, "3D viewport is clipped");
                    Check.True(!pane.IsLoading && client.Calls == 0, "Proposed geometry performed an unnecessary CAD export");
                    var before = pane.Camera.Position; pane.Orbit(.25, .1); Check.True(pane.Camera.Position != before, "Orbit did not move camera");
                    var width = pane.Camera.Width; pane.Zoom(.7); Check.True(pane.Camera.Width < width, "Zoom did not change camera"); pane.SetView("iso"); pane.Fit();
                    Check.True(pane.Scene!.Surfaces.IsFrozen && pane.Scene.Triangles == 384, "Proposed geometry was not rendered");
                    Check.True(JToken.DeepEquals(snapshot, proposal), "Viewport changed approval facts or token");
                    Check.True(!dialog.ProposalBox.Text.Contains("7899") && !dialog.ProposalBox.Text.Contains("fixture-part"), "User mode exposed graphical target IDs");
                    await Layout(dialog); render(dialog, "graphic-approval-" + (dark ? "dark-ko" : "light-en") + ".png");
                    var modes = Descendants<ComboBox>(pane).First(c => c.Items.Count == 2); modes.SelectedIndex = 1;
                    await Until(() => !pane.IsLoading && client.Calls > 0 && pane.Scene != null, dialog);
                    Check.Equal(12, pane.Scene!.Triangles, "Current document export did not replace proposed geometry");
                    dialog.Width = 620; await Layout(dialog);
                    Check.True(pane.ActualHeight >= 230 && dialog.ApproveButton.IsVisible, "Narrow review layout lost preview/actions");
                    render(dialog, "graphic-approval-narrow-" + (dark ? "dark" : "light") + ".png");
                }
                finally { dialog.Close(); }
            }
            StudioStrings.Apply("en"); TopSolidTheme.Apply(new(false, "Light", "Preview fixture"));
            var choices = new[] { new QuestionChoice("a", "Mounting plate", "Fixture assembly", "document", "Mounting plate"), new QuestionChoice("b", "Cutting tool", "Milling operation", "document", "Cutting tool") };
            var values = choices.ToDictionary(c => c.Key, c => new JObject { ["sourceTool"] = "topsolid_list_documents", ["sourceArguments"] = new JObject(), ["value"] = new JObject { ["documentId"] = c.Key, ["name"] = c.Label } });
            var question = new UserQuestion("Choose the part to machine", "select", "document", false, "", null, null, choices, values);
            var questionClient = new Client(); var questionWindow = Position(new QuestionWindow(question, questionClient), owner);
            try
            {
                questionWindow.Show(); await Layout(questionWindow); var pane = Descendants<GraphicPreviewPane>(questionWindow).Single();
                Check.True(questionClient.Calls == 0 && pane.Scene == null, "Question auto-selected or exported a document");
                var list = (ListBox)questionWindow.FindName("ChoiceList"); list.SelectedIndex = 0;
                await Until(() => pane.Scene != null, questionWindow); render(questionWindow, "graphic-question-document.png");
                var first = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
                questionClient.Handler = (target, _) => (string?)target["documentId"] == "b" ? first.Task : Task.FromResult(GraphicPreviewTests.Result("a"));
                list.SelectedIndex = 1; await Until(() => questionClient.Calls >= 2, questionWindow);
                list.SelectedIndex = 0; await Until(() => questionClient.Calls >= 3 && pane.Scene != null, questionWindow);
                first.SetResult(new JObject { ["status"] = "unavailable" }); await Layout(questionWindow);
                Check.True(pane.Scene != null && !pane.IsLoading, "Stale selection replaced the current geometry");
                ((Button)questionWindow.FindName("ClearSelection")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check.True(pane.Scene == null && !pane.IsLoading, "Cleared selection retained unrelated geometry");
            }
            finally { questionWindow.Close(); }
            var bad = new Client { Handler = (_, _) => Task.FromResult(new JObject { ["status"] = "tooLarge" }) };
            var failed = Position(new ChangeConfirmationWindow(JObject.Parse("{toolName:'topsolid_set_cam_parameter_value',target:{documentId:'doc',name:'Face milling'},arguments:{}}"), previewClient: bad), owner);
            try
            {
                failed.Show(); var pane = Descendants<GraphicPreviewPane>(failed).Single(); await Until(() => bad.Calls > 0 && !pane.IsLoading, failed);
                Check.True(pane.Scene == null && pane.StatusText.Contains("too large") && failed.ApproveButton.IsEnabled, "Missing preview blocked independent approval");
                render(failed, "graphic-preview-too-large.png");
            }
            finally { failed.Close(); }
        }
        finally { StudioStrings.Apply(originalLanguage); TopSolidTheme.Apply(originalTheme); }
    }
    private static T Position<T>(T dialog, Window owner) where T : Window
    { dialog.Owner = owner; dialog.ShowInTaskbar = false; dialog.ShowActivated = false; dialog.WindowStartupLocation = WindowStartupLocation.Manual; dialog.Left = -20000; dialog.Top = -20000; return dialog; }
    private static async Task Until(Func<bool> condition, Window window)
    { var watch = Stopwatch.StartNew(); while (!condition()) { if (watch.Elapsed > TimeSpan.FromSeconds(8)) throw new TimeoutException("Graphic UI did not settle"); await Layout(window); } await Layout(window); }
    private static async Task Layout(Window window)
    { window.UpdateLayout(); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); await Task.Delay(35); }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); if (child is T typed) yield return typed; foreach (var descendant in Descendants<T>(child)) yield return descendant; }
    }
}
