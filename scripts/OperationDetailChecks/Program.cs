using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio;
using TopSolid.Automation.AI.Studio.Appearance;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.AI.Studio.Preview;
using TopSolid.Automation.Mcp.Contracts;

internal static class Program
{
    private static readonly JObject Element = new() { ["documentId"] = "cam-rev", ["id"] = 12 };
    private static readonly string Output = Path.GetFullPath("artifacts/operation-detail-checks");
    [STAThread]
    private static int Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/TopSolid.Automation.AI.Studio;component/Appearance/TopSolidStyles.xaml", UriKind.Relative) });
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var exit = 0;
        app.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                Directory.CreateDirectory(Output);
                if (args.Length == 2 && args[0] == "--machine") { await MachineChecks(args[1]); return; }
                await ModelChecks(); await EntryCheck(); await UiChecks();
                if (args.Length == 2 && args[0] == "--live") await Live(args[1]);
                Console.WriteLine("PASS operation details: complete pagination, exact targeting, typed writes, approval decline, revision rebasing, stale edits, favorites, navigation and light/dark layout.");
            }
            catch (Exception error) { Console.Error.WriteLine(error); exit = 1; }
            finally { app.Shutdown(); }
        });
        app.Run(); return exit;
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static async Task MachineChecks(string path)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var scenes = await CamContextPreview.ReadAsync(Path.GetFullPath(path), default);
        Assert(new PreviewDefaults() == new PreviewDefaults(false, true, true, true), "Incorrect fresh preview defaults");
        var settingsDirectory = Path.Combine(Output, "preview-settings");
        var settingsStore = new SettingsStore(settingsDirectory);
        foreach (var edges in new[] { false, true })
        foreach (var part in new[] { false, true })
        foreach (var stock in new[] { false, true })
        foreach (var machine in new[] { false, true })
        {
            var defaults = new PreviewDefaults(edges, part, stock, machine);
            settingsStore.Save(new AppSettings { PreviewDefaults = defaults });
            Assert(settingsStore.Load().PreviewDefaults == defaults, "Preview defaults did not survive settings save/load");
            var selectedScene = scenes.Select(machine, stock ? CamStockMode.Remaining : CamStockMode.Part, part);
            var expected = (part ? scenes.Part.Triangles : 0) + (stock ? scenes.RemainingOnly?.Triangles ?? 0 : 0)
                + (machine ? scenes.MachinePart.Triangles - scenes.Part.Triangles : 0);
            Assert(selectedScene.Triangles == expected, "Preview layer defaults selected incorrect geometry");
        }
        var visibility = scenes.MachineElements ?? throw new Exception("No machine elements");
        Assert(visibility.Roots.Any(r => r.Name == "Portes") && visibility.Roots.Any(r => r.Name == "Chariot X"), "Native machine groups missing");
        var doors = visibility.Roots.Single(r => r.Name == "Portes");
        var original = scenes.Select(true, CamStockMode.Remaining);
        visibility.Apply(doors.Geometry);
        var hidden = scenes.Select(true, CamStockMode.Remaining);
        Assert(hidden.Triangles < original.Triangles && hidden.Triangles >= scenes.Work.Triangles, "Machine component visibility did not preserve work geometry");
        Assert(ReferenceEquals(scenes.Select(false, CamStockMode.Remaining), scenes.Work), "Machine visibility changed work-only mode");
        visibility.Apply([]); Assert(scenes.Select(true, CamStockMode.Remaining).Triangles == original.Triangles, "Show all did not restore geometry");
        foreach (var dark in new[] { false, true })
        {
            StudioStrings.Apply("ko"); TopSolidTheme.Apply(new(dark, "Machine elements", "Fixture"));
            var window = new MachineElementsWindow(visibility) { Left = -20000, Top = -20000, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
            window.Show(); await Layout(window);
            try
            {
                var checks = Descendants<CheckBox>(window).ToArray();
                var doorBox = checks.Single(b => AutomationProperties.GetName(b) == "Portes");
                doorBox.IsChecked = false; doorBox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert(window.Hidden.Count == doors.Geometry.Count && visibility.Hidden.Count == 0, "Draft visibility changed scene before Apply");
                Assert(checks[0].IsChecked == null, "Mixed parent state was lost");
                Render(window, "machine-elements-" + (dark ? "dark" : "light") + ".png");
                visibility.Apply(window.Hidden);
                Assert(scenes.Select(true, CamStockMode.Remaining).Triangles == hidden.Triangles, "Apply does not use checkbox selection");
            }
            finally { window.Close(); visibility.Apply([]); }
        }
        visibility.Apply(visibility.Roots.SelectMany(r => r.Geometry));
        Assert(scenes.Select(true, CamStockMode.Part).Triangles >= scenes.Part.Triangles, "Hide all removed the part");
        var client = new MachineClient(await File.ReadAllBytesAsync(path));
        using var pane = new GraphicPreviewPane(client, new JObject { ["documentId"] = "machine-fixture" });
        var host = new Window { Content = pane, Width = 1050, Height = 750, Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false };
        host.Show();
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!pane.HasMachineContext && DateTime.UtcNow < deadline) await Task.Delay(30);
            await Layout(host);
            Assert(pane.HasMachineContext, "Machine context did not load through preview pane");
            var dropdown = Descendants<Button>(host).Single(b => AutomationProperties.GetName(b) == StudioStrings.Get("Preview.MachineElements"));
            var finished = new TaskCompletionSource();
            _ = host.Dispatcher.BeginInvoke(async () =>
            {
                var dialog = host.OwnedWindows.OfType<MachineElementsWindow>().Single();
                try
                {
                    await Layout(dialog);
                    var checkbox = Descendants<CheckBox>(dialog).Single(b => AutomationProperties.GetName(b) == "Portes");
                    checkbox.IsChecked = false; checkbox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    Descendants<Button>(dialog).Single(b => b.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    finished.SetResult();
                }
                catch (Exception error) { finished.SetException(error); dialog.Close(); }
            }, DispatcherPriority.ApplicationIdle);
            dropdown.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await finished.Task;
            Assert(pane.Scene!.Triangles == hidden.Triangles && client.Reads == 1, "Dropdown Apply did not filter scene locally");
            pane.SetMachineVisible(false); pane.SetMachineVisible(true);
            Assert(pane.Scene!.Triangles == hidden.Triangles, "Master machine toggle lost component choices");
            pane.SetMachineVisible(false); pane.SetStockMode(CamStockMode.Part);
            var partButton = Descendants<ToggleButton>(host).Single(b => AutomationProperties.GetName(b) == StudioStrings.Get("Preview.Part"));
            partButton.IsChecked = false; await Layout(host);
            Assert(pane.Scene!.Triangles == 0, "All-hidden display did not clear geometry");
            partButton.IsChecked = true; await Layout(host);
            Assert(pane.Scene!.Triangles == scenes.Part.Triangles && client.Reads == 1, "Part toggle failed to restore cached geometry");
        }
        finally { host.Close(); }
        Console.WriteLine($"PASS native machine tree: {visibility.Roots.Count} groups; {original.Triangles} -> {hidden.Triangles} triangles with doors hidden; decode and UI {timer.Elapsed.TotalSeconds:F1}s; no native writes.");
    }
    private sealed class MachineClient(byte[] bytes) : IGraphicPreviewClient
    {
        internal int Reads;
        public Task<JObject> GetGraphicPreviewAsync(JObject target, CancellationToken token)
        {
            Reads++;
            return Task.FromResult(new JObject { ["status"] = "ready", ["documentId"] = "machine-fixture", ["format"] = "glb", ["units"] = "m", ["upAxis"] = "Z", ["camContext"] = true, ["data"] = Convert.ToBase64String(bytes) });
        }
        public Task<GraphicPreviewData> GetGraphicPreviewDataAsync(JObject target, CancellationToken token)
        {
            Reads++;
            return Task.FromResult(new GraphicPreviewData(new JObject { ["status"] = "ready", ["documentId"] = "machine-fixture", ["format"] = "glb", ["units"] = "m", ["upAxis"] = "Z", ["camContext"] = true }, bytes));
        }
    }
    private static JObject Real(string name, string label, double value, string unit = "Length") => new()
    {
        ["name"] = name, ["displayName"] = label, ["valueType"] = "Real", ["realValueSI"] = value,
        ["unitType"] = unit, ["displayValue"] = unit == "Length" ? (value * 1000) + " mm" : value.ToString(),
        ["editSupported"] = true, ["readOnly"] = false, ["smartType"] = "Basic"
    };
    private static List<JObject> Fixture() =>
    [
        Real("Diameter@Tool", "Tool diameter", .012), Real("Overhang@Tool", "Tool overhang", .052),
        Real("CuttingSpeed@CuttingConditions|Tool", "Cutting speed", 2.5, "Speed"),
        new() { ["name"] = "Feed@CuttingConditions", ["displayName"] = "Feed per tooth", ["valueType"] = "FeedRate", ["displayValue"] = "0.12 mm/tooth", ["editSupported"] = false, ["readOnly"] = false },
        Real("Stock@Geometry", "Radial stock allowance", .0003), Real("Safety@Strategy", "Safety distance", .002),
        new() { ["name"] = "Mode@Strategy", ["displayName"] = "Milling direction", ["valueType"] = "Integer", ["integerValue"] = 0, ["displayValue"] = "Climb", ["editSupported"] = true, ["readOnly"] = false, ["smartType"] = "Basic",
            ["allowedValues"] = new JArray(new JObject { ["value"] = 0, ["label"] = "Climb" }, new JObject { ["value"] = 2, ["label"] = "Conventional" }) },
        new() { ["name"] = "Comment@Comments", ["displayName"] = "Operation comment", ["valueType"] = "Text", ["textValue"] = "Finish pocket", ["displayValue"] = "Finish pocket", ["editSupported"] = true, ["readOnly"] = false, ["smartType"] = "Basic" },
        new() { ["name"] = "WCS@WCS", ["displayName"] = "Work coordinate system", ["valueType"] = "Geometry", ["displayValue"] = "G54 · Setup 1", ["editSupported"] = false, ["readOnly"] = true },
        Real("Tilt@MultiAxis", "Tool tilt angle", Math.PI / 6, "Angle"),
        new() { ["name"] = "Enabled@Properties", ["displayName"] = "Enabled", ["valueType"] = "Boolean", ["booleanValue"] = true, ["displayValue"] = "True", ["editSupported"] = true, ["readOnly"] = false, ["smartType"] = "Basic",
            ["allowedValues"] = new JArray(new JObject { ["value"] = false }, new JObject { ["value"] = true }) },
        new() { ["name"] = "Unknown@OtherCategory", ["displayName"] = "Machine limit", ["isError"] = true, ["error"] = "Fixture read failure" }
    ];
    private static async Task ModelChecks()
    {
        var client = new Client(); client.Rows = Enumerable.Range(0, 257).Select(i => Real("P" + i + "@Strategy", "P" + i, .002)).ToList();
        var rows = await CamOperationDetails.Load(client, Element, default);
        Assert(rows.Count == 257 && client.ListCalls > 2, "Pagination omitted operation parameters");
        client.BrokenPaging = true;
        try { await CamOperationDetails.Load(client, Element, default); throw new Exception("Bad pagination accepted"); } catch (InvalidDataException) { }
        Assert(CamOperationDetails.Page(Fixture()[2]) == "cam-cutting-conditions", "Nested Tool category captured cutting conditions");
        Assert(CamOperationDetails.Page(Fixture()[8]) == "cam-comment", "WCS page mapping");
        Assert(CamOperationDetails.Page(Fixture()[11]) == "cam-properties", "Unknown parameter disappeared");
        client = new Client();
        var duplicate = (JObject)client.Rows[0].DeepClone(); client.Rows[0]["parameter"] = new JObject { ["id"] = 1 }; duplicate["parameter"] = new JObject { ["id"] = 2 }; client.Rows.Add(duplicate);
        var duplicates = await CamOperationDetails.Load(client, Element, default);
        Assert(duplicates.Count == client.Rows.Count && duplicates.Where(r => (string?)r["name"] == "Diameter@Tool").All(r => (bool?)r["editSupported"] == false), "Ambiguous native owners were lost or editable");
        foreach (var approve in new[] { true, false })
        {
            client = new Client(); var revisions = new List<string>();
            var receipts = client.Rows.Take(2).Select(r => CamOperationDetails.Receipt(Element, r)).ToArray();
            await CamParameterEditRequest.RunSelected("Edit details", receipts, client,
                (fresh, _) => Task.FromResult<IReadOnlyList<JObject>?>(fresh.Select(r => new CamParameterDraft(r).Arguments("4", null)).ToArray()),
                (_, _) => Task.FromResult(approve), _ => { }, default, (_, after) => revisions.Add(after));
            Assert(client.Writes.Count == (approve ? 2 : 0), "Confirmation contract bypassed");
            if (approve) Assert((string?)client.Writes[1]["documentId"] == "cam-rev-1" && revisions.Count == 2, "Revision not propagated");
        }
    }
    private static async Task UiChecks()
    {
        var owner = new Window { Width = 1000, Height = 750, Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false };
        owner.Show();
        try
        {
            foreach (var dark in new[] { false, true })
            foreach (var language in new[] { "en", "ko" })
            {
                StudioStrings.Apply(language); TopSolidTheme.Apply(new(dark, "Detail fixture", "test"));
                var client = new Client(); var favorites = new CamParameterFavorites(Path.Combine(Output, "preferences-" + Guid.NewGuid().ToString("N")));
                var receipts = new Dictionary<string, JObject> { ["op"] = new() { ["value"] = new JObject { ["operation"] = new JObject { ["element"] = Element.DeepClone() } } } };
                var question = new UserQuestion("Operation", "select", "operation", false, "", null, null, [new("op", "1 · Pocket finishing", "", "operation", "Pocket")], receipts);
                var detail = new CamOperationDetailWindow("10 · Pocket finishing", "T 3 · End mill Ø12 · Carbide", "operation", Element, client,
                    (_, _) => Task.FromResult(true), _ => { }, question.RebaseDocument, favorites)
                    { Owner = owner, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false };
                detail.Show(); await detail.Loading; await Layout(detail);
                try
                {
                    var pages = Descendants<ListBox>(detail).Single(c => c.Name == "DetailPages");
                    var search = Descendants<TextBox>(detail).Single(c => c.Name == "DetailSearch");
                    var apply = Descendants<Button>(detail).Single(c => c.Name == "ApplyDetails");
                    Assert(pages.Items.Count == 8 && !apply.IsEnabled, "Page count or initial dirty state");
                    var diameter = Editor(detail, "Tool diameter"); diameter.Text = "14";
                    var star = Descendants<ToggleButton>(detail).First(c => c is not CheckBox && c.ToolTip?.ToString() == StudioStrings.Get("Cam.Favorite"));
                    star.IsChecked = true; star.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    pages.SelectedIndex = 4; await Layout(detail);
                    var safety = Editor(detail, "Safety distance"); safety.Text = "3";
                    Assert(Descendants<ComboBox>(detail).Single().SelectedItem is ComboBoxItem { Content: "Climb" }, "Native choice not selected");
                    search.Text = "no matching parameter"; search.Clear();
                    Assert(Editor(detail, "Safety distance").Text == "3", "Search lost the draft");
                    Render(detail, $"detail-strategy-{language}-{(dark ? "dark" : "light")}.png");
                    pages.SelectedIndex = 0; await Layout(detail);
                    Assert(Editor(detail, "Tool diameter").Text == "14", "Favorites lost draft or parameter");
                    Assert(apply.IsEnabled, "Apply unavailable after edits");
                    await detail.Apply(); await Layout(detail);
                    Assert(client.Writes.Count == 2 && (double)client.Writes[0]["realValueSI"]! == .014 && (double)client.Writes[1]["realValueSI"]! == .003, "UI edits not written as SI");
                    Assert((string?)question.PreviewTargetFor("op")?["documentId"] == "cam-rev-2", "Parent selection has stale revision");
                    Assert(!apply.IsEnabled, "Verified changes stayed dirty");
                    pages.SelectedIndex = 2; await Layout(detail);
                    Assert(Descendants<TextBlock>(detail).Any(c => c.Text == StudioStrings.Get("Cam.NativeEditor")), "Composite value has no read-only explanation");
                    Render(detail, $"detail-cutting-{language}-{(dark ? "dark" : "light")}.png");
                    pages.SelectedIndex = 4; await Layout(detail);
                    Editor(detail, "Safety distance").Text = "5";
                    client.Rows.Single(r => (string?)r["name"] == "Safety@Strategy")["realValueSI"] = .004;
                    await detail.Apply(); await Layout(detail);
                    Assert(client.Writes.Count == 2 && Editor(detail, "Safety distance").Text == "5", "Stale edit wrote or lost draft");
                    Assert(Descendants<TextBlock>(detail).Any(t => t.Text == StudioStrings.Get("Cam.ChangedExternally")), "External modification has no feedback");
                    // A second explicit apply after review succeeds and clears the local draft.
                    await detail.Apply(); Assert(client.Writes.Count == 3 && !apply.IsEnabled, "Reviewed stale edit did not apply");
                    pages.SelectedIndex = 7; await Layout(detail); Render(detail, $"detail-properties-{language}-{(dark ? "dark" : "light")}.png");
                    detail.Width = 790; detail.Height = 480; await Layout(detail); Render(detail, $"detail-small-{language}-{(dark ? "dark" : "light")}.png");
                }
                finally { detail.Close(); }
            }
        }
        finally { owner.Close(); }
    }
    private static async Task EntryCheck()
    {
        StudioStrings.Apply("en"); TopSolidTheme.Apply(new(false, "Light", "Detail button fixture"));
        foreach (var shape in new[] { "summary", "element", "flat" })
        foreach (var mode in new[] { "browse", "single", "multiple" })
        {
        var client = new Client();
        JObject Native(JObject element) => shape switch { "summary" => new JObject { ["operation"] = new JObject { ["element"] = element.DeepClone() } }, "element" => new JObject { ["element"] = element.DeepClone() }, _ => (JObject)element.DeepClone() };
        var receipt = new JObject { ["value"] = Native(Element) };
        var other = new JObject { ["value"] = Native(new JObject { ["documentId"] = "cam-rev", ["id"] = 13 }) };
        var question = new UserQuestion("Operations", "select", "operation", mode == "multiple", "", null, null,
            [new("one", "Pocket finishing", "", "operation", "Pocket"), new("two", "Face milling", "", "operation", "Face")],
            new Dictionary<string, JObject> { ["one"] = receipt, ["two"] = other }) { IsBrowse = mode == "browse" };
        var window = new QuestionWindow(question, client) { Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
        window.ConfirmDetailChange = (_, _) => Task.FromResult(true);
        window.Show(); await Layout(window);
        try
        {
            var list = (ListBox)window.FindName("ChoiceList");
            Assert(window.FindName("OperationDetail") == null, "Detail remains above the list");
            Assert(!Descendants<Button>(list).Any(b => b.Name == "RowOperationDetail" && b.IsVisible), "Detail without selected operation");
            list.SelectedIndex = 0; await Layout(window);
            var button = Descendants<Button>(list).Single(b => b.Name == "RowOperationDetail" && b.IsVisible);
            Assert(button.IsEnabled && button.ToolTip.ToString()!.Contains("Pocket finishing"), "Single operation has no row Detail action");
            Assert(window.Title == StudioStrings.Get("Cam.Operations"), "CAM dialog title depends on entry point");
            Assert(((Button)window.FindName("ClearSelection")).Visibility == Visibility.Visible, "Browse hides shared CAM actions");
            if (mode == "multiple")
            {
                Assert(list.SelectionMode == SelectionMode.Extended, "CAM plain-click selection differs from single/browse mode");
                list.SelectedItems.Add(list.Items[1]); await Layout(window);
                Assert(Descendants<Button>(list).Count(b => b.Name == "RowOperationDetail" && b.IsVisible) == 2 && button.ToolTip.ToString()!.Contains("Pocket finishing"), "Row Detail lost its own target in a multi-selection");
                list.SelectedItems.Remove(list.Items[1]);
            }
            Assert(JToken.DeepEquals(question.PreviewTargetFor("one")?["operation"], Element), "CAM identity format changes preview/detail target");
            var completion = new TaskCompletionSource();
            _ = window.Dispatcher.BeginInvoke(async () =>
            {
                var detail = window.OwnedWindows.OfType<CamOperationDetailWindow>().Single();
                try { await detail.Loading; await Layout(detail); Assert(Descendants<TextBox>(detail).Any(c => AutomationProperties.GetName(c).StartsWith("Tool diameter")), "Detail did not load selected operation"); completion.SetResult(); }
                catch (Exception error) { completion.SetException(error); }
                finally { detail.Close(); }
            }, DispatcherPriority.ApplicationIdle);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await completion.Task;
            Assert(list.SelectedItems.Count == 1, "Detail changed parent selection");
            Render(window, $"operation-dialog-{shape}-{mode}.png");
            ((Button)window.FindName("ClearSelection")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Layout(window);
            Assert(!Descendants<Button>(list).Any(b => b.Name == "RowOperationDetail" && b.IsVisible), "Clear left stale Detail target");
        }
        finally { window.Close(); }
        }
        var previewClient = new Client();
        var preview = await CamDialogRequest.ForPreview(new JObject { ["operation"] = Element.DeepClone() }, previewClient, _ => { }, default);
        Assert(preview.IsBrowse && preview.InitialInspectionKey != null && JToken.DeepEquals(preview.PreviewTargetFor(preview.InitialInspectionKey)?["operation"], Element), "Explicit toolpath preview lost its selected operation");
        var previewWindow = new QuestionWindow(preview, previewClient) { Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false };
        previewWindow.Show(); await Layout(previewWindow);
        try { Assert(Descendants<Button>(previewWindow).Any(b => b.Name == "RowOperationDetail" && b.IsVisible && b.IsEnabled), "Toolpath result still cannot open Detail"); }
        finally { previewWindow.Close(); }
    }
    private static async Task Live(string executable)
    {
        await using var client = new StdioMcpClient(); using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await client.ConnectAsync(Path.GetFullPath(executable), timeout.Token);
        var instances = await client.GetConnectionInstancesAsync(timeout.Token);
        var instance = instances.Single(i => i.Supported);
        await client.ConnectAsync(Path.GetFullPath(executable), new TopSolidConnectionOptions { Selection = "instance", ProcessId = instance.ProcessId, StartTimeUtcTicks = instance.StartTimeUtcTicks, ExpectedVersion = instance.Version }, "", timeout.Token);
        async Task<JObject> Read(string name, JObject args)
        {
            Assert(client.Tools.Single(t => t.Name == name).Annotations["readOnlyHint"]?.Value<bool>() == true, "Live test attempted a write");
            var result = await client.CallToolAsync(name, args, timeout.Token); Assert(!result.IsError, result.Content.ToString());
            return PdmInventory.Data(result)!;
        }
        var docs = await Read("topsolid_list_document_summaries", new JObject { ["scope"] = "open", ["limit"] = 100 });
        var cam = ((JArray)docs["items"]!).OfType<JObject>().First(r => ((string?)r["type"] ?? "").StartsWith("TopSolid.Cam.", StringComparison.Ordinal));
        var target = new JObject { ["documentId"] = cam["documentId"]!.DeepClone() };
        var before = await Read("topsolid_get_document_info", target);
        var ops = await Read("topsolid_list_cam_operation_summaries", new JObject { ["documentId"] = target["documentId"]!.DeepClone(), ["limit"] = 100 });
        var operationRow = ((JArray)ops["items"]!).OfType<JObject>().First(r => r["tool"] != null && r["tool"]!.Type != JTokenType.Null);
        var native = (JObject)(operationRow["operation"]!["element"] ?? operationRow["operation"]!);
        var first = await Read(CamOperationDetails.ListTool, new JObject { ["element"] = native.DeepClone(), ["offset"] = 0, ["limit"] = 100 });
        File.WriteAllText(Path.Combine(Output, "live-first-page.json"), first.ToString());
        File.WriteAllText(Path.Combine(Output, "live-target.json"), native.ToString());
        var raw = new JArray(); var page = first;
        while (true)
        {
            foreach (var row in (JArray)page["items"]!) raw.Add(row.DeepClone());
            if ((bool?)page["hasMore"] != true) break;
            page = await Read(CamOperationDetails.ListTool, new JObject { ["element"] = native.DeepClone(), ["offset"] = page["nextOffset"]!.DeepClone(), ["limit"] = 100 });
        }
        File.WriteAllText(Path.Combine(Output, "live-raw-parameters.json"), raw.ToString());
        var parameters = await CamOperationDetails.Load(client, native, timeout.Token);
        var after = await Read("topsolid_get_document_info", target);
        Assert(JToken.DeepEquals(before["document"], after["document"]), "Read-only detail inspection changed native document");
        var report = new JObject { ["operation"] = operationRow.DeepClone(), ["parameterCount"] = parameters.Count, ["parameters"] = new JArray(parameters), ["nativeWrites"] = 0,
            ["pages"] = JObject.FromObject(parameters.GroupBy(CamOperationDetails.Page).ToDictionary(g => g.Key, g => g.Count())), ["documentUnchanged"] = true };
        File.WriteAllText(Path.Combine(Output, "live-read.json"), report.ToString());
        Console.WriteLine("LIVE: " + parameters.Count + " parameters; " + report["pages"]);
        StudioStrings.Apply("en"); TopSolidTheme.Apply(new(false, "Light", "Live read-only operation detail"));
        var detail = new CamOperationDetailWindow((string?)operationRow["operationName"] ?? "Operation", null, TopSolidIcons.OperationKey(operationRow), native, client, null, _ => { }, (_, _) => { })
            { Left = -20000, Top = -20000, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
        detail.Show(); await detail.Loading; await Layout(detail);
        try
        {
            var pages = Descendants<ListBox>(detail).Single(c => c.Name == "DetailPages");
            for (var i = 1; i < 8; i++) { pages.SelectedIndex = i; await Layout(detail); Render(detail, "live-" + CamOperationDetails.Pages[i] + ".png"); }
        }
        finally { detail.Close(); }
    }
    private static TextBox Editor(Window window, string name) => Descendants<TextBox>(window).Single(c => AutomationProperties.GetName(c).StartsWith(name, StringComparison.Ordinal));
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); if (child is T typed) yield return typed; foreach (var descendant in Descendants<T>(child)) yield return descendant; }
    }
    private static async Task Layout(Window window) { window.UpdateLayout(); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); }
    private static void Render(Window window, string name)
    {
        var target = (FrameworkElement)window.Content; target.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(target.ActualWidth), (int)Math.Ceiling(target.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual(); using (var drawing = background.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(0, 0, target.ActualWidth, target.ActualHeight));
        bitmap.Render(background); bitmap.Render(target); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(Output, name)); encoder.Save(stream);
    }
    private sealed class Client : IConfirmableMcpClient, IGraphicPreviewClient
    {
        public Task<JObject> GetGraphicPreviewAsync(JObject target, CancellationToken token) => Task.FromResult(new JObject { ["available"] = false, ["message"] = "Fixture preview unavailable" });
        internal List<JObject> Rows = Fixture(); internal List<JObject> Writes = []; internal int ListCalls; internal bool BrokenPaging;
        public bool IsConnected => true;
        public IReadOnlyList<McpToolDefinition> Tools { get; } = [new() { Name = "topsolid_get_cam_operation_info", Annotations = new JObject { ["readOnlyHint"] = true } }, new() { Name = CamOperationDetails.ListTool, Annotations = new JObject { ["readOnlyHint"] = true } },
            new() { Name = CamParameterEditRequest.ReadTool, Annotations = new JObject { ["readOnlyHint"] = true } }, new() { Name = CamParameterEditRequest.WriteTool, Annotations = new JObject { ["readOnlyHint"] = false } }];
        public Task<McpToolResult> CallToolAsync(string name, JObject args, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); JObject data;
            if (name == "topsolid_get_cam_operation_info") return Task.FromResult(new McpToolResult { StructuredContent = new JObject { ["operation"] = new JObject { ["element"] = args["element"]!.DeepClone() }, ["operationName"] = "Pocket finishing" } });
            if (name == CamOperationDetails.ListTool)
            {
                ListCalls++; var offset = (int)args["offset"]!; var items = Rows.Skip(offset).Take(5).ToArray(); var next = offset + items.Length;
                data = new JObject { ["items"] = new JArray(items.Select(r => r.DeepClone())), ["offset"] = offset, ["total"] = Rows.Count,
                    ["hasMore"] = next < Rows.Count, ["nextOffset"] = BrokenPaging ? offset : next, ["operation"] = new JObject { ["element"] = args["element"]!.DeepClone() } };
            }
            else { data = (JObject)Rows.Single(r => (string?)r["name"] == (string?)args["name"]).DeepClone(); data["formula"] = JValue.CreateNull(); }
            return Task.FromResult(new McpToolResult { StructuredContent = data });
        }
        public Task<JObject> PrepareToolAsync(string name, JObject args, CancellationToken token) => Task.FromResult(new JObject { ["toolName"] = name, ["arguments"] = args.DeepClone(), ["target"] = new JObject { ["name"] = "Operation" }, ["confirmationToken"] = "fixture" });
        public Task<McpToolResult> CallConfirmedToolAsync(string name, JObject args, string confirmationToken, CancellationToken token)
        {
            Assert(confirmationToken == "fixture", "Missing confirmation"); Writes.Add((JObject)args.DeepClone());
            var row = Rows.Single(r => (string?)r["name"] == (string?)args["name"]);
            var field = (string)args["valueType"]! switch { "Real" => "realValueSI", "Integer" => "integerValue", "Boolean" => "booleanValue", _ => "textValue" };
            row[field] = args[field]!.DeepClone(); row["displayValue"] = args[field]!.ToString();
            return Task.FromResult(new McpToolResult { StructuredContent = new JObject { ["originalDocumentId"] = args["documentId"]!.DeepClone(), ["documentId"] = "cam-rev-" + Writes.Count, ["saved"] = false } });
        }
    }
}
