using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio;

internal sealed class CamMethodCatalogWindow : Window
{
    internal List<CamMethodDefinition>? Result { get; private set; }
    internal CamMethodCatalogWindow(IEnumerable<CamMethodDefinition> source, IMcpClient client, Func<JObject, CancellationToken, Task<bool>>? confirm = null)
    {
        CamUi.Window(this, "Cam.Methods", 820, 590);
        var methods = new ObservableCollection<CamMethodDefinition>(source.Select(m => m.Snapshot()));
        var root = new DockPanel { Margin = new Thickness(14) }; Content = root;
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
        var footer = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        var grid = new DataGrid { ItemsSource = methods, AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single };
        CamColorControls.Apply(grid); grid.Columns.Add(CamUi.Column("Cam.Process", "Process", 180)); grid.Columns.Add(CamUi.Column("Cam.Method", "Name", 210));
        grid.Columns.Add(CamUi.Column("Cam.Conditions", "Conditions", 340)); root.Children.Add(grid);
        void Edit(CamMethodDefinition original)
        {
            var dialog = new CamMethodEditorWindow(original, methods, client, confirm) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result is { } next) { var index = methods.IndexOf(original); if (index < 0) methods.Add(next); else methods[index] = next; }
        }
        footer.Children.Add(CamUi.Button("Cam.AddMethod", () => Edit(new CamMethodDefinition { Colors = CamColorStandard.Starter() })));
        footer.Children.Add(CamUi.Button("Cam.Edit", () => { if (grid.SelectedItem is CamMethodDefinition method) Edit(method); }));
        footer.Children.Add(CamUi.Button("Cam.Remove", () => { if (grid.SelectedItem is CamMethodDefinition method) methods.Remove(method); }));
        var cancel = CamUi.Button("Cam.Cancel", () => DialogResult = false); cancel.IsCancel = true; footer.Children.Add(cancel);
        footer.Children.Add(CamUi.Button("Cam.Save", () => { try { CamMethodCatalog.Validate(methods); Result = methods.Select(m => m.Snapshot()).ToList(); DialogResult = true; } catch (ArgumentException ex) { status.Text = ex.Message; } }));
    }
}

