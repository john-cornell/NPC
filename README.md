# NPC

Autonomous NPC simulation framework: innate biological drives, a priority-based state machine, neural-network decision making, and genetic evolution — all running on a spatial village world.

Characters have needs (hunger, thirst, fatigue, social) that decay over time. A state machine evaluates which need is most pressing, resolves available actions, and a neural network selects which action to take. Over hundreds of generations, a genetic algorithm breeds networks that learn to keep their characters alive.

## Solution

| Project | Role |
|---------|------|
| `NPC.Library` | Core simulation types: characters, drives, state machine, actuators, neural networks, spatial abstraction (.NET 9) |
| `NPC.Village` | Village-specific behaviours: gather food/water, sleep in beds, socialize, store/retrieve items, map generation |
| `NPC.Application` | High-level orchestration: `ScenarioRunner` (interactive sim), `GeneticTrainer` (headless evolution), LLM reasoning |
| `NPC.LLM` | LLM provider abstraction (OpenAI, Claude, Gemini, Ollama, OpenRouter) for narrative reasoning |
| `NPC.UI.Console` | Spectre-based terminal renderer |
| `NPC.UI.Isometric` | 2D isometric graphical renderer |

## Architecture

### Drive System

Characters have four universal drives that decay each tick inside `SimulationEngine`:

| Drive | Direction | Decay Rate | Lethal? |
|-------|-----------|------------|---------|
| Satiety | 1.0 → 0.0 | `SatietyDecayPerTick` | Yes — starvation at 0 |
| Thirst | 1.0 → 0.0 | `× 0.5` | Yes — dehydration at 0 |
| Fatigue | 0.0 → 1.0 | `× 0.5` | Yes — exhaustion at 1.0 |
| Social | 1.0 → 0.0 | `× 0.75` | No — quality-of-life only |

### Decision Loop

Each tick, the `StateMachine` picks the most urgent drive and selects an actuator:

1. **Survival drives first** — Satiety, Thirst, and Fatigue compete by lowest urgency level (below 0.8 threshold)
2. **Social only when safe** — Social can only win against Idle; it never outcompetes survival
3. **Critical lethality overrides** — If Satiety < 0.15 or Thirst < 0.15 or Fatigue > 0.85, that drive is forced regardless of current activity

The `NNActionSelector` feeds 16 inputs (drive levels, inventory state, spatial distances) into the character's neural network and picks the actuator with the highest output score.

### Genetic Training

`GeneticTrainer.RunTrainingAsync()` runs headless fast-forward simulation:

1. Generate a population of random neural networks
2. Each clone lives in a generated village map, ticking without delay
3. Fitness = ticks survived + bonus for remaining satiety/thirst + proximity bonuses (well, apple trees)
4. `GeneticEvolutionManager` breeds the next generation via elitism (top 20%) + crossover + mutation
5. Repeat for N generations — fitness should decrease from ~100 to ~3-4 as networks learn to survive

## Build

```bash
dotnet build NPC.sln
```

## Documentation

- [docs/README.md](docs/README.md) — Design docs index
- [Architecture.md](Architecture.md) — Decision loop diagrams and drive decay system
