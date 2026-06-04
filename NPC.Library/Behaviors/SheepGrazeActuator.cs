namespace NPC.Library.Behaviors;

using System;
using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Spatial;
using NPC.Library.Spatial.Grid;
using NPC.Library.State;

public class SheepGrazeActuator : IActuator
{
    private readonly ISpatialContext _spatialContext;
    public string Name => "Graze";
    public string Description => "Grazing on grass.";
    bool IActuator.IsPersistent => false;

    public SheepGrazeActuator(ISpatialContext spatialContext)
    {
        _spatialContext = spatialContext;
    }

    public bool CanExecute(Character character)
    {
        // Must be a sheep
        if (character is not Animal a || a.AnimalType != AnimalType.Sheep) return false;
        
        var loc = _spatialContext.GetCharacterLocation(character);
        if (loc == null) return false;

        // Is there grass nearby?
        var radius = 2; // Can search within 2 tiles
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                var target = (loc.Value.X + dx, loc.Value.Y + dy, loc.Value.Z);
                if (_spatialContext.GetTile(target) == TileType.Grass)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public Task ExecuteAsync(Character character)
    {
        var loc = _spatialContext.GetCharacterLocation(character);
        if (loc == null) return Task.CompletedTask;

        character.LastAction = "Grazing";

        // Find nearest grass
        (int X, int Y, int Z)? nearestGrass = null;
        int nearestDist = int.MaxValue;
        var radius = 5;
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                var target = (loc.Value.X + dx, loc.Value.Y + dy, loc.Value.Z);
                if (_spatialContext.GetTile(target) == TileType.Grass)
                {
                    int dist = Math.Abs(dx) + Math.Abs(dy);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestGrass = target;
                    }
                }
            }
        }

        if (nearestGrass != null)
        {
            if (nearestDist == 0)
            {
                // Already on grass, eat!
                if (character.Drives.TryGetLevel(DriveType.Satiety, out var satiety))
                {
                    character.Drives[DriveType.Satiety] = Math.Min(1.0m, satiety + 0.1m);
                }
            }
            else
            {
                // Path towards it avoiding water
                int bestX = loc.Value.X;
                int bestY = loc.Value.Y;
                int minDist = nearestDist; // which is the distance to nearest grass from current loc

                for (int mx = -1; mx <= 1; mx++)
                {
                    for (int my = -1; my <= 1; my++)
                    {
                        if (mx == 0 && my == 0) continue;
                        var testLoc = (loc.Value.X + mx, loc.Value.Y + my, loc.Value.Z);
                        
                        // Don't enter water
                        if (_spatialContext.GetTile(testLoc) == TileType.Water) continue;

                        int testDist = Math.Abs(testLoc.Item1 - nearestGrass.Value.X) + Math.Abs(testLoc.Item2 - nearestGrass.Value.Y);
                        // If it brings us closer, it's good
                        if (testDist < minDist)
                        {
                            minDist = testDist;
                            bestX = testLoc.Item1;
                            bestY = testLoc.Item2;
                        }
                    }
                }

                if (bestX != loc.Value.X || bestY != loc.Value.Y)
                {
                    _spatialContext.MoveCharacter(character, (bestX, bestY, loc.Value.Z));
                }
            }
        }

        return Task.CompletedTask;
    }
}

