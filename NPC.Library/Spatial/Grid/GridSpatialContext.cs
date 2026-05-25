namespace NPC.Library.Spatial.Grid;

using System;
using System.Collections.Generic;
using NPC.Library.Character;

public class GridSpatialContext : ISpatialContext
{
    private readonly MapGrid _grid;
    private readonly IPathfinder _pathfinder;
    private readonly Random _random = new();

    public Dictionary<Character, (int X, int Y)> CharacterPositions { get; } = new();
    
    public MapGrid Map => _grid;

    public GridSpatialContext(MapGrid grid)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _pathfinder = new SimplePathfinder(_grid);
    }

    public IEnumerable<(int X, int Y)> GetPath((int X, int Y) start, (int X, int Y) target)
    {
        return _pathfinder.FindPath(start, target);
    }

    public (int X, int Y) GetRandomWalkableLocation()
    {
        // Simple random sampling until we hit a walkable tile
        while (true)
        {
            int x = _random.Next(0, _grid.Width);
            int y = _random.Next(0, _grid.Height);

            if (_grid.Tiles[x, y] != TileType.Water && _grid.Tiles[x, y] != TileType.Wall)
            {
                return (x, y);
            }
        }
    }

    public (int X, int Y)? GetCharacterLocation(Character character)
    {
        if (CharacterPositions.TryGetValue(character, out var pos))
        {
            return pos;
        }
        return null;
    }

    public void MoveCharacter(Character character, (int X, int Y) newLocation)
    {
        CharacterPositions[character] = newLocation;
    }

    public IEnumerable<Character> GetCharacters()
    {
        return CharacterPositions.Keys;
    }

    public int GetAppleCount((int X, int Y) location)
    {
        if (_grid.TreeApples.TryGetValue(location, out int count))
        {
            return count;
        }
        return 0;
    }

    public bool TryGatherApple((int X, int Y) location)
    {
        if (_grid.TreeApples.TryGetValue(location, out int count) && count > 0)
        {
            _grid.TreeApples[location] = count - 1;
            return true;
        }
        return false;
    }

    public void TickEnvironment()
    {
        // 2% chance per tick to regrow 1 apple up to 10 max
        var keys = new List<(int X, int Y)>(_grid.TreeApples.Keys);
        foreach (var key in keys)
        {
            if (_random.NextDouble() < 0.02)
            {
                if (_grid.TreeApples[key] < 10)
                {
                    _grid.TreeApples[key]++;
                }
            }
        }
    }
}
