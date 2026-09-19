using TopSolid.Automation.AI.Studio.AI;

namespace TopSolid.Automation.Tests;

internal static class ModelIconTests
{
    public static Task Run()
    {
        Check.Equal("mistral-ai-icon.png",
            ModelIconCatalog.ResolveIconResource("Ollama", "", "mistral-small-3-24b-q4_K_M-mistral:latest"),
            "Local Mistral model icon");
        Check.Equal("qwen-ai-icon.png",
            ModelIconCatalog.ResolveIconResource("Ollama", "", "qwen2.5:latest"),
            "Local Qwen model icon");
        Check.Equal("claude-color.png",
            ModelIconCatalog.ResolveIconResource("OpenAI-compatible", "anthropic", "claude-sonnet"),
            "Cloud Claude icon");
        Check.Equal("meta-icon.png",
            ModelIconCatalog.ResolveIconResource("OpenAI-compatible", "openrouter", "meta-llama/llama-4"),
            "OpenRouter model-family icon");

        var options = ModelIconCatalog.CreateOptions(["a", "a", "b"], "Ollama", "");
        Check.Equal(2, options.Count, "Model selector should de-duplicate model IDs");
        Check.Equal("a", options[0].ToString(), "Model option ToString must preserve the API model ID");
        Check.True(options[0].IconUri.ToString().Contains("Assets/Providers/", StringComparison.Ordinal),
            "Model option icon must use the packaged provider asset");
        return Task.CompletedTask;
    }
}
