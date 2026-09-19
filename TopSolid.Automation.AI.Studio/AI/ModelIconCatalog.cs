namespace TopSolid.Automation.AI.Studio.AI;

/// <summary>Display-only model selector data. ModelId remains the value sent to the provider.</summary>
public sealed class ChatModelOption
{
    public ChatModelOption(string modelId, string iconResource)
    {
        ModelId = modelId;
        IconResource = iconResource;
        IconUri = new Uri($"/TopSolid.Automation.AI.Studio;component/Assets/Providers/{iconResource}", UriKind.Relative);
    }

    public string ModelId { get; }
    public string IconResource { get; }
    public Uri IconUri { get; }

    public override string ToString() => ModelId;
}

/// <summary>Maps known model families and cloud services to the supplied provider artwork.</summary>
public static class ModelIconCatalog
{
    private static readonly IReadOnlyDictionary<string, string> ServiceIcons =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai"] = "openai.png",
            ["gemini"] = "gemini-color.png",
            ["anthropic"] = "claude-color.png",
            ["xai"] = "grok.png",
            ["meta"] = "meta-icon.png",
            ["mistral"] = "mistral-ai-icon.png",
            ["deepseek"] = "deepseek-color.png"
        };

    private static readonly (string[] Matches, string Resource)[] ModelFamilies =
    [
        (["deepseek"], "deepseek-color.png"),
        (["claude", "anthropic"], "claude-color.png"),
        (["gemma"], "google-gemma-ai-icon.png"),
        (["gemini"], "gemini-color.png"),
        (["qwen", "qwq"], "qwen-ai-icon.png"),
        (["mistral", "mixtral"], "mistral-ai-icon.png"),
        (["llama", "meta-llama", "meta/"], "meta-icon.png"),
        (["grok", "xai"], "grok.png"),
        (["gpt-", "gpt_", "gptoss", "gpt-oss", "openai", "o1", "o3", "o4"], "openai.png"),
        (["phi-", "phi_", "microsoft"], "microsoft-color.png"),
        (["nemotron", "nvidia"], "nvidia-color.png"),
        (["kimi", "moonshot"], "kimi-color.png"),
        (["granite", "ibm"], "ibm.png"),
        (["apple", "mlx"], "apple.png")
    ];

    public static IReadOnlyList<ChatModelOption> CreateOptions(
        IEnumerable<string> modelIds, string provider, string cloudService)
    {
        ArgumentNullException.ThrowIfNull(modelIds);
        return modelIds
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(model => model.Trim())
            .Distinct(StringComparer.Ordinal)
            .Select(model => new ChatModelOption(model, ResolveIconResource(provider, cloudService, model)))
            .ToArray();
    }

    public static string ResolveIconResource(string provider, string cloudService, string modelId)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(cloudService);
        ArgumentNullException.ThrowIfNull(modelId);

        var normalized = modelId.Trim().ToLowerInvariant();
        var inferFromModel = provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase) ||
            cloudService.Equals("custom", StringComparison.OrdinalIgnoreCase) ||
            cloudService.Equals("openrouter", StringComparison.OrdinalIgnoreCase) ||
            cloudService.Equals("groq", StringComparison.OrdinalIgnoreCase);

        if (inferFromModel)
        {
            foreach (var family in ModelFamilies)
                if (family.Matches.Any(normalized.Contains)) return family.Resource;
        }

        if (ServiceIcons.TryGetValue(cloudService, out var serviceResource)) return serviceResource;
        foreach (var family in ModelFamilies)
            if (family.Matches.Any(normalized.Contains)) return family.Resource;
        return "openai.png";
    }
}
