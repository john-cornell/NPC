# NPC.World Dev Notes

## Chunk System & 3D Layering
- The `WorldMap` uses a 3D coordinate system (X, Y, Z) to future-proof for NetHack-style dungeon levels.
- Layer `Z = 0` is the surface (the original village layer).
- Positive/Negative Z can be used for sky or dungeon layers respectively, maintaining strict alignment of X,Y coords.
- Chunks are cached to disk to save RAM. When a chunk unloads, we track the timestamp.

## Catch-Up Mechanics
- When a chunk is reloaded via `ChunkManager.LoadChunk()`, the elapsed time is calculated.
- The `OnChunkReloaded(TimeSpan elapsed)` event is fired.
- **Rule**: Entities within that chunk (like Apple Trees) should hook into this event and calculate resource respawns instantly (e.g. `Apples = Min(MaxApples, Apples + elapsed.TotalHours * SpawnRate)`).

## Map Sizing
- The map is a 100x100 chunk grid, expanding the original village size by roughly 100x in each direction.
