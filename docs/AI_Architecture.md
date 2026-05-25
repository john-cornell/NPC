# AI System Architecture

This document provides a visual representation of the Persistent AI Configuration System and how the LLM reasoning is integrated into the simulation.

## Architecture Diagram

```mermaid
graph TD
    %% Configuration Layer
    subgraph Configuration
        JSON[ai_settings.json] --> |Loads on Startup| SettingsMgr(AISettingsManager)
        SettingsMgr --> GlobalConfig(Global LLM Config)
        SettingsMgr --> Overrides(Individual Overrides)
    end

    %% Entities
    subgraph Simulation
        NPC1[Character A] -.-> |Uses Global| GlobalConfig
        NPC2[Character B] --> |Uses Override| Overrides
    end

    %% Processing
    NPC1 -- ActuatorChanged / Died --> Bus{Message Dispatcher}
    NPC2 -- ActuatorChanged / Died --> Bus
    
    Bus --> LLMService(LLM Reasoning Service)
    
    %% AI Pipeline
    LLMService --> |Requires IsEnabled = true| APICloud((LLM API Provider))
    APICloud --> |Returns Justification| LLMService

    %% Logging and State
    LLMService --> |Saves to Narrative Component| State(Character Memory)
    LLMService -- Emits LLMReasoningGeneratedMessage --> Logger(Simulation Logger)
    
    Logger --> |Writes to| Disk[Docs/Logs/Run_.../]
```

### Key Components

1. **AISettingsManager**: Responsible for saving and loading the `ai_settings.json` file. It ensures your global configurations and individual overrides persist between simulation restarts.
2. **LLMReasoningService**: Runs asynchronously. It intercepts game events (like an NPC changing actions or dying) and issues a prompt to the configured LLM without blocking the main game thread.
3. **Hierarchy**: The system strictly enforces the hierarchy: `Individual Override -> Global Setting`. If an individual has an override that is explicitly disabled, it will *not* fall back to the global setting.
4. **SimulationLogger**: Captures the newly implemented `LLMReasoningGeneratedMessage` and writes the character's thoughts directly to their log file in `Docs/Logs`.
