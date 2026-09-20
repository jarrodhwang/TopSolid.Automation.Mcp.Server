using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
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
    internal static async Task NativeContext(Window owner, string file, Action<Window,string> render)
    {
        var originalLanguage = StudioStrings.CurrentLanguage; var originalTheme = TopSolidTheme.Current;
        StudioStrings.Apply("ko"); TopSolidTheme.Apply(new(false,"Light","Native CAM fixture"));
        var bytes = await System.IO.File.ReadAllBytesAsync(file);
        var client = new Client { Handler = (_,_) => Task.FromResult(new JObject { ["status"]="ready", ["documentId"]="native-cam", ["name"]="DMU65 CAM", ["format"]="glb", ["units"]="m", ["upAxis"]="Z", ["camContext"]=true, ["data"]=Convert.ToBase64String(bytes) }) };
        using var pane = new GraphicPreviewPane(client,new JObject { ["documentId"]="native-cam", ["operation"]=new JObject { ["documentId"]="native-cam", ["id"]=12 } });
        var window = Position(new Window { Content=pane, Width=1120, Height=850 },owner);
        TopSolidTheme.ApplyWindow(window);
        try
        {
            window.Show(); await Until(()=>pane.HasMachineContext && !pane.IsLoading,window);
            Check.True(pane.ToolpathSegments>0,"Context lost the selected operation overlay");
            var calls=client.Calls; var partTriangles=pane.Scene!.Triangles;
            Check.Equal(CamStockMode.Remaining, pane.StockMode, "Remaining stock must be the default");
            var camera = pane.Camera.Position; var width = pane.Camera.Width;
            pane.SetStockMode(CamStockMode.Part); await Layout(window);
            Check.True(pane.Scene!.Triangles < partTriangles && pane.ToolpathSegments > 0, "Part-only toggle lost the operation or retained stock");
            Check.Equal(camera, pane.Camera.Position, "Stock toggle reset the camera"); Check.Equal(width, pane.Camera.Width, "Stock toggle reset zoom");
            render(window, "cam-native-part-only.png");
            if (pane.HasOriginalStock)
            {
                pane.SetStockMode(CamStockMode.Original); await Layout(window);
                Check.Equal(CamStockMode.Original, pane.StockMode, "Original stock toggle failed");
                Check.True(pane.ToolpathSegments > 0, "Original stock toggle lost the operation overlay");
                render(window, "cam-native-original-stock.png");
            }
            pane.SetStockMode(CamStockMode.Remaining); await Layout(window);
            render(window,"cam-native-machine-hidden.png"); pane.SetMachineVisible(true); await Layout(window);
            Check.True(pane.Scene!.Triangles>partTriangles && pane.ToolpathSegments>0,"Machine toggle lost work geometry or toolpath");
            render(window,"cam-native-machine-visible.png"); pane.SetMachineVisible(false); await Layout(window);
            Check.Equal(partTriangles,pane.Scene!.Triangles,"Machine toggle did not restore work geometry");
            Check.Equal(calls,client.Calls,"Machine toggle re-exported the document");
            pane.SetTarget(new JObject { ["documentId"] = "native-cam", ["operation"] = new JObject { ["documentId"] = "native-cam", ["id"] = 13 } });
            await Until(() => !pane.IsLoading && client.PathRequests.Count == 2, window);
            Check.Equal(calls,client.Calls,"Switching operation re-exported machine/stock geometry");
            Check.Equal(CamStockMode.Remaining,pane.StockMode,"Switching operation changed stock mode");
        }
        finally { window.Close(); StudioStrings.Apply(originalLanguage); TopSolidTheme.Apply(originalTheme); }
    }
    private sealed class Client : IGraphicPreviewClient, IToolpathPreviewClient
    {
        internal int Calls;
        internal Func<JObject, CancellationToken, Task<JObject>>? Handler;
        internal readonly List<JObject> PathRequests = [];
        internal Func<JObject, CancellationToken, Task<JObject>>? PathHandler;
        public Task<JObject> GetGraphicPreviewAsync(JObject target, CancellationToken token)
        { Calls++; return Handler?.Invoke(target, token) ?? Task.FromResult(GraphicPreviewTests.Result((string)target["documentId"]!)); }
        public Task<JObject> GetToolpathPreviewAsync(JObject operation, CancellationToken token)
        {
            Check.True(operation.Properties().All(p => p.Name is "documentId" or "id"), "Toolpath request sent viewport/image parameters");
            var identity = (JObject)operation.DeepClone();
            PathRequests.Add((JObject)identity.DeepClone()); return PathHandler?.Invoke(identity, token) ?? Task.FromResult(PreviewRuntimeTests.PathResult(identity));
        }
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
                    dialog.Show(); var pane = Descendants<GraphicPreviewPane>(dialog).Single(); await Until(() => pane.IsScenePresented && !pane.IsLoading, dialog);
                    var background = (LinearGradientBrush)Application.Current.FindResource("ViewportGradientBrush");
                    Check.Equal(dark ? Color.FromRgb(61, 61, 61) : Color.FromRgb(74, 101, 151), background.GradientStops[0].Color, "Preview background differs from native TopSolid theme");
                    Check.Equal(dark ? Color.FromRgb(61, 61, 61) : Color.FromRgb(231, 228, 228), background.GradientStops[1].Color, "Preview background lower color differs from native TopSolid theme");
                    Check.True(pane.ActualWidth > 360 && pane.ActualHeight > 250, "3D viewport is clipped");
                    var cameraButton = Descendants<Button>(pane).SingleOrDefault(button =>
                        AutomationProperties.GetName(button) == StudioStrings.Get("Preview.CameraMenu"));
                    Check.True(cameraButton != null, "TopSolid-style camera menu is missing from the viewport toolbar");
                    Check.True(!Descendants<ComboBox>(pane).Any(combo => combo.Items.Count >= 7), "The old text camera-view dropdown remains in the footer");
                    cameraButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var cameraMenu = cameraButton.ContextMenu;
                    Check.True(cameraMenu != null && cameraMenu.IsOpen, "Camera button did not open its view menu");
                    Check.Equal(8, cameraMenu!.Items.Count, "Camera menu lost one of the standard TopSolid view directions");
                    Check.Equal("Ctrl+Shift+T", ((MenuItem)cameraMenu.Items[0]).InputGestureText, "Top camera shortcut is missing");
                    Check.Equal(StudioStrings.Get("Preview.View.perspective"), ((MenuItem)cameraMenu.Items[7]).Header?.ToString(), "Perspective camera entry is missing");
                    cameraMenu.IsOpen = false;
                    pane.SetView("top"); Check.True(pane.Camera.LookDirection.Z < 0, "Top camera direction is inverted");
                    pane.SetView("bottom"); Check.True(pane.Camera.LookDirection.Z > 0, "Bottom camera direction is inverted");
                    pane.SetView("front"); Check.True(pane.Camera.LookDirection.Y > 0, "Front camera direction is inverted");
                    pane.SetView("back"); Check.True(pane.Camera.LookDirection.Y < 0, "Back camera direction is inverted");
                    pane.SetView("perspective"); Check.True(pane.IsPerspective, "Perspective camera entry did not switch projection");
                    pane.SetView("iso"); Check.True(!pane.IsPerspective, "Selecting an orthographic view did not leave perspective mode");
                    Check.True(!pane.IsLoading && client.Calls == 0, "Proposed geometry performed an unnecessary CAD export");
                    var before = pane.Camera.Position; pane.Orbit(.25, .1); Check.True(pane.Camera.Position != before, "Orbit did not move camera");
                    var width = pane.Camera.Width; pane.Zoom(.7); Check.True(pane.Camera.Width < width, "Zoom did not change camera"); pane.SetView("iso"); pane.Fit();
                    Check.True(pane.Scene!.Surfaces.IsFrozen && pane.Scene.Triangles == 288, "Proposed geometry was not rendered");
                    Navigation(pane);
                    Check.True(JToken.DeepEquals(snapshot, proposal), "Viewport changed approval facts or token");
                    Check.True(!dialog.ProposalBox.Text.Contains("7899") && !dialog.ProposalBox.Text.Contains("fixture-part"), "User mode exposed graphical target IDs");
                    await Layout(dialog); render(dialog, "graphic-approval-" + (dark ? "dark-ko" : "light-en") + ".png");
                    var modes = Descendants<ComboBox>(pane).First(c => c.Items.Count == 2); modes.SelectedIndex = 1;
                    await Until(() => !pane.IsLoading && client.Calls > 0 && pane.Scene != null, dialog);
                    Check.Equal(12, pane.Scene!.Triangles, "Current document export did not replace proposed geometry");
                    client.Handler = (_, _) => Task.FromResult(new JObject { ["status"] = "ready", ["documentId"] = "fixture-part", ["name"] = "Mounting plate",
                        ["format"] = "stl", ["units"] = "mm", ["upAxis"] = "Z", ["linearToleranceMm"] = .05, ["angularToleranceDegrees"] = 5,
                        ["data"] = Convert.ToBase64String(GraphicPreviewTests.StlFixture()) });
                    await pane.ReloadAsync();
                    Check.True(pane.Scene?.Triangles == 12 && pane.Scene.Bounds.SizeZ == 20, "Native precise STL did not reach the viewport");
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
                await Until(() => pane.IsScenePresented && !pane.IsLoading, questionWindow); render(questionWindow, "graphic-question-document.png");
                var first = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
                questionClient.Handler = (target, _) => (string?)target["documentId"] == "b" ? first.Task : Task.FromResult(GraphicPreviewTests.Result("a"));
                list.SelectedIndex = 1; await Until(() => questionClient.Calls >= 2, questionWindow);
                Check.True(pane.IsLoading && !pane.IsScenePresented, "Loading a new selection exposed partial/stale geometry");
                list.SelectedIndex = 0; await Until(() => questionClient.Calls >= 3 && pane.Scene != null, questionWindow);
                first.SetResult(new JObject { ["status"] = "unavailable" }); await Layout(questionWindow);
                Check.True(pane.Scene != null && !pane.IsLoading, "Stale selection replaced the current geometry");
                ((Button)questionWindow.FindName("ClearSelection")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check.True(pane.Scene == null && !pane.IsLoading, "Cleared selection retained unrelated geometry");
                list.SelectedIndex = 0;
                questionWindow.Width = 500; questionWindow.Height = 600; await Layout(questionWindow);
                Check.True(list.IsVisible && list.ActualHeight > 100, "Narrow preview hides the question choices");
                var narrowTabs = Descendants<TabControl>(questionWindow).Single();
                Check.True(((Button)questionWindow.FindName("ContinueQuestion")).IsVisible, "Narrow question lost its shared action row");
                render(questionWindow, "graphic-question-narrow-selection.png");
                narrowTabs.SelectedIndex = 1; await Until(() => pane.IsVisible && pane.Scene != null, questionWindow);
                Check.True(pane.ActualHeight > 250, "Narrow preview tab clips its viewport");
                render(questionWindow, "graphic-question-narrow-preview.png");
                narrowTabs.SelectedIndex = 0; await Layout(questionWindow);
                Check.Equal(0, list.SelectedIndex, "Switching preview tabs lost the exact selection");
                questionWindow.Width = 1080; await Layout(questionWindow);
                Check.True(list.IsVisible && pane.IsVisible && !narrowTabs.IsVisible, "Wide layout did not restore side-by-side review");
            }
            finally { questionWindow.Close(); }
            await OperationBrowser(owner, render);
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
    internal static async Task OperationBrowser(Window owner, Action<Window, string> render)
    {
        var language = StudioStrings.CurrentLanguage;
        var theme = TopSolidTheme.Current;
        StudioStrings.Apply("ko");
        TopSolidTheme.Apply(new(false, "Light", "Operation card fixture"));
        var rows = new JArray(Enumerable.Range(1, 4).Select(CamSelectionTests.Row));
        ((JObject)rows[0])["operationName"] = "[1: 환경 활성 / 환경 비활성화]";
        foreach (var index in new[] { 1, 2, 3 })
        {
            var row = (JObject)rows[index];
            row["operationName"] = $"[{index + 1}: 멀티-블레이드 {(index == 3 ? "허브 정삭" : "황삭")} (5X)]";
            row["operationType"] = "TopSolid.Cam.NC.MillTurn.FiveAxis.DB.MultiBlade." + (index == 3 ? "MultiBladeHubFinishingOperation" : "MultiBladeRoughingOperation");
            row["toolPocket"] = index == 1 ? "T 2" : index == 2 ? "T 3" : "T 6";
            row["toolDefinitionName"] = index == 1 ? "Conic Nose Ball Mill D 10 d 3,81 A3 L60" : index == 2 ? "Conic Nose Ball Mill D 6 d 2,91 A2 L60" : "Ball Nose Mill D2 L5 SD4";
            row["toolFunction"] = "BallNoseMill";
        }
        var sources = new QuestionSources();
        sources.Capture("operation-layout", "topsolid_list_cam_operation_summaries", new JObject(), new TopSolid.Automation.Mcp.Contracts.McpToolResult { StructuredContent = new JObject { ["items"] = rows } });
        var source = sources.Create(JObject.Parse("{question:'가공 작업',kind:'select',itemKind:'operation',sources:[{toolCallId:'operation-layout',path:'/items'}]}"));
        var question = UserQuestion.Browse([source], source.Choices.Count, false, null);
        var client = new Client(); var dialog = Position(new QuestionWindow(question, client), owner);
        try
        {
            dialog.Show(); var pane = Descendants<GraphicPreviewPane>(dialog).Single();
            await Until(() => pane.Scene != null && !pane.IsLoading, dialog);
            Check.True(client.Calls == 1 && client.PathRequests.Count == 0, "Operation browser did not show its document context first");
            Check.True(((Button)dialog.FindName("ContinueQuestion")).Visibility == Visibility.Collapsed, "Read-only list asked for approval");
            var list = (ListBox)dialog.FindName("ChoiceList"); list.SelectedIndex = 1;
            await Until(() => pane.ToolpathSegments == 2 && !pane.IsLoading, dialog);
            Check.True(JToken.DeepEquals(client.PathRequests[^1], question.PreviewTargetFor(question.Choices[1].Key)!["operation"]), "Toolpath used a part/tool handle instead of the selected operation");
            pane.Orbit(.2, .1); var camera = pane.Camera.Position; var scene = pane.Scene;
            render(dialog, "graphic-operation-list-toolpath-ko.png");
            var delayed = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            JObject? pendingOperation = null;
            client.PathHandler = (op, _) => { pendingOperation = op; return delayed.Task; };
            list.SelectedIndex = 0;
            Check.Equal(0, pane.ToolpathSegments, "Switching operations retained the previous path");
            await Until(() => pendingOperation != null, dialog);
            client.PathHandler = (op, _) => Task.FromResult(new JObject { ["operation"] = op.DeepClone(), ["status"] = "coordinatesUnavailable" });
            list.SelectedIndex = 1;
            await Until(() => !pane.IsLoading, dialog);
            delayed.SetResult(PreviewRuntimeTests.PathResult(pendingOperation!)); await Layout(dialog);
            Check.True(pane.ToolpathSegments == 0 && pane.ToolpathStatus.Contains("좌표"), "Stale path replaced a newer coordinate failure");
            Check.True(client.Calls == 1 && ReferenceEquals(scene, pane.Scene) && pane.Camera.Position == camera, "Operation selection re-exported the model or reset the camera");
            client.PathHandler = (op, _) => Task.FromResult(new JObject { ["status"] = "ready", ["operation"] = op.DeepClone(),
                ["format"] = "native-view-png", ["stateRestored"] = true, ["upToDate"] = true,
                ["data"] = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aLxkAAAAASUVORK5CYII=" });
            list.SelectedIndex = 0;
            await Until(() => !pane.IsLoading, dialog);
            Check.True(pane.ToolpathSegments == 0 && ReferenceEquals(scene, pane.Scene) &&
                pane.ToolpathStatus == StudioStrings.Get("Preview.PathUnavailable"), "Screenshot response replaced geometry or was silently accepted");
            var requests = client.PathRequests.Count;
            pane.Orbit(.15, .05); pane.Zoom(.9);
            await Task.Delay(450); await Layout(dialog);
            Check.True(client.PathRequests.Count == requests && pane.Camera.Position != camera, "Local camera requested another toolpath scan");
            client.PathHandler = (op, _) => Task.FromResult(PreviewRuntimeTests.PathResult(op));
            list.SelectedIndex = 1; await Until(() => pane.ToolpathSegments == 2 && !pane.IsLoading, dialog);
            requests = client.PathRequests.Count;
            pane.SetView("top"); pane.Zoom(.8); await Task.Delay(450); await Layout(dialog);
            Check.True(pane.ToolpathSegments == 2 && client.PathRequests.Count == requests && client.Calls == 1 && ReferenceEquals(scene, pane.Scene),
                "Navigating native geometry lost the path or called the backend");
            TopSolidTheme.Apply(new(true, "Dark", "Operation card fixture")); await Layout(dialog);
            render(dialog, "operation-list-dark.png");
            dialog.Width = 500; await Layout(dialog);
            render(dialog, "operation-list-narrow-dark.png");
            TopSolidTheme.Apply(new(false, "Light", "Operation card fixture")); await Layout(dialog);
            render(dialog, "operation-list-narrow-light.png");
            ((System.Windows.Controls.Primitives.ToggleButton)dialog.FindName("GroupByTool")).IsChecked = true; await Layout(dialog);
            render(dialog, "operation-list-grouped-light.png");
        }
        finally { dialog.Close(); StudioStrings.Apply(language); TopSolidTheme.Apply(theme); }
    }
    private static void Navigation(GraphicPreviewPane pane)
    {
        var original = pane.Camera.Position;
        Check.True(!pane.BeginDrag(MouseButton.Left, ModifierKeys.None, new Point()), "Left drag captured the view");
        pane.MoveDrag(new Point(30, 20)); Check.Equal(original, pane.Camera.Position, "Left drag moved the view");
        foreach (var input in new[] { (MouseButton.Right, ModifierKeys.Control), (MouseButton.Middle, ModifierKeys.None) })
        {
            var direction = pane.Camera.LookDirection;
            Check.True(pane.BeginDrag(input.Item1, input.Item2, new Point()), "TopSolid rotation did not start");
            Check.True(!pane.BeginDrag(MouseButton.Left, ModifierKeys.None, new Point(30, 20)), "Second mouse button changed the active gesture");
            Check.True(!pane.EndDrag(MouseButton.Left), "Unrelated button release ended the drag");
            pane.MoveDrag(new Point(30, 20)); Check.True(pane.Camera.LookDirection != direction, "TopSolid rotation did not rotate");
            pane.EndDrag(input.Item1); var stopped = pane.Camera.Position; pane.MoveDrag(new Point(60, 50));
            Check.Equal(stopped, pane.Camera.Position, "Camera kept moving after release");
        }
        var beforePan = pane.Camera.Position; var panDirection = pane.Camera.LookDirection;
        pane.BeginDrag(MouseButton.Right, ModifierKeys.None, new Point()); pane.MoveDrag(new Point(25, 30));
        Check.True(pane.Camera.Position != beforePan && pane.Camera.LookDirection == panDirection, "Right drag rotated instead of panning");
        pane.CancelDrag(); var cancelled = pane.Camera.Position; pane.MoveDrag(new Point(50, 60));
        Check.Equal(cancelled, pane.Camera.Position, "Lost mouse capture retained an active drag");
        pane.SetView("iso"); pane.Fit();
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
