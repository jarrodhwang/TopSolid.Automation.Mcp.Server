using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;

namespace TopSolid.Automation.AI.Studio.Settings;

public sealed class SettingsStore
{
    private readonly string _filePath;

    public SettingsStore(string? directory = null)
    {
        _filePath = Path.Combine(directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TopSolid.Automation.AI.Studio"), "settings.json");
    }

    public string FilePath => _filePath;
    public string? LastLoadWarning { get; private set; }

    public AppSettings Load()
    {
        LastLoadWarning = null;
        if (!File.Exists(_filePath)) return new AppSettings();
        try
        {
            if (new FileInfo(_filePath).Length > 128 * 1024)
                throw new InvalidDataException("Settings file is too large.");
            using var input = new JsonTextReader(new StringReader(File.ReadAllText(_filePath, Encoding.UTF8)))
            {
                MaxDepth = 8,
                DateParseHandling = DateParseHandling.None
            };
            var data = JObject.Load(input, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            if (input.Read()) throw new InvalidDataException("Settings contain trailing data.");
            if (data["version"]?.Type != JTokenType.Integer || data["version"]?.ToString(Formatting.None) != "1")
                throw new InvalidDataException("Unsupported settings version.");

            var settings = new AppSettings
            {
                Provider = ReadString(data, "provider") ?? AppSettings.OpenAiProvider,
                CloudBaseUrl = ReadString(data, "cloudBaseUrl") ?? "https://api.openai.com/v1",
                CloudModel = ReadString(data, "cloudModel") ?? "",
                OllamaServerUrl = ReadString(data, "ollamaServerUrl") ?? "http://localhost:11434",
                OllamaModel = ReadString(data, "ollamaModel") ?? ""
            };
            settings.McpServerPath = ReadString(data, "mcpServerPath") ?? settings.McpServerPath;
            settings.DevMode = data.Value<bool?>("devMode") ?? false;
            settings.AppearanceMode = ReadChoice(data, "appearanceMode", "topsolid", "topsolid", "system", "light", "dark");
            settings.InterfaceLanguage = ReadChoice(data, "interfaceLanguage", "system", "system", "en", "ko", "ja", "zh", "fr", "de");
            settings.ResponseLanguage = ReadChoice(data, "responseLanguage", "auto", "auto", "en", "ko", "ja", "zh", "fr", "de", "es");
            if (data["ollamaFastGptOss"]?.Type == JTokenType.Boolean) settings.OllamaFastGptOss = (bool)data["ollamaFastGptOss"]!;
            if (data["requestTimeoutMinutes"] != null)
            {
                if (data["requestTimeoutMinutes"]?.Type != JTokenType.Integer ||
                    !int.TryParse(data["requestTimeoutMinutes"]!.ToString(), out var minutes) || minutes is < 1 or > 60)
                    LastLoadWarning = "Invalid AI timeout; using 15 minutes. Other settings were preserved.";
                else settings.RequestTimeoutMinutes = minutes;
            }
            settings.CloudService = ReadString(data, "cloudService") ?? CloudServices.Infer(settings.CloudBaseUrl);
            CloudServices.Get(settings.CloudService);
            if (settings.CloudService != CloudServices.Custom &&
                CloudServices.Infer(settings.CloudBaseUrl) != settings.CloudService)
                settings.CloudService = CloudServices.Custom;
            if (data["cloudProfiles"] is JObject profiles)
            {
                if (profiles.Count > CloudServices.All.Count) throw new InvalidDataException("Too many cloud profiles.");
                foreach (var entry in profiles.Properties())
                {
                    CloudServices.Get(entry.Name);
                    if (entry.Name == settings.CloudService) continue;
                    if (entry.Value is not JObject profile) throw new InvalidDataException("Invalid cloud profile.");
                    var url = ReadString(profile, "baseUrl") ?? "";
                    EndpointValidator.Validate(url);
                    settings.CloudProfiles[entry.Name] = new CloudProfile
                    {
                        BaseUrl = url, Model = ReadString(profile, "model") ?? "",
                        ApiKey = UnprotectProfileKey(profile, url)
                    };
                }
            }
            var protectedKey = ReadString(data, "protectedApiKey");
            var keyScope = ReadString(data, "apiKeyScope");
            if (!string.IsNullOrWhiteSpace(protectedKey))
            {
                if (!string.Equals(keyScope, EndpointValidator.KeyScope(settings.CloudBaseUrl),
                    StringComparison.Ordinal))
                {
                    LastLoadWarning = "The saved API key belongs to another endpoint. Enter the key for the current cloud endpoint.";
                }
                else
                {
                    try
                    {
                        var clearBytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedKey),
                            Entropy(keyScope!), DataProtectionScope.CurrentUser);
                        try { settings.ApiKey = Encoding.UTF8.GetString(clearBytes); }
                        finally { CryptographicOperations.ZeroMemory(clearBytes); }
                    }
                    catch (Exception ex) when (ex is CryptographicException or FormatException)
                    {
                        LastLoadWarning = "The saved API key could not be decrypted for this Windows user. Enter it again.";
                    }
                }
            }
            // v0.3 accepted the native Gemini URL while sending OpenAI-format requests.
            // Rebind only this exact Google HTTPS origin/path, after decrypting the old
            // endpoint-bound key. Never transfer a key between different service hosts.
            if (EndpointValidator.KeyScope(settings.CloudBaseUrl) == "https://generativelanguage.googleapis.com/v1beta/")
            {
                var geminiKey = settings.ApiKey;
                settings.CloudBaseUrl = CloudServices.Get("gemini").BaseUrl;
                settings.CloudService = "gemini";
                settings.ApiKey = geminiKey;
                settings.CloudModel = settings.CloudModel.StartsWith("models/", StringComparison.Ordinal)
                    ? settings.CloudModel[7..] : settings.CloudModel;
                LastLoadWarning ??= "Updated the saved Gemini URL to its OpenAI-compatible endpoint. Save settings to persist this correction.";
            }
            if (settings.CloudService == "gemini")
                settings.CloudModel = OpenAiCompatibleProvider.NormalizeGeminiModel(settings.CloudModel);
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                                   or InvalidDataException or ArgumentException)
        {
            LastLoadWarning = "Settings could not be read. Defaults were loaded; the existing file was left unchanged.";
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ValidateProvider(settings.Provider);
        settings.RequestTimeout();
        CloudServices.Get(settings.CloudService);
        var cloudUrl = EndpointValidator.Validate(settings.CloudBaseUrl, "Cloud base URL");
        EndpointValidator.Validate(settings.OllamaServerUrl, "Ollama server URL", allowRemoteHttp: true);
        var keyScope = cloudUrl.AbsoluteUri;
        string? protectedKey = null;
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            var clearBytes = Encoding.UTF8.GetBytes(settings.ApiKey);
            try
            {
                protectedKey = Convert.ToBase64String(ProtectedData.Protect(clearBytes,
                    Entropy(keyScope), DataProtectionScope.CurrentUser));
            }
            finally { CryptographicOperations.ZeroMemory(clearBytes); }
        }

        var data = new JObject
        {
            ["version"] = 1,
            ["provider"] = settings.Provider,
            ["cloudService"] = settings.CloudService,
            ["cloudBaseUrl"] = settings.CloudBaseUrl.Trim(),
            ["cloudModel"] = settings.CloudModel.Trim(),
            ["ollamaServerUrl"] = settings.OllamaServerUrl.Trim(),
            ["ollamaModel"] = settings.OllamaModel.Trim(),
            ["requestTimeoutMinutes"] = settings.RequestTimeoutMinutes,
            ["ollamaFastGptOss"] = settings.OllamaFastGptOss,
            ["devMode"] = settings.DevMode,
            ["appearanceMode"] = settings.AppearanceMode,
            ["interfaceLanguage"] = settings.InterfaceLanguage,
            ["responseLanguage"] = settings.ResponseLanguage,
            ["mcpServerPath"] = settings.McpServerPath.Trim(),
            ["apiKeyScope"] = keyScope,
            ["protectedApiKey"] = protectedKey
        };
        var profiles = new JObject();
        foreach (var entry in settings.CloudProfiles)
        {
            CloudServices.Get(entry.Key);
            if (entry.Key == settings.CloudService) continue;
            var scope = EndpointValidator.Validate(entry.Value.BaseUrl).AbsoluteUri;
            profiles[entry.Key] = new JObject
            {
                ["baseUrl"] = entry.Value.BaseUrl, ["model"] = entry.Value.Model,
                ["apiKeyScope"] = scope, ["protectedApiKey"] = ProtectKey(entry.Value.ApiKey, scope)
            };
        }
        data["cloudProfiles"] = profiles;

        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var temporaryPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            {
                var bytes = Encoding.UTF8.GetBytes(data.ToString(Formatting.Indented));
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    internal static void ValidateProvider(string provider)
    {
        if (provider != AppSettings.OpenAiProvider && provider != AppSettings.OllamaProvider)
            throw new ArgumentException("Choose OpenAI-compatible or Ollama.");
    }

    private static byte[] Entropy(string scope) => SHA256.HashData(Encoding.UTF8.GetBytes(
        "TopSolid.Automation.AI.Studio/api-key/v1/" + scope));

    private static string? ProtectKey(string key, string scope)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var bytes = Encoding.UTF8.GetBytes(key);
        try { return Convert.ToBase64String(ProtectedData.Protect(bytes, Entropy(scope), DataProtectionScope.CurrentUser)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private string UnprotectProfileKey(JObject profile, string url)
    {
        var encrypted = ReadString(profile, "protectedApiKey");
        if (string.IsNullOrWhiteSpace(encrypted)) return "";
        var scope = EndpointValidator.KeyScope(url);
        if (ReadString(profile, "apiKeyScope") == scope)
        {
            try
            {
                var bytes = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), Entropy(scope), DataProtectionScope.CurrentUser);
                try { return Encoding.UTF8.GetString(bytes); }
                finally { CryptographicOperations.ZeroMemory(bytes); }
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException) { }
        }
        LastLoadWarning = "A saved cloud profile key could not be loaded for its endpoint. Enter that service's key again.";
        return "";
    }

    private static string? ReadString(JObject data, string name) => data[name]?.Type switch
    {
        null or JTokenType.Null => null,
        JTokenType.String => data.Value<string>(name),
        _ => throw new InvalidDataException("Settings contain an invalid value type.")
    };

    private static string ReadChoice(JObject data, string name, string fallback, params string[] allowed)
    {
        var value = ReadString(data, name)?.ToLowerInvariant();
        return value != null && allowed.Contains(value, StringComparer.Ordinal) ? value : fallback;
    }
}
