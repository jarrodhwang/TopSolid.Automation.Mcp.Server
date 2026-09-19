using System.IO;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Settings;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Connections;

public partial class TopSolidConnectionEditor : UserControl
{
    private bool loading = true;
    private CancellationTokenSource? operation;
    private TopSolidConnectionOptions saved = new();
    public Func<string> ServerPath { get; set; } = () => new AppSettings().McpServerPath;
    public bool IsWorking => operation != null;
    public string GatewayToken => TokenBox.Password;
    public event Action? WorkingChanged;
    public TopSolidConnectionEditor() { InitializeComponent(); Unloaded += (_, _) => operation?.Cancel(); }

    public void Load(AppSettings settings)
    {
        loading = true;
        saved = JsonConvert.DeserializeObject<TopSolidConnectionOptions>(JsonConvert.SerializeObject(settings.TopSolidConnection))!;
        ModeBox.SelectedValue = saved.Mode; SelectionBox.SelectedValue = saved.Selection;
        HostBox.Text = saved.Host; PortBox.Text = saved.Port.ToString(); VersionBox.Text = saved.ExpectedVersion;
        PipeBox.Text = saved.PipeName; TokenBox.Password = settings.TopSolidGatewayToken;
        InstanceBox.ItemsSource = saved.ProcessId == 0 ? Array.Empty<TopSolidInstanceInfo>() : new[] {
            new TopSolidInstanceInfo { ProcessId = saved.ProcessId, StartTimeUtcTicks = saved.StartTimeUtcTicks,
                Version = saved.ExpectedVersion, PipeName = saved.PipeName, Title = StudioStrings.Get("TsConnection.SavedInstance") } };
        if (saved.ProcessId > 0) InstanceBox.SelectedIndex = 0;
        loading = false; UpdateFields(); Feedback.Text = saved.Mode == "invalid" ? StudioStrings.Get("TsConnection.InvalidSaved") : "";
    }

    public TopSolidConnectionOptions Read(bool forDiscovery = false)
    {
        var mode = ModeBox.SelectedValue as string ?? "invalid";
        var instance = InstanceBox.SelectedItem as TopSolidInstanceInfo;
        var selection = forDiscovery ? "auto" : SelectionBox.SelectedValue as string ?? "auto";
        var result = new TopSolidConnectionOptions {
            Mode = mode, Selection = mode == "tcp" ? "auto" : selection,
            Host = HostBox.Text.Trim(), Port = mode == "local" ? 443 :
                string.IsNullOrWhiteSpace(PortBox.Text) && mode == "https" ? 443 : int.TryParse(PortBox.Text, out var port) ? port : 0,
            ExpectedVersion = VersionBox.Text.Trim(), PipeName = selection == "pipe" ? PipeBox.Text.Trim() : "",
            ProcessId = mode != "tcp" && selection == "instance" ? instance?.ProcessId ?? 0 : 0,
            StartTimeUtcTicks = mode != "tcp" && selection == "instance" ? instance?.StartTimeUtcTicks ?? 0 : 0
        };
        result.Validate();
        if (mode == "https" && (GatewayToken.Length < 32 || GatewayToken.Length > 256 ||
            !System.Text.RegularExpressions.Regex.IsMatch(GatewayToken, @"\A[a-zA-Z0-9_+/=-]+\z")))
            throw new ArgumentException(StudioStrings.Get("TsConnection.TokenRequired"));
        return result;
    }

