using TopSolid.Automation.AI.Studio.Settings;

namespace TopSolid.Automation.AI.Studio.AI;

public sealed record CloudService(string Id, string Name, string BaseUrl, string KeyHint);

public static class CloudServices
{
    public const string Custom = "custom";
    public static IReadOnlyList<CloudService> All { get; } = Array.AsReadOnly(new[]
    {
        new CloudService("openai", "OpenAI", "https://api.openai.com/v1", "Use an OpenAI API key."),
        new CloudService("gemini", "Google Gemini", "https://generativelanguage.googleapis.com/v1beta/openai", "Use a Gemini API key from Google AI Studio, not an OAuth token or an OpenAI key."),
        new CloudService("anthropic", "Anthropic (Claude)", "https://api.anthropic.com/v1", "Use an Anthropic API key, not a Claude subscription login."),
        new CloudService("xai", "xAI (Grok)", "https://api.x.ai/v1", "Use an xAI inference API key."),
        new CloudService("meta", "Meta Model API", "https://api.meta.ai/v1", "Use a Meta Model API key from dev.meta.ai, not a Groq key."),
        new CloudService("groq", "Groq / Meta Llama", "https://api.groq.com/openai/v1", "Meta Llama models hosted by Groq. Use a Groq API key, not a Meta key."),
        new CloudService("mistral", "Mistral", "https://api.mistral.ai/v1", "Use a Mistral API key."),
        new CloudService("deepseek", "DeepSeek", "https://api.deepseek.com/v1", "Use a DeepSeek API key."),
        new CloudService("openrouter", "OpenRouter", "https://openrouter.ai/api/v1", "Use an OpenRouter API key for models hosted through OpenRouter."),
        new CloudService(Custom, "Custom OpenAI-compatible", "https://api.openai.com/v1", "Enter this endpoint's API key. Select a named service for its preset URL.")
    });

    public static CloudService Get(string id) => All.FirstOrDefault(x => x.Id == id)
        ?? throw new ArgumentException("Choose a supported cloud service or Custom OpenAI-compatible.");

    public static string Infer(string url) => All.FirstOrDefault(x => x.Id != Custom &&
        EndpointValidator.KeyScope(x.BaseUrl) == EndpointValidator.KeyScope(url))?.Id ?? Custom;
}
