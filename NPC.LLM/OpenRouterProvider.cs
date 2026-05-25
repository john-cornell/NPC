namespace NPC.LLM;

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

public class OpenRouterProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly LLMConfig _config;

    public OpenRouterProvider(LLMConfig config, HttpClient? httpClient = null)
    {
        _config = config;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> GenerateResponseAsync(LLMRequest request)
    {
        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            throw new InvalidOperationException("API Key is required for OpenRouter.");
        }

        var url = string.IsNullOrEmpty(_config.BaseUrl) 
            ? "https://openrouter.ai/api/v1/chat/completions" 
            : _config.BaseUrl;

        var payload = new
        {
            model = string.IsNullOrEmpty(request.Model) ? _config.ModelName : request.Model,
            messages = request.Messages.Select(m => new
            {
                role = m.Role.ToString().ToLower(),
                content = m.Content
            }).ToArray(),
            temperature = request.Temperature,
            max_tokens = request.MaxTokens
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");
        httpRequest.Headers.Add("HTTP-Referer", "https://github.com/NPC-Project"); // Required by OpenRouter

        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);
        
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
