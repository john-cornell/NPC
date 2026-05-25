namespace NPC.LLM;

public enum ProviderType
{
    None,
    Ollama,
    OpenAI,
    Gemini,
    Claude,
    OpenRouter
}

/// <summary>
/// Configuration payload detailing how to connect to an LLM provider.
/// </summary>
public class LLMConfig
{
    public bool IsEnabled { get; set; } = true;
    public ProviderType Provider { get; set; } = ProviderType.None;
    
    /// <summary>
    /// The API Key for cloud providers (OpenAI, Gemini, Claude, OpenRouter).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL if you want to override the default endpoint (especially needed for Ollama).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The specific model to target on this provider.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Controls randomness. Lower is more deterministic.
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// The maximum number of tokens to generate.
    /// </summary>
    public int MaxTokens { get; set; } = 1024;
}
