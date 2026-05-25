namespace NPC.Application;

using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Character.Components;
using NPC.Library.Messaging;
using NPC.Library.Spatial;
using NPC.Library.Spatial.Grid;
using NPC.LLM;
using NPC.Village.Memory;

public class LLMReasoningService
{
    private readonly MessageDispatcher _dispatcher;
    private readonly UIState _uiState;
    private readonly ISpatialContext _spatialContext;

    public LLMReasoningService(MessageDispatcher dispatcher, UIState uiState, ISpatialContext spatialContext)
    {
        _dispatcher = dispatcher;
        _uiState = uiState;
        _spatialContext = spatialContext;

        _dispatcher.Subscribe<ActuatorChangedMessage>(OnActuatorChanged);
        _dispatcher.Subscribe<CharacterDiedMessage>(OnCharacterDied);
        _dispatcher.Subscribe<CharacterSocializingMessage>(OnCharacterSocializing);
    }

    private void OnActuatorChanged(ActuatorChangedMessage message)
    {
        // 1. Check for LLM Assignment Hierarchy
        LLMConfig? config = null;
        if (message.Character.TryGetComponent<LLMComponent>(out var llmComp))
        {
            // If they have an individual override, we respect it strictly.
            // If it's disabled, we stop here (do not fall back to global).
            if (!llmComp.Config.IsEnabled) return;
            config = llmComp.Config;
        }
        else if (_uiState.GlobalLLMConfig.IsEnabled && _uiState.GlobalLLMConfig.Provider != ProviderType.None)
        {
            // Fallback to global if they have no override
            config = _uiState.GlobalLLMConfig;
        }

        if (config == null || config.Provider == ProviderType.None)
        {
            return;
        }

        // Fire off background task so we don't block the Simulation Tick
        Task.Run(() => QueryReasoningAsync(message.Character, message.OldActuatorName, message.NewActuatorName, config));

        // Check for goodbye
        if (message.OldActuatorName.Contains("Socialize") && !message.NewActuatorName.Contains("Socialize"))
        {
            if (message.Character.TryGetComponent<NPC.Library.Character.Components.ConversationComponent>(out var convComp) && convComp.Target != null)
            {
                var target = convComp.Target;
                convComp.Target = null; // Clear it so we don't say goodbye again
                Task.Run(() => QuerySocializingAsync(message.Character, target, config));
            }
        }
    }

    private void OnCharacterDied(CharacterDiedMessage message)
    {
        // 1. Check for LLM Assignment Hierarchy
        LLMConfig? config = null;
        if (message.Character.TryGetComponent<LLMComponent>(out var llmComp))
        {
            if (!llmComp.Config.IsEnabled) return;
            config = llmComp.Config;
        }
        else if (_uiState.GlobalLLMConfig.IsEnabled && _uiState.GlobalLLMConfig.Provider != ProviderType.None)
        {
            config = _uiState.GlobalLLMConfig;
        }

        if (config == null || config.Provider == ProviderType.None)
        {
            return;
        }

        // Fire off background task so we don't block the Simulation Tick
        Task.Run(() => QueryDeathReasoningAsync(message.Character, message.Reason, config));
    }

    private void OnCharacterSocializing(CharacterSocializingMessage message)
    {
        LLMConfig? config = null;
        if (message.Character.TryGetComponent<LLMComponent>(out var llmComp))
        {
            if (!llmComp.Config.IsEnabled) return;
            config = llmComp.Config;
        }
        else if (_uiState.GlobalLLMConfig.IsEnabled && _uiState.GlobalLLMConfig.Provider != ProviderType.None)
        {
            config = _uiState.GlobalLLMConfig;
        }

        if (config == null || config.Provider == ProviderType.None)
        {
            return;
        }

        Task.Run(() => QuerySocializingAsync(message.Character, message.Target, config));
    }

    private string GetFriendlyActuatorName(string className)
    {
        var name = className.Replace("Village", "").Replace("Actuator", "");
        return string.Join(" ", System.Text.RegularExpressions.Regex.Split(name, @"(?<!^)(?=[A-Z])"));
    }

