namespace NPC.Library.Memory;

using System.Collections.Generic;
using NPC.Library.Spatial.Grid;

/// <summary>
/// A tunable memory component that can be queried and modified by Machine Learning algorithms.
/// </summary>
public interface IMemory
{
    /// <summary>
    /// Records an observation in the world.
    /// </summary>
    void Remember(TileType type, (int X, int Y) location);

    /// <summary>
    /// Removes a specific location from memory.
    /// </summary>
    void Forget(TileType type, int x, int y);

    /// <summary>
    /// Retrieves all remembered locations of a specific type.
    /// </summary>
    IEnumerable<(int X, int Y)> Recall(TileType type);

    /// <summary>
    /// Retrieves tunable parameters exposed to Genetic Algorithms (e.g., Capacity, DecayRate).
    /// </summary>
    IDictionary<string, double> GetTunableParameters();

    /// <summary>
    /// Sets the tunable parameters mutated by Genetic Algorithms.
    /// </summary>
    void SetTunableParameters(IDictionary<string, double> parameters);
}
