namespace NPC.Library.Memory;

using System.Collections.Generic;
using System.Linq;
using NPC.Library.Spatial.Grid;

/// <summary>
/// A bounded memory component that forgets the oldest entries when capacity is reached.
/// </summary>
public class GoldfishMemory : IMemory
{
    // The tunable parameter for our GA
    private int _capacity = 3; 

    // Queue to keep track of chronological order of all memories
    private readonly Queue<(TileType Type, int X, int Y)> _timeline = new();
    
    // Fast lookup
    private readonly Dictionary<TileType, HashSet<(int X, int Y)>> _memory = new();

    public void Remember(TileType type, (int X, int Y) location)
    {
        if (!_memory.TryGetValue(type, out var locations))
        {
            locations = new HashSet<(int X, int Y)>();
            _memory[type] = locations;
        }

        // Avoid adding duplicates to timeline
        if (locations.Contains(location)) return;

        locations.Add(location);
        _timeline.Enqueue((type, location.X, location.Y));

        // Enforce capacity constraint
        while (_timeline.Count > _capacity)
        {
            var oldest = _timeline.Dequeue();
            _memory[oldest.Type].Remove((oldest.X, oldest.Y));
        }
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
        return new Dictionary<string, double>
        {
            { "Capacity", _capacity }
        };
    }

    public void SetTunableParameters(IDictionary<string, double> parameters)
    {
        if (parameters.TryGetValue("Capacity", out var cap))
        {
            _capacity = (int)cap;
            
            // Trim if capacity was lowered
            while (_timeline.Count > _capacity)
            {
                var oldest = _timeline.Dequeue();
                _memory[oldest.Type].Remove((oldest.X, oldest.Y));
            }
        }
    }
}
