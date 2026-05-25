namespace NPC.LLM;

using System;
using System.Net.Http;
using System.Threading.Tasks;

public class GeminiProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly LLMConfig _config;

    public GeminiProvider(LLMConfig config, HttpClient? httpClient = null)
    {
        _config = config;
        _httpClient = httpClient ?? new HttpClient();
    }

    public Task<string> GenerateResponseAsync(LLMRequest request)
    {
        // TODO: Implement Gemini specific JSON structure and API endpoint
        throw new NotImplementedException("Gemini Provider is not fully implemented yet.");
    }
}
