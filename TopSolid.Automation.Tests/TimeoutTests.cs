using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Settings;

namespace TopSolid.Automation.Tests;

internal static class TimeoutTests
{
    public static async Task Run()
    {
        using (var defaults = new AiHttpClient(new Uri("https://example.test/"), null, new ScriptedHandler()))
            Check.Equal(TimeSpan.FromMinutes(15), defaults.RequestTimeout, "Default wait must exceed the reported three-minute cutoff");
        var pending = new ScriptedHandler().PendingUntilCancelled();
        using (var http = new AiHttpClient(new Uri("https://example.test/"), null, pending, requestTimeout: TimeSpan.FromMilliseconds(80)))
        {
            var error = await Check.ThrowsAsync<AiProviderException>(() => http.SendAsync(HttpMethod.Get, "models", null, CancellationToken.None));
            Check.True(error.Message.Contains("Increase AI wait"), "Timeout must explain the configurable limit");
            Check.Equal(1, pending.Requests.Count, "Timeout must not silently repeat a provider request");
        }
        using var cancel = new CancellationTokenSource(80);
        using (var http = new AiHttpClient(new Uri("https://example.test/"), null, new ScriptedHandler().PendingUntilCancelled(), requestTimeout: TimeSpan.FromMinutes(30)))
            await Check.ThrowsAsync<OperationCanceledException>(() => http.SendAsync(HttpMethod.Get, "models", null, cancel.Token));

        foreach (var (model, fast, expected) in new[] { ("gpt-oss:20b", true, "low"), ("gpt-oss:20b", false, (string?)null), ("gemma4:31b", true, (string?)null) })
        {
            var handler = new ScriptedHandler().Json("{done:true,message:{role:'assistant',content:'Ready'}}");
            using var provider = ProviderFactory.Create(new AppSettings { Provider = AppSettings.OllamaProvider, OllamaModel = model, RequestTimeoutMinutes = 30, OllamaFastGptOss = fast }, handler);
            await provider.CompleteAsync([new AiMessage { Role = "user", Content = "Ready?" }], [], CancellationToken.None);
            Check.Equal(expected, (string?)handler.Requests[0].Body!["think"], "Thinking option must be family-specific and reversible");
        }
        Check.Throws<ArgumentException>(() => ProviderFactory.Create(new AppSettings { RequestTimeoutMinutes = 0 }));
        Check.Throws<ArgumentException>(() => ProviderFactory.Create(new AppSettings { RequestTimeoutMinutes = 61 }));
    }
}
