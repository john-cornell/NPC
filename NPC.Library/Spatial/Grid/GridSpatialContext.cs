namespace NPC.Library.Spatial.Grid;

using System;
using System.Collections.Generic;
using NPC.Library.Character;

public class GridSpatialContext : ISpatialContext
{
    private readonly MapGrid _grid;
    private readonly IPathfinder _pathfinder;
    private readonly Random _random = new();

    public Dictionary<Character, (int X, int Y, int Z)> CharacterPositions { get; } = new();
    
    public DateTime CurrentTime { get; set; } = DateTime.MinValue;

    public MapGrid Map => _grid;

    public GridSpatialContext(MapGrid grid)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _pathfinder = new SimplePathfinder(_grid);
    }

    public IEnumerable<(int X, int Y, int Z)> GetPath((int X, int Y, int Z) start, (int X, int Y, int Z) target)
    {
        var path2d = _pathfinder.FindPath((start.X, start.Y), (target.X, target.Y));
        var path3d = new List<(int X, int Y, int Z)>();
        foreach (var p in path2d) path3d.Add((p.X, p.Y, target.Z));
        return path3d;
    }

    public (int X, int Y, int Z) GetRandomWalkableLocation()
    {
        // Simple random sampling until we hit a walkable tile
        while (true)
        {
            int x = _random.Next(0, _grid.Width);
            int y = _random.Next(0, _grid.Height);

            if (_grid.Tiles[x, y] != TileType.Water && _grid.Tiles[x, y] != TileType.Wall)
            {
                return (x, y, 0);
            }
        }
    }

    public (int X, int Y, int Z)? GetCharacterLocation(Character character)
    {
        if (CharacterPositions.TryGetValue(character, out var pos))
        {
            return pos;
        }
        return null;
    }

    public void MoveCharacter(Character character, (int X, int Y, int Z) newLocation)
    {
        CharacterPositions[character] = newLocation;
    }

    public IEnumerable<Character> GetCharacters()
    {
        return CharacterPositions.Keys;
    }

    public int GetAppleCount((int X, int Y, int Z) location)
    {
        if (_grid.TreeApples.TryGetValue((location.X, location.Y), out int count))
        {
            return count;
        }
        return 0;
    }

    public bool TryGatherApple((int X, int Y, int Z) location)
    {
        if (_grid.TreeApples.TryGetValue((location.X, location.Y), out int count) && count > 0)
        {
            _grid.TreeApples[(location.X, location.Y)] = count - 1;
            return true;
        }
        return false;
    }

    public TileType GetTile((int X, int Y, int Z) location)
    {
        if (location.X >= 0 && location.X < _grid.Width && location.Y >= 0 && location.Y < _grid.Height)
        {
            return _grid.Tiles[location.X, location.Y];
        }
        return TileType.Grass;
    }

    public NPC.Library.Inventory.IInventory? GetChest((int X, int Y, int Z) location)
    {
        if (_grid.Chests.TryGetValue((location.X, location.Y), out var chest))
        {
            return chest;
        }
        return null;
    }

    public void DropItem((int X, int Y, int Z) location, NPC.Library.Inventory.IItem item)
    {
        // Simple mapgrid doesn't support drops currently, stub it or implement a dictionary
    }

    public IEnumerable<NPC.Library.Inventory.IItem> GetGroundItems((int X, int Y, int Z) location)
    {
        return Array.Empty<NPC.Library.Inventory.IItem>();
    }

    public void RemoveGroundItem((int X, int Y, int Z) location, NPC.Library.Inventory.IItem item)
    {
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
