using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Preview;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio;

internal sealed class CamAutomationReviewWindow : Window, IDisposable
{
    private readonly GraphicPreviewPane preview = new(null, null, localOnly: true);
    private readonly CancellationTokenSource lifetime = new();
    private readonly CamAutomationPlan plan;
    private readonly ObservableCollection<ProcessRow> rows;
    private readonly DataGrid processes = new();
    private readonly DataGrid targets;
    private CamFaceGeometry? geometry;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6) };
    private readonly CheckBox original = new() { Margin = new Thickness(6) };
    internal CamAutomationPlan? Result { get; private set; }
    internal CamAutomationReviewWindow(CamAutomationPlan source, IReadOnlyList<CamMethodDefinition> methods, IGraphicPreviewClient client)
    {
        plan = source.Snapshot(); rows = new(plan.Steps.Select(s => new ProcessRow(s)));
        CamUi.Window(this, "Cam.Review", 1260, 840);
        var root = new DockPanel { Margin = new Thickness(12) }; Content = root;
        var heading = new TextBlock { Text = plan.DocumentName + (plan.WorkpieceName.Length == 0 ? "" : " · " + plan.WorkpieceName), TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 0, 4, 8) }; DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var footer = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        footer.Children.Add(CamUi.Button("Cam.Cancel", () => DialogResult = false));
        var recommend = CamUi.Button("Cam.AiRecommendation", () => Accept(true)); footer.Children.Add(recommend);
        var proceed = CamUi.Button("Cam.Proceed", () => Accept(false)); footer.Children.Add(proceed);
        recommend.IsEnabled = proceed.IsEnabled = false;
        var layout = new Grid(); layout.ColumnDefinitions.Add(new() { Width = new GridLength(1.15, GridUnitType.Star) }); layout.ColumnDefinitions.Add(new()); root.Children.Add(layout);
        var view = new DockPanel { Margin = new Thickness(0, 0, 10, 0) }; layout.Children.Add(view);
        original.Content = StudioStrings.Get("Cam.CompareOriginal"); original.Checked += (_, _) => Render(); original.Unchecked += (_, _) => Render();
        DockPanel.SetDock(original, Dock.Top); view.Children.Add(original); view.Children.Add(preview);
        var review = new DockPanel(); Grid.SetColumn(review, 1); layout.Children.Add(review);
        var toolbar = new WrapPanel(); DockPanel.SetDock(toolbar, Dock.Top); review.Children.Add(toolbar);
        toolbar.Children.Add(CamUi.Button("Cam.Up", () => Move(-1))); toolbar.Children.Add(CamUi.Button("Cam.Down", () => Move(1)));
        var change = new ComboBox { ItemsSource = methods, DisplayMemberPath = "Name", Width = 190, Margin = new Thickness(4), ToolTip = StudioStrings.Get("Cam.ChangeMethod") }; toolbar.Children.Add(change);
        toolbar.Children.Add(CamUi.Button("Cam.ChangeMethod", () => {
            if (processes.SelectedItem is not ProcessRow row || change.SelectedItem is not CamMethodDefinition method) return;
            CommitTargets();
            row.Step.Method = method.Snapshot(); row.Step.Colors.Palette = method.Colors.Snapshot();
            row.Step.Colors.Assignments.RemoveAll(a => !method.Colors.Roles.Any(r => r.Key == a.RoleKey));
            selected = null;
            processes.Items.Refresh(); SelectProcess();
        }));
        targets = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, SelectionMode = DataGridSelectionMode.Extended, MinHeight = 170 };
        CamColorControls.Apply(targets);
        var details = new Expander { Header = StudioStrings.Get("Cam.GeometryContract"), Content = targets, IsExpanded = false, MaxHeight = 330, Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(details, Dock.Bottom); review.Children.Add(details);
        processes = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, SelectionMode = DataGridSelectionMode.Single };
        CamColorControls.Apply(processes);
        processes.Columns.Add(new DataGridCheckBoxColumn { Header = StudioStrings.Get("Color.Include"), Binding = new Binding(nameof(ProcessRow.Include)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged } });
        processes.Columns.Add(CamUi.Column("Cam.Process", "Process", 130, true)); processes.Columns.Add(CamUi.Column("Cam.Method", "Method", 125, true));
        processes.Columns.Add(CamUi.Column("Cam.TargetCount", "Count", 64, true)); processes.Columns.Add(CamUi.Column("Color.Reason", "Reason", 240, true));
        review.Children.Add(processes);
        foreach (var row in rows) row.PropertyChanged += (_, _) => Render();
        processes.SelectionChanged += (_, _) => SelectProcess();
        Loaded += async (_, _) => {
            try {
                status.Text = StudioStrings.Get("Cam.LoadPreview");
                var mapped = await CamFaceGeometry.Load(client, plan, lifetime.Token); geometry = mapped;
                foreach (var step in plan.Steps)
                    step.Colors.Assignments.RemoveAll(a => !mapped.HasTarget(step.Colors.Geometry.OfType<JObject>().Single(r => (string?)r["key"] == a.TargetKey)));
                status.Text = StudioStrings.Get("Cam.UnassignedCount", plan.Geometry.Count(r => !plan.Steps.Any(s => s.Colors.Assignments.Any(a => a.TargetKey == (string?)r["key"]))));
                processes.Items.Refresh(); processes.SelectedIndex = rows.Count > 0 ? 0 : -1; SelectProcess();
                if (rows.Count == 0 || mapped.MappedFaceCount == 0) details.IsExpanded = true;
                recommend.IsEnabled = proceed.IsEnabled = rows.Count > 0 && mapped.MappedFaceCount > 0;
                if (mapped.MappedFaceCount == 0) status.Text = StudioStrings.Get("Cam.PreviewUnavailable");
            } catch (OperationCanceledException) { }
            catch (Exception ex) { status.Text = StudioStrings.Get("Cam.PreviewUnavailable") + " " + ex.Message; }
        };
        Closed += (_, _) => lifetime.Cancel();
        void Accept(bool recommended)
        {
            try {
                CommitTargets(); processes.CommitEdit(DataGridEditingUnit.Cell, true); processes.CommitEdit(DataGridEditingUnit.Row, true);
                if (recommended) {
                    plan.Steps = source.Snapshot().Steps;
                    foreach (var step in plan.Steps) step.Colors.Assignments.RemoveAll(a => !geometry!.HasTarget(step.Colors.Geometry.OfType<JObject>().Single(r => (string?)r["key"] == a.TargetKey)));
                } else plan.Steps = rows.Select(r => r.Step).ToList();
                if (geometry == null) throw new ArgumentException(StudioStrings.Get("Cam.PreviewUnavailable"));
                foreach (var step in plan.Steps.Where(s => s.Included)) foreach (var assignment in step.Colors.Assignments)
                    if (!geometry.HasTarget(step.Colors.Geometry.OfType<JObject>().Single(r => (string?)r["key"] == assignment.TargetKey))) throw new ArgumentException(StudioStrings.Get("Cam.PreviewUnavailable"));
                plan.Validate(); plan.Status = "reviewed"; Result = plan; DialogResult = true;
            } catch (ArgumentException ex) { status.Text = ex.Message; }
        }
    }
    private ProcessRow? selected;
    private List<CamColorReviewWindow.Row> targetRows = [];
    private void CommitTargets(bool commitEdit = true)
    {
        if (selected == null) return;
        if (commitEdit) { targets.CommitEdit(DataGridEditingUnit.Cell, true); targets.CommitEdit(DataGridEditingUnit.Row, true); }
        selected.Step.Colors.Assignments = targetRows.Where(r => r.Include && geometry?.HasTarget(r.Source) == true).Select(r => new CamColorAssignment {
            TargetKey = (string)r.Source["key"]!, RoleKey = r.RoleKey, Group = selected.Step.Method.Process, Reason = r.Reason }).ToList();
        selected.Step.Colors.Geometry = new JArray(targetRows.Where(r => r.Include && geometry?.HasTarget(r.Source) == true).Select(r => r.Source.DeepClone()));
        selected.TargetsChanged();
    }
    private void SelectProcess()
    {
        CommitTargets(); selected = processes.SelectedItem as ProcessRow; targets.Columns.Clear(); targetRows = [];
        targets.IsReadOnly = selected == null;
        {
            var colors = selected?.Step.Colors ?? new CamColorPlan { Palette = CamColorStandard.Starter() };
            targetRows = plan.Geometry.OfType<JObject>().Select(row => {
                var assignment = colors.Assignments.FirstOrDefault(a => a.TargetKey == (string?)row["key"]);
                return new CamColorReviewWindow.Row { Source = row, Palette = colors.Palette, DisplaySupported = geometry?.HasTarget(row) == true, Include = assignment != null, RoleKey = assignment?.RoleKey ?? "",
                    Reason = geometry?.HasTarget(row) != true ? geometry?.UnavailableReason(row) ?? StudioStrings.Get("Cam.MappingUnavailable") :
                        (bool?)row["colorSupported"] != true ? (string?)row["supportReason"] ?? StudioStrings.Get("Cam.MappingUnavailable") : assignment?.Reason ?? (string?)row["proposalReason"] ?? StudioStrings.Get("Color.Unassigned") };
            }).ToList();
            var checkStyle = new Style(typeof(CheckBox)); checkStyle.Setters.Add(new Setter(IsEnabledProperty, new Binding("Supported")));
            targets.Columns.Add(new DataGridCheckBoxColumn { Header = StudioStrings.Get("Color.Include"), Binding = new Binding("Include") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, ElementStyle = checkStyle, EditingElementStyle = checkStyle });
            targets.Columns.Add(CamUi.Column("Color.Target", "Target", 145, true)); targets.Columns.Add(CamUi.Column("Color.Scope", "Scope", 100, true));
            targets.Columns.Add(CamUi.Column("Color.Before", "Before", 85, true));
            targets.Columns.Add(new DataGridComboBoxColumn { Header = StudioStrings.Get("Color.Role"), ItemsSource = colors.Palette.Roles, DisplayMemberPath = "Label", SelectedValuePath = "Key", SelectedValueBinding = new Binding("RoleKey") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = 140 });
            targets.Columns.Add(CamUi.Column("Color.After", "After", 85, true)); targets.Columns.Add(CamUi.Column("Color.Reason", "Reason", 250, true));
            foreach (var row in targetRows) row.PropertyChanged += (_, _) => { CommitTargets(false); Render(); };
        }
        targets.ItemsSource = targetRows; Render();
    }
    private void Move(int delta)
    {
        var index = processes.SelectedIndex; if (index < 0 || index + delta < 0 || index + delta >= rows.Count) return;
        rows.Move(index, index + delta); processes.SelectedIndex = index + delta;
    }
    private void Render()
    {
        if (geometry == null || geometry.MappedFaceCount == 0) return;
        try { preview.ShowScene(geometry.Scene(selected?.Step.Included == true ? selected.Step.Colors : null, original.IsChecked == true), preview.Scene == null); }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException) { status.Text = ex.Message; }
    }
    public void Dispose() { lifetime.Cancel(); lifetime.Dispose(); preview.Dispose(); }
    internal sealed class ProcessRow(CamAutomationStep step) : INotifyPropertyChanged
    {
        public CamAutomationStep Step { get; } = step;
        public bool Include { get => Step.Included; set { Step.Included = value; PropertyChanged?.Invoke(this, new(nameof(Include))); } }
        public string Process => Step.Method.Process; public string Method => Step.Method.Name; public int Count => Step.Colors.Assignments.Count; public string Reason => Step.Reason;
        internal void TargetsChanged() => PropertyChanged?.Invoke(this, new(nameof(Count)));
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
