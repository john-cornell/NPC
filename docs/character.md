# Character model

Runtime types for a single NPC live in `NPC.Library/Character/`.

## Character

[`Character.cs`](../NPC.Library/Character/Character.cs) is the **runtime aggregate** for one NPC. It currently exposes:

| Member | Type | Description |
|--------|------|-------------|
| `Drives` | `Drives` | Mutable innate drive levels |

More properties will be added here as the model grows (state machine, traits, memory, and so on).

Do not construct `Character` directly. Use [`CharacterFactory`](../NPC.Library/Character/CharacterFactory.cs).

## CharacterFactory

[`CharacterFactory.cs`](../NPC.Library/Character/CharacterFactory.cs) is the **spawn entry point**. All new characters should be created through it so future subsystems are assembled in one place.

```csharp
var factory = new CharacterFactory();

// Universal drives at baseline (see DriveDefaults)
Character npc = factory.Create();

// Baseline plus overrides (e.g. already thirsty at spawn)
Character npc2 = factory.Create(new Dictionary<DriveType, decimal>
{
    [DriveType.Satiety] = 0.6m,
    [DriveType.Thirst] = 0.3m,
});
```

Overload summary:

| Method | Result |
|--------|--------|
| `Create()` | `Character` with universal drives at [`DriveDefaults.BaselineLevels`](../NPC.Library/Character/DriveDefaults.cs) |
| `Create(IEnumerable<KeyValuePair<DriveType, decimal>>)` | Same, with listed drives overridden |

## DriveDefaults

[`DriveDefaults.cs`](../NPC.Library/Character/DriveDefaults.cs) defines what **every standard character** starts with:

| Member | Role |
|--------|------|
| `Universal` | `DriveType` values always initialized on spawn |
| `BaselineLevels` | Starting level for each universal drive |
| `CreateBaseline()` | Builds a `Drives` instance from those levels |
| `CreateBaseline(overrides)` | Baseline, then applies overrides |

Default baseline: **satiety 1.0** (fully fueled), **all pressures 0** (no active urge).

Not every future `DriveType` must be universal. When you add optional or species-specific drives, add them to the enum but **omit** them from `Universal` until a specialized factory path should set them.

```csharp
Drives drives = DriveDefaults.CreateBaseline();
// or use CharacterFactory.Create() which does the same for the character aggregate
```

## Drives

[`Drives.cs`](../NPC.Library/Character/Drives.cs) holds **current levels** for innate drives. Values are `decimal`. Exact scale is left to the simulation.

### Drive semantics

Most drives are **pressures** (higher = stronger urge). One drive is a **reserve**:

| Kind | Drives | High value means |
|------|--------|------------------|
| Reserve | `Satiety` | Well fueled — plenty of energy available to spend |
| Pressure | `Thirst`, `Fatigue`, `Social`, `Safety`, `Comfort` | Stronger need or wear |

### Satiety vs Fatigue

These are intentionally separate:

| Drive | What it models | Example |
|-------|----------------|---------|
| **Satiety** | Metabolic fuel — how satisfied/fueled the body is (eating/rest restores it) | Low satiety → seek food or sleep, even if you have not run far |
| **Fatigue** | Exertion wear — tired because you have **been doing too much** | High fatigue → rest, even if you recently ate |

You can be **low on satiety** (need calories) while **low on fatigue** (not worn out from activity), or **high satiety** but **high fatigue** after a sprint. Simulation code should adjust them independently.

Pronunciation: **satiety** is usually *SUH-ty-uh-tee* (like “society” without the first syllable). **Satiation** is the related process of becoming satisfied.

### DriveType

[`DriveType.cs`](../NPC.Library/Character/DriveType.cs) lists built-in drives:

| Value | Kind | Typical meaning |
|-------|------|-----------------|
| `Satiety` | Reserve | How fueled/satisfied the body is (low → need sustenance) |
| `Thirst` | Pressure | Need for water |
| `Fatigue` | Pressure | Exhaustion from activity |
| `Social` | Pressure | Need for interaction |
| `Safety` | Pressure | Need for security |
| `Comfort` | Pressure | Need for pleasant conditions |

Extend the enum as new innate drives are identified.

### API behavior

| Operation | Behavior |
|-----------|----------|
| Indexer `this[drive]` get | Returns stored level, or **0** if unset |
| Indexer set / `SetLevel` | Stores level |
| `Adjust(drive, delta)` | Adds delta to current level (including implicit 0) |
| `Levels` | Read-only view of drives that have been set |
| `TryGetLevel` | `false` if drive was never set (universal drives are set at spawn via `DriveDefaults`) |
| `Clear` / `ClearAll` | Remove stored entries |
| `Clone()` | Deep copy for snapshots or branches |

Constructors:

- `new Drives()` — empty (prefer `DriveDefaults.CreateBaseline()` for characters)
- `new Drives(initialLevels)` — from key/value pairs
- `new Drives(other)` — copy from another instance

### Example (simulation tick)

```csharp
Character npc = factory.Create();

// Metabolic drain over time
npc.Drives.Adjust(DriveType.Satiety, -0.05m);
if (npc.Drives[DriveType.Satiety] < 0.25m)
{
    // Seek food or long rest
}

// Running adds exertion fatigue (independent of satiety)
npc.Drives.Adjust(DriveType.Fatigue, 0.1m);
if (npc.Drives[DriveType.Fatigue] > 0.75m)
{
    // Stop and recover
}
```

## Design notes

- **No drive profiles yet** — starting levels are passed directly to `CharacterFactory.Create` or set on `Drives` after spawn. Immutable archetype/profile types can return when spawn involves many subsystems or external data.
- **Factory vs profiles** — the factory is in place because `Character` will gain more parts; profiles are deferred until that complexity warrants them.
