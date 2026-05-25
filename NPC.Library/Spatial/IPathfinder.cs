namespace NPC.Library.Spatial;

using System.Collections.Generic;

/// <summary>
/// A generic interface for pathfinding algorithms.
/// </summary>
public interface IPathfinder
{
    /// <summary>
    /// Computes a path from the start point to the end point.
    /// Returns an empty enumerable if no path is found.
    /// </summary>
    IEnumerable<(int X, int Y)> FindPath((int X, int Y) start, (int X, int Y) end);
}
