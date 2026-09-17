using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Settings;

namespace TopSolid.Automation.Tests;

internal static class SettingsTests
{
    public static Task SecureRoundTrip()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TopSolid.Automation.Tests", Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(directory);
        try
        {
            const string testKey = "test-only-secret-not-an-api-key-38e6dd";
            var original = new AppSettings
            {
                Provider = AppSettings.OllamaProvider,
                CloudBaseUrl = "https://example.test/v1", CloudModel = "cloud-test",
                OllamaServerUrl = "http://127.0.0.1:11434", OllamaModel = "local-test",
                McpServerPath = "C:\\Test\\Mcp.Server.exe", ApiKey = testKey, RequestTimeoutMinutes = 30, OllamaFastGptOss = false
            };
            store.Save(original);
            var persisted = File.ReadAllText(store.FilePath);
            Check.True(!persisted.Contains(testKey, StringComparison.Ordinal), "Persisted settings exposed the API key");
            Check.True(!JsonConvert.SerializeObject(original).Contains(testKey, StringComparison.Ordinal),
                "Generic settings serialization exposed the API key");
            Check.True(!string.IsNullOrEmpty((string?)JObject.Parse(persisted)["protectedApiKey"]), "Encrypted key was absent");
            var loaded = store.Load();
            Check.Equal(testKey, loaded.ApiKey, "DPAPI key round trip");
            Check.Equal(original.Provider, loaded.Provider, "Provider round trip");
            Check.Equal(original.CloudBaseUrl, loaded.CloudBaseUrl, "Cloud URL round trip");
            Check.Equal(original.CloudModel, loaded.CloudModel, "Cloud model round trip");
            Check.Equal(original.OllamaServerUrl, loaded.OllamaServerUrl, "Ollama URL round trip");
            Check.Equal(original.OllamaModel, loaded.OllamaModel, "Ollama model round trip");
            Check.Equal(original.McpServerPath, loaded.McpServerPath, "MCP path round trip");
            Check.Equal(30, loaded.RequestTimeoutMinutes, "Configured wait must persist");
            Check.True(!loaded.OllamaFastGptOss, "Thinking preference must persist");
            Check.True(store.LastLoadWarning == null, "Valid settings produced a warning");
            var legacy = JObject.Parse(persisted); legacy.Remove("requestTimeoutMinutes"); legacy.Remove("ollamaFastGptOss");
            File.WriteAllText(store.FilePath, legacy.ToString());
            var migrated = store.Load();
            Check.Equal(15, migrated.RequestTimeoutMinutes, "Existing settings must receive the longer default");
            Check.Equal(testKey, migrated.ApiKey, "Default migration must preserve encrypted credentials");
            legacy["requestTimeoutMinutes"] = 999;
            File.WriteAllText(store.FilePath, legacy.ToString());
            Check.Equal(15, store.Load().RequestTimeoutMinutes, "Invalid timeout must recover locally");
            Check.Equal(original.OllamaModel, store.Load().OllamaModel, "Invalid timeout must not discard the model");

            loaded.CloudBaseUrl += "/";
            Check.Equal(testKey, loaded.ApiKey, "Equivalent normalized URL should retain its API key");
            loaded.CloudBaseUrl = "https://another.example.test/v1";
            Check.Equal("", loaded.ApiKey, "Changing the cloud endpoint must clear its API key");

            var changed = JObject.Parse(persisted);
            changed["cloudBaseUrl"] = "https://another.example.test/v1";
            File.WriteAllText(store.FilePath, changed.ToString());
            Check.Equal("", store.Load().ApiKey, "Endpoint tampering must not load the old API key");
            Check.True(store.LastLoadWarning != null, "Endpoint mismatch should be reported");

            store.Save(new AppSettings());
            Check.Equal("", store.Load().ApiKey, "Clearing the key must persist");
            var oldStudio = Path.Combine(directory, "old-studio");
            var newStudio = Path.Combine(directory, "new-studio");
            Directory.CreateDirectory(Path.Combine(oldStudio, "McpServer"));
            Directory.CreateDirectory(Path.Combine(newStudio, "McpServer"));
            File.WriteAllText(Path.Combine(oldStudio, "TopSolid.Automation.AI.Studio.dll"), "test fixture");
            var oldServer = Path.Combine(oldStudio, "McpServer", "TopSolid.Automation.Mcp.Server.AddIn.exe");
            var newServer = Path.Combine(newStudio, "McpServer", "TopSolid.Automation.Mcp.Server.AddIn.exe");
            File.WriteAllText(newServer, "test fixture");
            Check.Equal(newServer, AppSettings.ResolveServerPath(oldServer, newStudio), "New Studio must use its matching bundled server");
            Check.Equal(original.McpServerPath, AppSettings.ResolveServerPath(original.McpServerPath, newStudio), "Standalone custom server paths must be preserved");
            File.WriteAllText(store.FilePath, "{malformed json");
            Check.Equal("", store.Load().ApiKey, "Malformed settings must not recover a key");
            Check.True(store.LastLoadWarning != null, "Malformed settings should produce a recovery warning");
            Check.Equal("{malformed json", File.ReadAllText(store.FilePath), "Load should preserve malformed evidence");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        return Task.CompletedTask;
    }

    public static Task EndpointSafety()
    {
        Check.Equal("https://example.test/v1/", EndpointValidator.Validate(" https://example.test/v1 ").AbsoluteUri,
            "Base URL normalization");
        Check.Equal("http://localhost:11434/", EndpointValidator.Validate("http://localhost:11434").AbsoluteUri,
            "Local Ollama HTTP URL");
        foreach (var invalid in new[] { "not-a-url", "file:///C:/key", "http://example.test/v1",
                     "https://user:secret@example.test/v1", "https://example.test/v1?key=secret",
                     "https://example.test/v1#fragment" })
            Check.Throws<ArgumentException>(() => EndpointValidator.Validate(invalid));
        return Task.CompletedTask;
    }
}
