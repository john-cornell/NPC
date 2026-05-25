namespace NPC.LLM;

using System.Collections.Generic;

/// <summary>
/// A standardized chat message role.
/// </summary>
public enum ChatRole
{
    System,
    User,
    Assistant
}

/// <summary>
/// A single message in the conversation history.
/// </summary>
public class ChatMessage
{
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;

    public ChatMessage() { }
    public ChatMessage(ChatRole role, string content)
    {
        Role = role;
        Content = content;
    }
}

/// <summary>
/// A request payload to send to an LLM provider.
/// </summary>
public class LLMRequest
{
    /// <summary>
    /// The conversation history, usually ending with the latest user prompt.
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// Model name to use (e.g., 'gpt-3.5-turbo', 'llama3', 'claude-3-haiku').
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Optional parameters.
    /// </summary>
    public float? Temperature { get; set; }
    
    public int? MaxTokens { get; set; }
}
