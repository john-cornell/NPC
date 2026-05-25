namespace NPC.LLM;

using System;
using System.Net.Http;

public static class LLMProviderFactory
{
    public static ILLMProvider Create(LLMConfig config, HttpClient? httpClient = null)
    {
        return config.Provider switch
        {
            ProviderType.Ollama => new OllamaProvider(config, httpClient),
            ProviderType.OpenAI => new OpenAIProvider(config, httpClient),
            ProviderType.OpenRouter => new OpenRouterProvider(config, httpClient),
            ProviderType.Gemini => new GeminiProvider(config, httpClient),
            ProviderType.Claude => new ClaudeProvider(config, httpClient),
            _ => throw new NotSupportedException($"Provider {config.Provider} is not supported.")
        };
    }
}
