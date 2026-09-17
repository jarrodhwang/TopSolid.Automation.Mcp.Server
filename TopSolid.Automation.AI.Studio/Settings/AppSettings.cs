using System.IO;
using Newtonsoft.Json;
using TopSolid.Automation.AI.Studio.AI;

namespace TopSolid.Automation.AI.Studio.Settings;

public sealed class AppSettings
{
    public const string OpenAiProvider = "OpenAI-compatible";
    public const string OllamaProvider = "Ollama";
    private string _cloudBaseUrl = "https://api.openai.com/v1";

    public string Provider { get; set; } = OpenAiProvider;
    public string CloudService { get; set; } = "openai";
    public Dictionary<string, CloudProfile> CloudProfiles { get; set; } = new(StringComparer.Ordinal);

    public void SelectCloudService(string serviceId)
    {
        var preset = CloudServices.Get(serviceId);
        if (CloudService == serviceId) return;
        CloudProfiles[CloudService] = new CloudProfile { BaseUrl = CloudBaseUrl, Model = CloudModel, ApiKey = ApiKey };
        var profile = CloudProfiles.GetValueOrDefault(serviceId) ?? new CloudProfile { BaseUrl = preset.BaseUrl };
        if (serviceId != CloudServices.Custom && EndpointValidator.KeyScope(profile.BaseUrl) != EndpointValidator.KeyScope(preset.BaseUrl))
            profile = new CloudProfile { BaseUrl = preset.BaseUrl };
        CloudService = serviceId;
        CloudBaseUrl = profile.BaseUrl;
        CloudModel = profile.Model;
        ApiKey = profile.ApiKey;
        CloudProfiles.Remove(serviceId);
    }

    public string CloudBaseUrl
    {
        get => _cloudBaseUrl;
        set
        {
            var next = value?.Trim() ?? "";
            if (!string.Equals(EndpointValidator.KeyScope(_cloudBaseUrl),
                EndpointValidator.KeyScope(next), StringComparison.Ordinal))
                ApiKey = "";
            _cloudBaseUrl = next;
        }
    }

    public string CloudModel { get; set; } = "";
    public string OllamaServerUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "";
    public const int DefaultRequestTimeoutMinutes = 15;
    public int RequestTimeoutMinutes { get; set; } = DefaultRequestTimeoutMinutes;
    public bool OllamaFastGptOss { get; set; } = true;

    public TimeSpan RequestTimeout()
    {
        if (RequestTimeoutMinutes is < 1 or > 60)
            throw new ArgumentException("AI request timeout must be between 1 and 60 minutes.");
        return TimeSpan.FromMinutes(RequestTimeoutMinutes);
    }
    public string McpServerPath { get; set; } = Path.Combine(AppContext.BaseDirectory,
        "McpServer", "TopSolid.Automation.Mcp.Server.AddIn.exe");

    public static string ResolveServerPath(string configured, string applicationDirectory)
    {
        const string serverName = "TopSolid.Automation.Mcp.Server.AddIn.exe";
        var bundled = Path.Combine(applicationDirectory, "McpServer", serverName);
        if (string.IsNullOrWhiteSpace(configured)) return bundled;
        if (!File.Exists(bundled)) return configured;
        try
        {
            var previous = new FileInfo(configured);
            // A saved absolute path into another Studio build must not pin an
            // upgraded app to that old bundled server. Standalone/custom servers stay.
            if (previous.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase) &&
                previous.Directory?.Name.Equals("McpServer", StringComparison.OrdinalIgnoreCase) == true &&
                previous.Directory.Parent is { } previousStudio &&
                File.Exists(Path.Combine(previousStudio.FullName, "TopSolid.Automation.AI.Studio.dll")))
                return bundled;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
        return configured;
    }

    // The settings store persists a DPAPI protected value scoped to CloudBaseUrl.
    // JsonIgnore prevents accidental plain-text serialization of this object.
    [JsonIgnore]
    public string ApiKey { get; set; } = "";
}

public sealed class CloudProfile
{
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "";
    [JsonIgnore] public string ApiKey { get; set; } = "";
}
