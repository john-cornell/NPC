namespace NPC.Village.Memory;

using System;
using System.Linq;
using System.Collections.Generic;
using NPC.Library.Memory;
using NPC.Library.Spatial.Grid;

/// <summary>
/// A specialized memory component for the village scenario.
/// It permanently remembers the location of the village Well.
/// It does not remember anything else (capacity 0 for other things).
/// </summary>
public class VillageMemory : IMemory
{
    private readonly HashSet<(int X, int Y, int Z)> _wells = new();
    private readonly HashSet<(int X, int Y, int Z)> _doors = new();
    private readonly HashSet<(int X, int Y, int Z)> _chests = new();
    private readonly HashSet<(int X, int Y, int Z)> _beds = new();
    
    private readonly Dictionary<TileType, Queue<(int X, int Y, int Z)>> _dynamicMemory = new();
    private int _capacity = 10;

    public VillageMemory((int X, int Y, int Z) wellLocation, (int X, int Y, int Z) doorLocation, (int X, int Y, int Z) chestLocation, (int X, int Y, int Z) bedLocation)
    {
        _wells.Add(wellLocation);
        _doors.Add(doorLocation);
        _chests.Add(chestLocation);
        _beds.Add(bedLocation);
    }

    public void Remember(TileType type, (int X, int Y, int Z) location)
    {
        if (type == TileType.Well || type == TileType.Door || type == TileType.Chest || type == TileType.Bed) return;

        if (!_dynamicMemory.TryGetValue(type, out var queue))
        {
            queue = new Queue<(int X, int Y, int Z)>();
            _dynamicMemory[type] = queue;
        }

        if (!queue.Contains(location))
        {
            if (queue.Count >= _capacity)
            {
                queue.Dequeue();
            }
            queue.Enqueue(location);
        }
    }

    public void Forget(TileType type, int x, int y, int z)
    {
        if (type == TileType.Well || type == TileType.Door || type == TileType.Chest || type == TileType.Bed) return;
        
        if (_dynamicMemory.TryGetValue(type, out var queue))
        {
            var items = queue.ToList();
            if (items.Remove((x, y, z)))
            {
                _dynamicMemory[type] = new Queue<(int X, int Y, int Z)>(items);
            }
        }
    }

    public IEnumerable<(int X, int Y, int Z)> Recall(TileType type)
    {
        if (type == TileType.Well) return _wells;
        if (type == TileType.Door) return _doors;
        if (type == TileType.Chest) return _chests;
        if (type == TileType.Bed) return _beds;

        if (_dynamicMemory.TryGetValue(type, out var queue))
        {
            return queue;
        }
        return Array.Empty<(int X, int Y, int Z)>();
    }

    public IDictionary<string, double> GetTunableParameters()
    {
        return new Dictionary<string, double> { { "Capacity", _capacity } };
    }

    public void SetTunableParameters(IDictionary<string, double> parameters)
    {
        if (parameters.TryGetValue("Capacity", out var val))
        {
            _capacity = (int)val;
        }
    }
}
