# Overview

## Purpose

NPC is a C# library for **automated NPC characters**: simulated agents whose behavior is driven by internal state (innate drives, and eventually state machines) rather than scripted paths alone. The aim is **relatively simple application code** with room for **emergent behavior** as drives interact with the world and with each other over time.

## Solution structure

| Item | Description |
|------|-------------|
| `NPC.sln` | Solution |
| `NPC.Library` | Class library targeting **.NET 9** |

Future projects (hosts, tools, tests) can be added to the solution alongside the library.

## Namespace layout

Code lives under `NPC.Library.*`. Subfolders mirror domains:

| Folder | Namespace | Role |
|--------|-----------|------|
| `Character/` | `NPC.Library.Character` | What defines and holds one NPC’s runtime state |

More folders will appear as subsystems are added (for example state machines, perception, actions).

## Conventions

### Character construction

- **`Character`** is the runtime aggregate for one NPC.
- Create instances only via **`CharacterFactory`** so new subsystems can be wired at spawn in one place.
- **`Character`** uses an internal constructor; the factory is the supported entry point.

### Drive levels

- Drive levels use **`decimal`** for stable arithmetic across many simulation ticks (see [character.md](character.md)).
- Missing drives are treated as **0** when read.
- Normalization (for example clamping to 0–1) is not enforced yet; callers decide ranges.

### Versioning

- Library version is set in `NPC.Library/NPC.Library.csproj` (`<Version>`).

## Current scope

Implemented today:

- Innate **drives** on a **character**, created through a **factory** with a universal baseline via **`DriveDefaults`**.

Planned (not built yet):

- State machines for behavior selection
- Additional character facets beyond drives
- Profiles or data-driven archetypes when spawn logic grows

See [character.md](character.md) for the current character model.
