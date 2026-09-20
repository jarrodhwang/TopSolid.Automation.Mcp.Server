using System.IO;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Preview;

namespace TopSolid.Automation.Tests;

internal static partial class UiShellTests
{
    internal static bool CamAutomationOnly;
    private static async Task VerifyCamAutomationUi(string output)
    {
        foreach (var language in new[] { "en", "ko", "fr", "ja", "es", "pt" }) foreach (var dark in new[] { false, true })
        {
            StudioStrings.Apply(language); TopSolidTheme.Apply(new(dark, dark ? "Dark" : "Classic", "CAM automation UI fixture"));
            var client = new CamPreviewFixture(); var plan = CamAutomationTests.Plan();
            plan.DocumentName = "CAM fixture · Mold plate"; plan.WorkpieceName = "Mold plate"; plan.MachiningStageName = "Machining 1";
            using var review = new CamAutomationReviewWindow(plan, [CamAutomationTests.Method()], client) { Left = -20000, Top = -20000, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
            review.Show();
            var preview = (GraphicPreviewPane)Field(review, "preview")!;
            for (int i = 0; i < 60 && preview.Scene == null; i++) { await Task.Delay(30); await Layout(review); }
            Check.True(preview.Scene != null, "CAM face preview did not load: " + ((TextBlock)Field(review, "status")!).Text);
            Check.Equal(1, client.Reads, "Review reloaded native geometry unexpectedly");
            Check.Equal((byte)191, preview.Scene!.GpuMeshes[0].Color.G, "Proposed face RGB missing");
            var original = (CheckBox)Field(review, "original")!; original.IsChecked = true; await Layout(review);
            Check.Equal((byte)192, preview.Scene.GpuMeshes[0].Color.R, "Original comparison did not restore display color");
            original.IsChecked = false; await Layout(review); Check.Equal(1, client.Reads, "Local appearance toggle called TopSolid");
            var grid = (DataGrid)Field(review, "processes")!;
            var process = (CamAutomationReviewWindow.ProcessRow)grid.Items[0]; process.Include = false; await Layout(review);
            Check.Equal((byte)192, preview.Scene.GpuMeshes[0].Color.R, "Excluded process still paints targets"); process.Include = true;
            Check.True(original.Focusable && grid.Focusable, "CAM review is not keyboard accessible");
            if (language is "ko" or "en") Render(review, Path.Combine(output, $"cam-automation-review-{language}-{(dark ? "dark" : "light")}.png"));
            review.Width = review.MinWidth; await Layout(review);
            Check.True(review.ActualWidth >= review.MinWidth, "Review minimum layout"); review.Close();
            var execution = new CamMethodExecutionWindow(plan) { Left = -20000, Top = -20000, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
            execution.Show(); await Layout(execution);
            if (language == "ko") Render(execution, Path.Combine(output, $"cam-automation-confirm-{(dark ? "dark" : "light")}.png")); execution.Close();
            var catalog = new CamMethodCatalogWindow([CamAutomationTests.Method()], new CamAutomationTests.Client()) { Left = -20000, Top = -20000, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
            catalog.Show(); await Layout(catalog); if (language == "ko") Render(catalog, Path.Combine(output, $"cam-automation-catalog-{(dark ? "dark" : "light")}.png")); catalog.Close();
            var editor = new CamMethodEditorWindow(CamAutomationTests.Method(), [], new CamAutomationTests.Client()) { Left = -20000, Top = -20000, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
            editor.Show(); await Layout(editor);
            if (language == "ko") Render(editor, Path.Combine(output, $"cam-automation-registration-{(dark ? "dark" : "light")}.png")); editor.Close();
            var chooser = new CamMethodBrowserWindow(new CamMethodBrowserTests.Client()) { Left = -20000, Top = -20000, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
            chooser.Show(); await Layout(chooser);
            var tree = (TreeView)Field(chooser, "tree")!;
            Check.Equal(2, tree.Items.Count, "PDM explorer must show both projects and libraries");
            foreach (TreeViewItem source in tree.Items) {
                source.IsExpanded = true; await Layout(chooser);
                var project = (TreeViewItem)source.Items[0]; project.IsExpanded = true; await Layout(chooser);
                var folder = (TreeViewItem)project.Items[0]; folder.IsExpanded = true; await Layout(chooser);
                var leaf = (TreeViewItem)folder.Items[0]; leaf.IsSelected = true; await Layout(chooser);
                Check.True(((Button)Field(chooser, "choose")!).IsEnabled, "CAM method leaf is not selectable");
                Check.True(leaf.Header is StackPanel header && header.Children[0] is Image { Source: not null }, "PDM tree icon missing");
                project.IsSelected = true; Check.True(!((Button)Field(chooser, "choose")!).IsEnabled, "Project selected as method");
                ((Button)project.Items[project.Items.Count - 1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Layout(chooser);
                Check.Equal(2, project.Items.Count, "PDM tree load more lost documents");
            }
            if (language is "ko" or "en") Render(chooser, Path.Combine(output, $"cam-pdm-explorer-{language}-{(dark ? "dark" : "light")}.png"));
            chooser.Width = chooser.MinWidth; await Layout(chooser); chooser.Close();
        }
        var overrides = CamAutomationTests.Plan(); var overrideRow = (JObject)overrides.Geometry[0]!;
        overrideRow["color"] = JObject.Parse("{r:18,g:52,b:86}");
        var cached = await CamFaceGeometry.Load(new CamPreviewFixture(overrideRow), overrides, CancellationToken.None);
        var colors = overrides.Steps[0].Colors;
        var faceKey = colors.Assignments[0].TargetKey;
        colors.Assignments[0].TargetKey = new JObject { ["element"] = overrideRow["target"]!["face"]!["element"]!.DeepClone() }.ToString(Newtonsoft.Json.Formatting.None);
        Check.Equal((byte)18, cached.Scene(colors, false).GpuMeshes[0].Color.R, "Whole-shape preview erased an existing native face override");
        colors.Assignments[0].TargetKey = faceKey;
        Check.Equal((byte)191, cached.Scene(colors, false).GpuMeshes[0].Color.G, "Explicit face assignment failed to override the original color");
        var uncertain = CamAutomationTests.Plan(); uncertain.Steps.Clear(); uncertain.Geometry[0]!["proposalReason"] = "Insufficient evidence for machining role.";
        using (var uncertainReview = new CamAutomationReviewWindow(uncertain, [CamAutomationTests.Method()], new CamPreviewFixture()) { Left = -20000, Top = -20000, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual }) {
            uncertainReview.Show(); var preview = (GraphicPreviewPane)Field(uncertainReview, "preview")!;
            for (int i = 0; i < 60 && preview.Scene == null; i++) { await Task.Delay(30); await Layout(uncertainReview); }
            var targets = (DataGrid)Field(uncertainReview, "targets")!;
            Check.True(targets.Items.Count == 1 && targets.IsReadOnly, "Unassigned geometry disappeared when no process could be proposed");
            Check.Equal("Insufficient evidence for machining role.", ((CamColorReviewWindow.Row)targets.Items[0]).Reason, "Uncertainty reason was hidden"); uncertainReview.Close();
        }
        StudioStrings.Apply("en"); Console.WriteLine("PASS CAM automation UI: native-ID fixture preview, local RGB changes, original comparison, process exclusion, themes, languages and dialogs.");
    }
    private sealed class CamPreviewFixture : IGraphicPreviewClient
    {
        private readonly JObject row;
        internal CamPreviewFixture(JObject? row = null) { this.row = row ?? CamAutomationTests.Geometry(); }
        internal int Reads;
        public Task<JObject> GetGraphicPreviewAsync(JObject target, CancellationToken token) => throw new InvalidOperationException("Use private preview data transfer");
        public Task<GraphicPreviewData> GetGraphicPreviewDataAsync(JObject target, CancellationToken token)
        {
            Reads++; Check.True((bool?)target["camFaceGeometry"] == true, "Review requested an unverified preview format");
            return Task.FromResult(new GraphicPreviewData(new JObject { ["status"] = "ready", ["format"] = "cam-faces-v1", ["documentId"] = "rev" }, CamAutomationTests.Mesh(row)));
        }
    }
}
