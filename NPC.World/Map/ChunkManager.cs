namespace NPC.World.Map;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

public interface IChunkGenerator
{
    void GenerateChunk(MapChunk chunk);
}

public class ChunkManager
{
    private readonly ConcurrentDictionary<(int X, int Y, int Z), MapChunk> _activeChunks = new();
    private readonly IChunkGenerator? _generator;

    public ChunkManager(IChunkGenerator? generator = null)
    {
        _generator = generator;
    }

    // In a real implementation, this would read/write from a SQLite DB or JSON files in WorldData.
    public MapChunk LoadChunk(int x, int y, int z, DateTime currentSimulationTime)
    {
        var key = (x, y, z);
        if (_activeChunks.TryGetValue(key, out var chunk))
        {
            return chunk;
        }

        // Simulate loading from disk
        chunk = new MapChunk(x, y, z);
        _generator?.GenerateChunk(chunk);
        
        // Calculate catch-up time
        if (chunk.LastSavedTime.HasValue)
        {
            var elapsed = currentSimulationTime - chunk.LastSavedTime.Value;
            chunk.TriggerReloaded(elapsed);
        }

        _activeChunks[key] = chunk;
        return chunk;
    }

    public void UnloadChunk(int x, int y, int z, DateTime currentSimulationTime)
    {
        var key = (x, y, z);
        if (_activeChunks.TryRemove(key, out var chunk))
        {
            chunk.LastSavedTime = currentSimulationTime;
            // Simulate saving to disk
        }
    }

    public IEnumerable<MapChunk> GetActiveChunks() => _activeChunks.Values;
}

public class MapChunk
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }
    public DateTime? LastSavedTime { get; set; }

    public NPC.Library.Spatial.Grid.TileType[,] Tiles = new NPC.Library.Spatial.Grid.TileType[100, 100];
    public Dictionary<(int X, int Y), NPC.Library.Inventory.StandardInventory> Chests = new();
    public Dictionary<(int X, int Y), int> TreeApples = new();
    public Dictionary<(int X, int Y), List<NPC.Library.Inventory.IItem>> GroundItems = new();

    public MapChunk(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
        
        // Generate grass for now
        for(int cy = 0; cy < 100; cy++)
        {
            for(int cx = 0; cx < 100; cx++)
            {
                Tiles[cx, cy] = NPC.Library.Spatial.Grid.TileType.Grass;
            }
        }
    }

    public void TriggerReloaded(TimeSpan elapsed)
    {
        // Entities (like trees) within this chunk would calculate respawns based on `elapsed`.
        OnChunkReloaded?.Invoke(this, elapsed);
    }

    public event EventHandler<TimeSpan>? OnChunkReloaded;
}
