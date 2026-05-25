# NPC Architecture & Decision Loop

The NPC Simulation relies on a prioritized state machine driven by biological needs (Drives) and context-aware actions (Actuators).

## Core Action Selection Loop

Below is a diagram of the core decision loop that runs every simulation tick. This diagram highlights the recent fix to the persistence lock, which ensures characters do not starve while gathering food.

```mermaid
flowchart TD
    Start[Tick Character] --> CheckDrives[Evaluate Drives]
    
    CheckDrives --> |Satiety < 80%| SetHungry[TargetDrive = Satiety]
    CheckDrives --> |Thirst < 80%| SetThirsty[TargetDrive = Thirst]
    CheckDrives --> |All >= 80%| SetIdle[TargetDrive = Idle]

    SetHungry --> HasActive{Active Actuator?}
    SetThirsty --> HasActive
    SetIdle --> HasActive

    HasActive -->|Yes| CanExecute{Can Execute?}
    HasActive -->|No| SelectNew

    CanExecute -->|Yes| ExecActive[Execute Active]
    
    %% The Persistence Lock Break
    CanExecute -->|No| LockBroken[Lock Broken\nActiveActuator = null]
    LockBroken --> SelectNew

    SelectNew[Query IActionResolver for Available Actuators] --> Filter[Filter out CanExecute == false]
    Filter --> Prioritize[IActionSelector checks GetPriority]
    Prioritize --> Pick[Pick Highest Priority Actuator]
    
    Pick --> IsPersistent{Is Persistent?}
    IsPersistent -->|Yes| SaveActive[Save as ActiveActuator]
    IsPersistent -->|No| ClearActive[Clear ActiveActuator]
    
    SaveActive --> ExecNew[Execute New]
    ClearActive --> ExecNew

    style LockBroken fill:#ffb3b3,stroke:#cc0000,stroke-width:2px
```

### Breaking the Persistence Lock (The Apple Hoarding Bug)
Previously, `GatherFood` was persistent. If a character became hungry, they selected `GatherFood`. Once they had an apple, `GatherFood` remained active and capable of executing, meaning they just kept gathering apples forever until they starved to death. 

To fix this, we updated `GatherFoodActuator.CanExecute()` to return `false` if `TargetDrive == Satiety` AND the character already has an apple. This triggers the **Lock Broken** state in the diagram above, forcing the State Machine to re-evaluate and select the higher priority `EatActuator`.

## Drive Decay System

Drives passively decay over time inside the `SimulationEngine`.

```mermaid
flowchart LR
    Tick[Engine Tick] --> ApplyDecay[Apply Passive Decay]
    
    ApplyDecay --> Sat[Satiety -= 0.05 / tick]
    ApplyDecay --> Thi[Thirst -= 0.025 / tick]
    ApplyDecay --> Fat[Fatigue += 0.025 / tick]
    
    Sat --> SatCheck{Satiety <= 0?}
    Thi --> ThiCheck{Thirst <= 0?}
    Fat --> FatCheck{Fatigue >= 1?}
    
    SatCheck -->|Yes| DieSat[Die of Starvation]
    ThiCheck -->|Yes| DieThi[Die of Dehydration]
    FatCheck -->|Yes| DieFat[Die of Exhaustion]
    
    style DieSat fill:#2d2d2d,stroke:#fff,color:#fff
    style DieThi fill:#2d2d2d,stroke:#fff,color:#fff
    style DieFat fill:#2d2d2d,stroke:#fff,color:#fff
```

## Per-Character Logging Architecture

A newly introduced `CharacterLoggerComponent` allows individual characters to write out their decision matrices to isolated log files.

```mermaid
sequenceDiagram
    participant Engine as SimulationEngine
    participant SM as StateMachine
    participant Char as Character
    participant Log as CharacterLoggerComponent (File)
    
    Engine->>Char: ApplyPassiveDecay()
    Engine->>Log: UpdateTick(currentTick)
    
    Engine->>SM: TickAsync(Character)
    SM->>Char: Evaluate Drives
    Char-->>SM: TargetDrive = Satiety
    SM->>Log: Log("TargetDrive changed to Satiety")
    
    SM->>SM: PriorityActionSelector.Select()
    SM->>Log: Log("Evaluated: [Eat(100), Gather(50)]. Chose: Eat")
    
    SM->>Char: EatActuator.ExecuteAsync()
    Char-->>SM: Done
```