internal sealed class CamMethodEditorWindow : Window
{
    internal CamMethodDefinition? Result { get; private set; }
    internal CamMethodEditorWindow(CamMethodDefinition original, IEnumerable<CamMethodDefinition> catalog, IMcpClient client, Func<JObject, CancellationToken, Task<bool>>? confirm = null)
    {
        var method = original.Snapshot(); CamUi.Window(this, "Cam.MethodRegistration", 850, 810);
        var lifetime = new CancellationTokenSource(); Closed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
        var root = new DockPanel { Margin = new Thickness(14) }; Content = root;
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) }; DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        var form = new StackPanel(); root.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var identity = new StackPanel();
        var pdm = CamUi.Field(identity, "Cam.PdmId", method.PdmObjectId);
        var revision = CamUi.Field(identity, "Cam.Revision", method.MethodDocumentId, readOnly: true);
        var name = CamUi.Field(form, "Cam.Method", method.Name, 120); var process = CamUi.Field(form, "Cam.Process", method.Process, 80);
        form.Children.Add(new Expander { Header = StudioStrings.Get("Cam.NativeIdentity"), Content = identity, Margin = new Thickness(0, 8, 0, 0) });
        var actions = new WrapPanel(); form.Children.Insert(0, actions);
        var select = CamUi.Button("Cam.PdmExplorer", () => { }); actions.Children.Add(select);
        var verify = CamUi.Button("Cam.VerifyMethod", () => { }); actions.Children.Add(verify);
        async Task Verify(string pdmId)
        {
            var result = await client.CallToolAsync("topsolid_inspect_cam_method", new JObject { ["pdmObjectId"] = pdmId }, lifetime.Token);
            var data = PdmInventory.Data(result);
            if (result.IsError || data == null || (bool?)data["isDirty"] == true) throw new InvalidOperationException(data?.ToString() ?? StudioStrings.Get("Cam.MethodUnavailable"));
            method.PdmObjectId = (string)data["pdmObjectId"]!; method.MethodDocumentId = (string)data["methodDocumentId"]!;
            pdm.Text = method.PdmObjectId; revision.Text = method.MethodDocumentId;
            if (string.IsNullOrWhiteSpace(name.Text)) name.Text = (string?)data["name"] ?? "";
            status.Text = StudioStrings.Get("Cam.Verified");
        }
        async Task WithStatus(Func<Task> action)
        {
            verify.IsEnabled = select.IsEnabled = false;
            try {
                await action();
            } catch (Exception ex) when (ex is not OperationCanceledException) { status.Text = ex.Message; }
            catch (OperationCanceledException) { }
            finally { verify.IsEnabled = select.IsEnabled = true; }
        }
        verify.Click += async (_, _) => await WithStatus(() => Verify(pdm.Text.Trim()));
        select.Click += async (_, _) => await WithStatus(async () => {
            var chooser = new CamMethodBrowserWindow(client, confirm) { Owner = this };
            if (chooser.ShowDialog() == true && chooser.Selected is { } selected) {
                name.Text = (string?)selected["name"] ?? "";
                method.PdmObjectId = (string)selected["pdmObjectId"]!;
                method.MethodDocumentId = (string)selected["methodDocumentId"]!;
                pdm.Text = method.PdmObjectId; revision.Text = method.MethodDocumentId;
                status.Text = StudioStrings.Get("Cam.Verified");
            }
            await Task.CompletedTask;
        });
        var conditions = CamUi.Field(form, "Cam.Conditions", method.Conditions, 4000); conditions.AcceptsReturn = true; conditions.MinHeight = 64; conditions.TextWrapping = TextWrapping.Wrap;
        var scope = new ComboBox { ItemsSource = new[] { new { Key = "document", Label = StudioStrings.Get("Cam.ScopeDocument") }, new { Key = "workpiece", Label = StudioStrings.Get("Cam.ScopeWorkpiece") } },
            DisplayMemberPath = "Label", SelectedValuePath = "Key", SelectedValue = method.SearchScope, Margin = new Thickness(0, 8, 0, 0) }; form.Children.Add(scope);
        form.Children.Add(new TextBlock { Text = StudioStrings.Get("Cam.DocumentScope"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8) });
        var palette = CamUi.Button("Cam.ColorContract", () => {
            var dialog = new CamColorPaletteWindow(method.Colors) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result is { } colors) method.Colors = colors;
        }); form.Children.Add(palette);
        form.Children.Add(new TextBlock { Text = StudioStrings.Get("Cam.Prerequisites"), Margin = new Thickness(0, 8, 0, 4) });
        var predecessors = new ListBox { ItemsSource = catalog.Where(m => m.Id != method.Id).ToArray(), DisplayMemberPath = "Name", SelectionMode = SelectionMode.Multiple, MinHeight = 45, MaxHeight = 100 };
        foreach (CamMethodDefinition item in predecessors.Items) if (method.AfterMethods.Contains(item.Id)) predecessors.SelectedItems.Add(item); form.Children.Add(predecessors);
        form.Children.Add(CamUi.Options(method.Options));
        form.Children.Add(new TextBlock { Text = StudioStrings.Get("Cam.Inputs"), Margin = new Thickness(0, 8, 0, 4) });
        var inputs = new ObservableCollection<InputRow>(method.Inputs.Select(i => new InputRow(i)));
        var inputGrid = new DataGrid { ItemsSource = inputs, AutoGenerateColumns = false, CanUserAddRows = true, CanUserDeleteRows = true, MinHeight = 130, MaxHeight = 230 };
        CamColorControls.Apply(inputGrid); inputGrid.Columns.Add(CamUi.Column("Cam.MethodParameter", "Name")); inputGrid.Columns.Add(CamUi.Column("Cam.Label", "Label"));
        inputGrid.Columns.Add(new DataGridComboBoxColumn { Header = StudioStrings.Get("Cam.ValueType"), ItemsSource = new[] { "text", "real", "integer", "boolean" }, SelectedItemBinding = new System.Windows.Data.Binding("ValueType"), Width = 100 });
        inputGrid.Columns.Add(CamUi.Column("Cam.Unit", "UnitType", 100)); inputGrid.Columns.Add(CamUi.Column("Cam.Value", "Value", 160)); form.Children.Add(inputGrid);
        var cancel = CamUi.Button("Cam.Cancel", () => DialogResult = false); cancel.IsCancel = true; footer.Children.Add(cancel);
        footer.Children.Add(CamUi.Button("Cam.Save", () => {
            try {
                if (!inputGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !inputGrid.CommitEdit(DataGridEditingUnit.Row, true)) return;
                if (pdm.Text.Trim() != method.PdmObjectId || method.MethodDocumentId.Length == 0) throw new ArgumentException(StudioStrings.Get("Cam.VerifyRequired"));
                method.Name = name.Text.Trim(); method.Process = process.Text.Trim(); method.Conditions = conditions.Text.Trim();
                method.SearchScope = (string?)scope.SelectedValue ?? "document";
                method.AfterMethods = predecessors.SelectedItems.Cast<CamMethodDefinition>().Select(m => m.Id).ToList(); method.Inputs = inputs.Select(i => i.ToInput()).ToList();
                method.Validate(); Result = method; DialogResult = true;
            } catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException) { status.Text = ex.Message; }
        }));
    }
    public sealed class InputRow
    {
        public string Name { get; set; } = ""; public string Label { get; set; } = ""; public string ValueType { get; set; } = "text";
        public string UnitType { get; set; } = ""; public string Value { get; set; } = "";
        public InputRow() { }
        public InputRow(CamMethodInput input) { Name = input.Name; Label = input.Label; ValueType = input.ValueType; UnitType = input.UnitType; Value = input.Value?.ToString() ?? ""; }
        public CamMethodInput ToInput() => new() { Name = Name.Trim(), Label = Label, ValueType = ValueType, UnitType = UnitType,
            Value = Value.Length == 0 ? null : ValueType switch { "real" => new JValue(double.Parse(Value, CultureInfo.InvariantCulture)), "integer" => new JValue(int.Parse(Value, CultureInfo.InvariantCulture)), "boolean" => new JValue(bool.Parse(Value)), _ => new JValue(Value) } };
    }
}
