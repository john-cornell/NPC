namespace NPC.Library.Character.Components;

using NPC.LLM;
using NPC.Library.Character;

/// <summary>
/// Attaches an LLM configuration to a specific character, allowing them to override
/// the global LLM settings.
/// </summary>
public class LLMComponent
{
    public LLMConfig Config { get; set; } = new LLMConfig();
}