    private async Task QueryReasoningAsync(Character character, string oldActuator, string newActuator, LLMConfig config)
    {
        try
        {
            if (!character.TryGetComponent<NarrativeComponent>(out var narrative))
            {
                narrative = new NarrativeComponent();
                character.AddComponent(narrative);
            }

            var friendlyNewActuator = GetFriendlyActuatorName(newActuator);
            var friendlyOldActuator = GetFriendlyActuatorName(oldActuator);

            // Collate Context
            var drivesStr = string.Join(", ", character.Drives.Levels.Select(d => $"{d.Key}: {d.Value:F2}"));
            
            string itemsStr = "None";
            if (character.TryGetComponent<NPC.Library.Inventory.IInventory>(out var inv))
            {
                itemsStr = string.Join(", ", inv.GetItems().Select(i => i.Type.ToString()));
            }

            string chestStr = "None";
            if (character.TryGetComponent<NPC.Library.Memory.IMemory>(out var memory) && memory is VillageMemory villageMem)
            {
                var chestLocs = villageMem.Recall(NPC.Library.Spatial.Grid.TileType.Chest).ToList();
                if (chestLocs.Any())
                {
                    var chestLoc = chestLocs.First();
                    if (_spatialContext is GridSpatialContext gridCtx && gridCtx.Map.Chests.TryGetValue(chestLoc, out var chestInv))
                    {
                        var chestItems = chestInv.GetItems();
                        if (chestItems.Any())
                        {
                            chestStr = string.Join(", ", chestItems.Select(i => i.Type.ToString()));
                        }
                        else
                        {
                            chestStr = "Empty";
                        }
                    }
                }
            }

            var historyJson = JsonSerializer.Serialize(narrative.GetHistory().Select(h => new { action = h.Action, reason = h.Reason }));

            string prompt = $@"You are a human simulated as an NPC in a survival village. Your name is {character.Name}.
Your brain has subconsciously decided to change your action from '{friendlyOldActuator}' to '{friendlyNewActuator}'.
Even though you didn't consciously make this decision, you must now justify why you decided to do this.

Here is your current state:
- Drives: {drivesStr}
- Inventory Items: {itemsStr}
- Chest Items (at home): {chestStr}

Here is your recent memory of actions and the reasons you did them:
{historyJson}

Please invent a short, in-character justification for why you just decided to change your action to '{friendlyNewActuator}'. Keep your answer to ONE short sentence. Do not include your name or quotes.
";

            var provider = LLMProviderFactory.Create(config);
            var request = new LLMRequest
            {
                Model = config.ModelName,
                Temperature = config.Temperature,
                MaxTokens = config.MaxTokens,
                Messages = new System.Collections.Generic.List<ChatMessage>
                {
                    new ChatMessage { Role = ChatRole.System, Content = "You are an NPC. Provide exactly one sentence of justification." },
                    new ChatMessage { Role = ChatRole.User, Content = prompt }
                }
            };

            var response = await provider.GenerateResponseAsync(request);
            
            // Log to narrative history
            var finalResponse = response.Trim();
            narrative.AddRecord(friendlyNewActuator, finalResponse);
            
            // Fire message for external loggers
            _dispatcher.DispatchImmediate(new LLMReasoningGeneratedMessage(character, friendlyNewActuator, finalResponse));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LLM Service Error] {ex.Message}");
        }
    }

    private async Task QueryDeathReasoningAsync(Character character, string deathReason, LLMConfig config)
    {
        try
        {
            if (!character.TryGetComponent<NarrativeComponent>(out var narrative))
            {
                narrative = new NarrativeComponent();
                character.AddComponent(narrative);
            }

            var historyJson = JsonSerializer.Serialize(narrative.GetHistory().Select(h => new { action = h.Action, reason = h.Reason }));

            string prompt = $@"You are a human simulated as an NPC in a survival village. Your name is {character.Name}.
You have just DIED tragically from {deathReason}.

Here is the memory of your final moments and the reasons you took those actions:
{historyJson}

Please provide your final words. Comment on your life, your struggles, and how it all ended.
Keep it to one or two sentences, be dramatic, humorous, or poignant. Do not include your name or quotes.";

            var provider = LLMProviderFactory.Create(config);
            var request = new LLMRequest
            {
                Model = config.ModelName,
                Temperature = config.Temperature,
                MaxTokens = config.MaxTokens,
                Messages = new System.Collections.Generic.List<ChatMessage>
                {
                    new ChatMessage { Role = ChatRole.System, Content = "You are an NPC giving your final words." },
                    new ChatMessage { Role = ChatRole.User, Content = prompt }
                }
            };

            var response = await provider.GenerateResponseAsync(request);
            
            var finalResponse = response.Trim();
            narrative.AddRecord("DIED", finalResponse);
            
            _dispatcher.DispatchImmediate(new LLMReasoningGeneratedMessage(character, "Last Words", finalResponse));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LLM Service Death Error] {ex.Message}");
        }
    }

    private async Task QuerySocializingAsync(Character character, Character target, LLMConfig config)
    {
        try
        {
            if (!character.TryGetComponent<NarrativeComponent>(out var narrative))
            {
                narrative = new NarrativeComponent();
                character.AddComponent(narrative);
            }

            if (!character.TryGetComponent<NPC.Library.Character.Components.ConversationComponent>(out var convComp))
            {
                convComp = new NPC.Library.Character.Components.ConversationComponent();
                character.AddComponent(convComp);
            }
            convComp.Target = target;

            var historyJson = JsonSerializer.Serialize(narrative.GetHistory().Select(h => new { action = h.Action, reason = h.Reason }));

            bool isStillSocializing = character.LastAction?.StartsWith("Socializing") == true;
            string prompt;

            if (isStillSocializing)
            {
                prompt = $@"You are a human simulated as an NPC in a survival village. Your name is {character.Name}.
You are currently having a conversation with {target.Name}.

Here is your recent memory of actions and thoughts (including what was just said):
{historyJson}

Please generate exactly ONE short sentence of spoken dialogue you say to {target.Name} in response or to continue the conversation. Keep it conversational, in-character, and relevant. Do not include your name or quotes.";
            }
            else
            {
                prompt = $@"You are a human simulated as an NPC in a survival village. Your name is {character.Name}.
You were just talking to {target.Name}, but you had to leave to do something else (your current action is {character.LastAction}).

Here is your recent memory:
{historyJson}

Please generate exactly ONE short sentence of spoken dialogue saying a final goodbye to {target.Name} before you leave. Keep it brief. Do not include your name or quotes.";
            }

            var provider = LLMProviderFactory.Create(config);
            var request = new LLMRequest
            {
                Model = config.ModelName,
                Temperature = config.Temperature,
                MaxTokens = config.MaxTokens,
                Messages = new System.Collections.Generic.List<ChatMessage>
                {
                    new ChatMessage { Role = ChatRole.System, Content = "You are an NPC. Provide exactly one sentence of spoken dialogue." },
                    new ChatMessage { Role = ChatRole.User, Content = prompt }
                }
            };

            var response = await provider.GenerateResponseAsync(request);
            
            var finalResponse = response.Trim().Trim('"');
            narrative.AddRecord($"Speaking to {target.Name}", finalResponse);
            
            // Also add what we said to the target's memory so it shows up in their UI
            if (target.TryGetComponent<NarrativeComponent>(out var targetNarrative))
            {
                targetNarrative.AddRecord($"Heard from {character.Name}", finalResponse);
            }

            _dispatcher.DispatchImmediate(new DialogueGeneratedMessage(character, finalResponse));

            // Ping-pong the conversation!
            // Wait a few seconds for pacing, then if they are still socializing, trigger the other character
            await Task.Delay(3000); // Wait 3 seconds before the other character replies
            if (target.LastAction?.StartsWith("Socializing") == true && character.LastAction?.StartsWith("Socializing") == true)
            {
                // Trigger the target to reply to the character
                _dispatcher.DispatchImmediate(new CharacterSocializingMessage(target, character));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LLM Service Socializing Error] {ex.Message}");
        }
    }
}
