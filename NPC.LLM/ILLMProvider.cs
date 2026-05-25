namespace NPC.LLM;

using System.Threading.Tasks;

/// <summary>
/// Defines a unified interface for various LLM providers.
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// Connects to the LLM backend to generate a response based on the request parameters.
    /// </summary>
    Task<string> GenerateResponseAsync(LLMRequest request);
}
