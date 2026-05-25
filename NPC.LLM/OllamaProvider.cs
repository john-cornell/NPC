namespace NPC.LLM;

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

public class OllamaProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly LLMConfig _config;

    public OllamaProvider(LLMConfig config, HttpClient? httpClient = null)
    {
        _config = config;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> GenerateResponseAsync(LLMRequest request)
    {
        var url = string.IsNullOrEmpty(_config.BaseUrl) 
            ? "http://localhost:11434/api/chat" 
            : _config.BaseUrl;

        var payload = new
        {
            model = string.IsNullOrEmpty(request.Model) ? _config.ModelName : request.Model,
            messages = request.Messages.Select(m => new
            {
                role = m.Role.ToString().ToLower(),
                content = m.Content
            }).ToArray(),
            stream = false,
            options = new
            {
                temperature = request.Temperature,
                num_predict = request.MaxTokens
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);
        
        return doc.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
