namespace NPC.Library.Spatial.Grid;

using System;
using System.Collections.Generic;
using System.Linq;

public class SimplePathfinder : IPathfinder
{
    private readonly MapGrid _grid;

    public SimplePathfinder(MapGrid grid)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
    }

    public IEnumerable<(int X, int Y)> FindPath((int X, int Y) start, (int X, int Y) end)
    {
        if (start == end) return Array.Empty<(int X, int Y)>();

        var openSet = new PriorityQueue<(int X, int Y), int>();
        var cameFrom = new Dictionary<(int X, int Y), (int X, int Y)>();
        var gScore = new Dictionary<(int X, int Y), int>();
        
        openSet.Enqueue(start, 0);
        gScore[start] = 0;

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current == end)
            {
                return ReconstructPath(cameFrom, current);
            }

            foreach (var neighbor in GetWalkableNeighbors(current))
            {
                int tentativeGScore = gScore[current] + 1;
                
                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    int fScore = tentativeGScore + ManhattanDistance(neighbor, end);
                    openSet.Enqueue(neighbor, fScore);
                }
            }
        }
        
        return Array.Empty<(int X, int Y)>();
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

    private int ManhattanDistance((int X, int Y) a, (int X, int Y) b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    private IEnumerable<(int X, int Y)> GetWalkableNeighbors((int X, int Y) point)
    {
        var (x, y) = point;
        var candidates = new[] { (x, y - 1), (x, y + 1), (x - 1, y), (x + 1, y) };

        foreach (var c in candidates)
        {
            if (c.Item1 >= 0 && c.Item1 < _grid.Width && c.Item2 >= 0 && c.Item2 < _grid.Height)
            {
                if (_grid.Tiles[c.Item1, c.Item2] != TileType.Water && _grid.Tiles[c.Item1, c.Item2] != TileType.Wall)
                {
                    yield return c;
                }
            }
        }
    }
}
