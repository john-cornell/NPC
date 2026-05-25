namespace NPC.Library.Memory;

using System.Collections.Generic;
using System.Linq;
using NPC.Library.Spatial.Grid;

/// <summary>
/// A memory component that never forgets. It remembers all tiles it has ever seen.
/// </summary>
public class SpatialMemory : IMemory
{
    private readonly Dictionary<TileType, HashSet<(int X, int Y)>> _memory = new();

    public void Remember(TileType type, (int X, int Y) location)
    {
        if (!_memory.TryGetValue(type, out var locations))
        {
            locations = new HashSet<(int X, int Y)>();
            _memory[type] = locations;
        }

        locations.Add(location);
    }

    public void Forget(TileType type, int x, int y)
    {
        if (_memory.TryGetValue(type, out var locations))
        {
            locations.Remove((x, y));
        }
    }

    public IEnumerable<(int X, int Y)> Recall(TileType type)
    {
        if (_memory.TryGetValue(type, out var locations))
        {
            return locations;
        }
        return Enumerable.Empty<(int X, int Y)>();
    }

    public IDictionary<string, double> GetTunableParameters()
    {
        // This implementation has no tunable limits (it's infinite).
        return new Dictionary<string, double>();
    }

    public void SetTunableParameters(IDictionary<string, double> parameters)
    {
        // Ignored
    }
}