    private void UpdateFields()
    {
        if (RemoteFields == null) return;
        var mode = ModeBox.SelectedValue as string;
        RemoteFields.Visibility = mode != "local" ? Visibility.Visible : Visibility.Collapsed;
        HttpsFields.Visibility = mode == "https" ? Visibility.Visible : Visibility.Collapsed;
        TcpHint.Visibility = mode == "tcp" ? Visibility.Visible : Visibility.Collapsed;
        LocalFields.Visibility = mode != "tcp" ? Visibility.Visible : Visibility.Collapsed;
        InstanceFields.Visibility = (string?)SelectionBox.SelectedValue == "instance" ? Visibility.Visible : Visibility.Collapsed;
        PipeFields.Visibility = (string?)SelectionBox.SelectedValue == "pipe" ? Visibility.Visible : Visibility.Collapsed;
        ScanButton.Visibility = mode != "tcp" ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading) return;
        PortBox.Text = (string?)ModeBox.SelectedValue == "tcp" ? "8090" : "443";
        InstanceBox.ItemsSource = null; Feedback.Text = ""; UpdateFields();
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!loading) { Feedback.Text = ""; UpdateFields(); } }
    private void HostChanged(object sender, TextChangedEventArgs e)
    {
        if (loading) return;
        TokenBox.Clear(); InstanceBox.ItemsSource = null; Feedback.Text = "";
    }
    private void FieldChanged(object sender, RoutedEventArgs e) { if (!loading) Feedback.Text = ""; }
    private void InstanceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loading && InstanceBox.SelectedItem is TopSolidInstanceInfo item)
        { VersionBox.Text = item.Version; Feedback.Text = item.Supported ? "" : StudioStrings.Get("TsConnection.Unsupported"); }
    }
    private async void ScanClicked(object sender, RoutedEventArgs e) => await Run(async (client, token) => {
        var selected = InstanceBox.SelectedItem as TopSolidInstanceInfo;
        var instances = await client.GetConnectionInstancesAsync(token);
        InstanceBox.ItemsSource = instances;
        InstanceBox.SelectedItem = instances.FirstOrDefault(i => i.ProcessId == selected?.ProcessId && i.StartTimeUtcTicks == selected.StartTimeUtcTicks);
        var version = VersionBox.Text;
        VersionBox.ItemsSource = instances.Select(i => i.Version).Distinct().ToArray(); VersionBox.Text = version;
        Feedback.Text = StudioStrings.Get("TsConnection.Found", instances.Count);
    }, discovery: true);

    private async void TestClicked(object sender, RoutedEventArgs e) => await Run(async (client, token) => {
        var result = await client.CallToolAsync("topsolid_get_status", new JObject(), token);
        var status = result.StructuredContent ?? result.Content.OfType<JObject>().Where(b => (string?)b["type"] == "text")
            .Select(b => { try { return JObject.Parse((string?)b["text"] ?? "{}"); } catch (JsonException) { return new JObject(); } }).FirstOrDefault() ?? new JObject();
        if (!TopSolidConnectionStatus.IsConnected(result))
        {
            var detail = (string?)status["detail"] ?? "";
            App.DiagnosticLog.Write("warning", "topsolid.connectionTest", data: status);
            var reason = detail.Contains(": ", StringComparison.Ordinal) ? detail[(detail.IndexOf(": ", StringComparison.Ordinal) + 2)..] : detail;
            Feedback.Text = StudioStrings.Get("TsConnection.Failed") + (StudioStrings.KeyForText(reason) != null ? " " + StudioStrings.Text(reason) : "");
            return;
        }
        var license = await client.GetLicenseStatusAsync(token);
        Feedback.Text = StudioStrings.Get("TsConnection.Connected", (string?)status["hostVersionText"] ?? "?", (string?)status["processId"] ?? "?") + " · " +
            StudioStrings.Get(license.CanStart ? "License.Allowed" : "License.Denied");
    }, discovery: false);

    private async Task Run(Func<StdioMcpClient, CancellationToken, Task> action, bool discovery)
    {
        if (IsWorking) return;
        try
        {
            var options = Read(discovery);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25)); operation = deadline;
            Fields.IsEnabled = ScanButton.IsEnabled = TestButton.IsEnabled = false; CancelButton.Visibility = Visibility.Visible;
            WorkingChanged?.Invoke(); Feedback.Text = StudioStrings.Get("TsConnection.Checking");
            await using var client = new StdioMcpClient();
            await client.ConnectAsync(ServerPath(), options, GatewayToken, deadline.Token);
            await action(client, deadline.Token);
        }
        catch (OperationCanceledException) { Feedback.Text = StudioStrings.Get("TsConnection.Cancelled"); }
        catch (Exception error)
        {
            App.DiagnosticLog.Write("warning", "topsolid.connectionTestFailed", error.GetType().Name);
            Feedback.Text = error is ArgumentException ? StudioStrings.Text(error.Message) : StudioStrings.Get("TsConnection.Failed");
        }
        finally
        {
            operation = null; Fields.IsEnabled = ScanButton.IsEnabled = TestButton.IsEnabled = true;
            CancelButton.Visibility = Visibility.Collapsed; WorkingChanged?.Invoke();
        }
    }
    private void CancelClicked(object sender, RoutedEventArgs e) => operation?.Cancel();
}
