namespace NPC.World.Map;

using System;
using System.Collections.Generic;
using System.Linq;
using NPC.Library.Character;
using NPC.Library.Spatial;

public class WorldMap : ISpatialContext
{
    public ChunkManager ChunkManager { get; }
    private readonly Dictionary<Character, (int X, int Y, int Z)> _characterLocations = new();
    
    // For now, tracking time internally or via an injected service.
    private DateTime _simulationTime = DateTime.MinValue;
    public DateTime CurrentTime => _simulationTime;

    public WorldMap(ChunkManager chunkManager)
    {
        ChunkManager = chunkManager ?? throw new ArgumentNullException(nameof(chunkManager));
    }

    public void UpdateSimulationTime(DateTime newTime)
    {
        _simulationTime = newTime;
    }

    public IEnumerable<(int X, int Y, int Z)> GetPath((int X, int Y, int Z) start, (int X, int Y, int Z) target)
    {
        if (start == target) return new List<(int X, int Y, int Z)>();

        int curX = start.X;
        int curY = start.Y;
        int curZ = start.Z;

        if (curX < target.X) curX++;
        else if (curX > target.X) curX--;

        if (curY < target.Y) curY++;
        else if (curY > target.Y) curY--;

        return new List<(int X, int Y, int Z)> { (curX, curY, curZ) };
    }

    public (int X, int Y, int Z) GetRandomWalkableLocation()
    {
        // Mock returning a location within the central village chunk (0,0,0)
        return (Random.Shared.Next(0, 100), Random.Shared.Next(0, 100), 0);
    }

    public (int X, int Y, int Z)? GetCharacterLocation(Character character)
    {
        if (_characterLocations.TryGetValue(character, out var loc))
        {
            return (loc.X, loc.Y, loc.Z);
        }
        return null;
    }

    public void MoveCharacter(Character character, (int X, int Y, int Z) newLocation)
    {
        int z = newLocation.Z;

        _characterLocations[character] = (newLocation.X, newLocation.Y, z);

        int chunkX = (int)Math.Floor(newLocation.X / 100.0);
        int chunkY = (int)Math.Floor(newLocation.Y / 100.0);

        // Ensure chunk is loaded
        ChunkManager.LoadChunk(chunkX, chunkY, z, _simulationTime);
    }

    public IEnumerable<Character> GetCharacters()
    {
        return _characterLocations.Keys;
    }

    private (MapChunk chunk, int localX, int localY)? GetChunkAndLocal(int x, int y, int z = 0)
    {
        int chunkX = (int)Math.Floor(x / 100.0);
        int chunkY = (int)Math.Floor(y / 100.0);
        
        var chunk = ChunkManager.LoadChunk(chunkX, chunkY, z, _simulationTime);
        if (chunk == null) return null;

        int localX = x - (chunkX * 100);
        int localY = y - (chunkY * 100);
        
        return (chunk, localX, localY);
    }

    public int GetAppleCount((int X, int Y, int Z) location)
    {
        var result = GetChunkAndLocal(location.X, location.Y, location.Z);
        if (result == null) return 0;

        if (result.Value.chunk.TreeApples.TryGetValue((result.Value.localX, result.Value.localY), out int count))
        {
            return count;
        }
        return 0;
    }

    public bool TryGatherApple((int X, int Y, int Z) location)
    {
        var result = GetChunkAndLocal(location.X, location.Y, location.Z);
        if (result == null) return false;

        if (result.Value.chunk.TreeApples.TryGetValue((result.Value.localX, result.Value.localY), out int count) && count > 0)
        {
            result.Value.chunk.TreeApples[(result.Value.localX, result.Value.localY)] = count - 1;
            return true;
        }
        return false;
    }

    public NPC.Library.Spatial.Grid.TileType GetTile((int X, int Y, int Z) location)
    {
        var result = GetChunkAndLocal(location.X, location.Y, location.Z);
        if (result == null) return NPC.Library.Spatial.Grid.TileType.Grass;

        return result.Value.chunk.Tiles[result.Value.localX, result.Value.localY];
    }

    public NPC.Library.Inventory.IInventory? GetChest((int X, int Y, int Z) location)
    {
        var result = GetChunkAndLocal(location.X, location.Y, location.Z);
        if (result == null) return null;

        if (result.Value.chunk.Chests.TryGetValue((result.Value.localX, result.Value.localY), out var chest))
        {
            return chest;
        }
        return null;
    }

    public void TickEnvironment()
    {
        // Environment tick logic could check active chunks and update them
    }

    public void DropItem((int X, int Y, int Z) location, NPC.Library.Inventory.IItem item)
    {
        var result = GetChunkAndLocal(location.X, location.Y, location.Z);
        if (result == null) return;
        
        var pos = (result.Value.localX, result.Value.localY);
        if (!result.Value.chunk.GroundItems.ContainsKey(pos))
        {
            result.Value.chunk.GroundItems[pos] = new List<NPC.Library.Inventory.IItem>();
        }
        result.Value.chunk.GroundItems[pos].Add(item);
    }

    public IEnumerable<NPC.Library.Inventory.IItem> GetGroundItems((int X, int Y, int Z) location)
    {
        var result = GetChunkAndLocal(location.X, location.Y, location.Z);
        if (result != null && result.Value.chunk.GroundItems.TryGetValue((result.Value.localX, result.Value.localY), out var items))
        {
            return items;
        }
        return Array.Empty<NPC.Library.Inventory.IItem>();
    }

    public void RemoveGroundItem((int X, int Y, int Z) location, NPC.Library.Inventory.IItem item)
    {
        var result = GetChunkAndLocal(location.X, location.Y, location.Z);
        if (result != null && result.Value.chunk.GroundItems.TryGetValue((result.Value.localX, result.Value.localY), out var items))
        {
            items.Remove(item);
        }
    }
}
