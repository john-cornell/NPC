namespace NPC.Library.Spatial.Grid;

using System;
using System.Collections.Generic;
using System.Linq;

public class AStarPathfinder : IPathfinder
{
    private readonly MapGrid _grid;

    public AStarPathfinder(MapGrid grid)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
    }

    public IEnumerable<(int X, int Y)> FindPath((int X, int Y) start, (int X, int Y) end)
    {
        if (start == end) return Enumerable.Empty<(int X, int Y)>();
        if (!IsWalkable(end.X, end.Y)) return Enumerable.Empty<(int X, int Y)>();

        var openSet = new HashSet<(int X, int Y)> { start };
        var cameFrom = new Dictionary<(int X, int Y), (int X, int Y)>();
        var gScore = new Dictionary<(int X, int Y), int> { [start] = 0 };
        var fScore = new Dictionary<(int X, int Y), int> { [start] = Heuristic(start, end) };

        while (openSet.Count > 0)
        {
            var current = openSet.OrderBy(n => fScore.TryGetValue(n, out var score) ? score : int.MaxValue).First();

            if (current == end)
                return ReconstructPath(cameFrom, current);

            openSet.Remove(current);

            foreach (var neighbor in GetNeighbors(current))
            {
                var tentativeGScore = gScore[current] + 1; // All steps cost 1

                if (!gScore.TryGetValue(neighbor, out var currentGScore) || tentativeGScore < currentGScore)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + Heuristic(neighbor, end);
                    openSet.Add(neighbor);
                }
            }
        }

        return Enumerable.Empty<(int X, int Y)>();
    }

    private int Heuristic((int X, int Y) a, (int X, int Y) b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y); // Manhattan distance
    }

    private IEnumerable<(int X, int Y)> GetNeighbors((int X, int Y) point)
    {
        var (x, y) = point;
        var candidates = new[] { (x, y - 1), (x, y + 1), (x - 1, y), (x + 1, y) };

        foreach (var c in candidates)
        {
            if (IsWalkable(c.Item1, c.Item2))
            {
                yield return c;
            }
        }
    }

    private bool IsWalkable(int x, int y)
    {
        if (x < 0 || x >= _grid.Width || y < 0 || y >= _grid.Height)
            return false;
            
        // For now, anything but water is walkable
        return _grid.Tiles[x, y] != TileType.Water;
    }

    private IEnumerable<(int X, int Y)> ReconstructPath(Dictionary<(int X, int Y), (int X, int Y)> cameFrom, (int X, int Y) current)
    {
        var path = new List<(int X, int Y)>();
        while (cameFrom.ContainsKey(current))
        {
            path.Add(current);
            current = cameFrom[current];
        }
        path.Reverse();
        return path;
    }
}
