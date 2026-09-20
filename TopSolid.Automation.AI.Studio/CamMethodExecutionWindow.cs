using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio;

internal sealed class CamMethodExecutionWindow : Window
{
    internal CamAutomationPlan? Result { get; private set; }
    internal CamMethodExecutionWindow(CamAutomationPlan source)
    {
        var plan = source.Snapshot(); CamUi.Window(this, "Cam.ConfirmExecution", 880, 780);
        var root = new DockPanel { Margin = new Thickness(14) }; Content = root;
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap }; DockPanel.SetDock(error, Dock.Bottom); root.Children.Add(error);
        var form = new StackPanel(); root.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        form.Children.Add(new TextBlock { Text = plan.DocumentName + "\n" + StudioStrings.Get("Cam.PreparedConfirm"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
        form.Children.Add(new TextBlock { Text = StudioStrings.Get("Cam.ExecutionScope", plan.WorkpieceName, plan.MachiningStageName), TextWrapping = TextWrapping.Wrap });
        var edits = new List<(CamAutomationStep Step, DataGrid Grid, ObservableCollection<CamMethodEditorWindow.InputRow> Values)>();
        int order = 0;
        foreach (var step in plan.Steps.Where(s => s.Included))
        {
            form.Children.Add(new TextBlock { Text = $"{++order}. {step.Method.Process} · {step.Method.Name} · {step.Colors.Assignments.Count}", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 3) });
            form.Children.Add(new TextBlock { Text = StudioStrings.Get("Cam.State." + step.Status), Margin = new Thickness(0, 0, 0, 3) });
            if (step.Status is "calculated" or "pending" or "deferred" or "failedOrUnknown" or "submitting") continue;
            form.Children.Add(CamUi.Options(step.Method.Options));
            var rows = new ObservableCollection<CamMethodEditorWindow.InputRow>(step.Method.Inputs.Select(i => new CamMethodEditorWindow.InputRow(i)));
            if (rows.Count == 0) continue;
            var grid = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, MaxHeight = 180 }; CamColorControls.Apply(grid);
            grid.Columns.Add(CamUi.Column("Cam.Label", "Label", 200, true)); grid.Columns.Add(CamUi.Column("Cam.ValueType", "ValueType", 100, true));
            grid.Columns.Add(CamUi.Column("Cam.Unit", "UnitType", 100, true)); grid.Columns.Add(CamUi.Column("Cam.Value", "Value", 180)); form.Children.Add(grid); edits.Add((step, grid, rows));
        }
        var cancel = CamUi.Button("Cam.KeepPrepared", () => DialogResult = false); cancel.IsCancel = true; footer.Children.Add(cancel);
        footer.Children.Add(CamUi.Button("Cam.Execute", () => {
            try {
                foreach (var edit in edits) { if (!edit.Grid.CommitEdit(DataGridEditingUnit.Cell, true) || !edit.Grid.CommitEdit(DataGridEditingUnit.Row, true)) return; edit.Step.Method.Inputs = edit.Values.Select(v => v.ToInput()).ToList(); }
                foreach (var step in plan.Steps.Where(s => s.Included && s.Status is "planned" or "colored"))
                    foreach (var input in step.Method.Inputs)
                        if (input.Value == null || input.Value.Type == Newtonsoft.Json.Linq.JTokenType.Null) throw new ArgumentException(StudioStrings.Get("Cam.InputRequired", input.Label.Length == 0 ? input.Name : input.Label));
                plan.Validate(); Result = plan; DialogResult = true;
            } catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException) { error.Text = ex.Message; }
        }));
    }
}
