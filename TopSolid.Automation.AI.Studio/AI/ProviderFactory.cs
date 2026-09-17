using TopSolid.Automation.AI.Studio.Settings;
using System.Net.Http;

namespace TopSolid.Automation.AI.Studio.AI;

public static class ProviderFactory
{
    public static IAiProvider Create(AppSettings settings, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var timeout = settings.RequestTimeout();
        if (settings.Provider == AppSettings.OllamaProvider)
            return new OllamaProvider(settings.OllamaServerUrl, settings.OllamaModel, handler, timeout, settings.OllamaFastGptOss);
        SettingsStore.ValidateProvider(settings.Provider);
        var service = CloudServices.Get(settings.CloudService);
        if (service.Id != CloudServices.Custom && string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new ArgumentException(service.Name + ": enter an API key before listing models or sending a message. " + service.KeyHint);
        if (Uri.TryCreate(settings.CloudBaseUrl, UriKind.Absolute, out var endpoint) &&
            endpoint.Host == "generativelanguage.googleapis.com" && endpoint.AbsolutePath.TrimEnd('/') != "/v1beta/openai")
            throw new ArgumentException("Select Google Gemini under Cloud service to use its OpenAI-compatible API URL, then enter your Gemini API key.");
        if (service.Id != CloudServices.Custom && EndpointValidator.KeyScope(settings.CloudBaseUrl) != EndpointValidator.KeyScope(service.BaseUrl))
            throw new ArgumentException("The URL does not match the selected cloud service. Select its preset again, or choose Custom OpenAI-compatible for another endpoint.");
        return service.Id switch
        {
            "anthropic" => new AnthropicProvider(settings.CloudBaseUrl, settings.ApiKey, settings.CloudModel, handler, timeout),
            _ => new OpenAiCompatibleProvider(settings.CloudBaseUrl, settings.ApiKey,
                settings.CloudModel, handler, service.KeyHint, timeout)
        };
    }
}
