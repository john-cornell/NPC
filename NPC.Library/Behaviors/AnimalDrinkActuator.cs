namespace NPC.Library.Behaviors;

using System;
using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Spatial;
using NPC.Library.Spatial.Grid;
using NPC.Library.State;

public class AnimalDrinkActuator : IActuator
{
    private readonly ISpatialContext _spatialContext;
    public string Name => "Drink";
    public string Description => "Drinking from a water source.";
    bool IActuator.IsPersistent => false;

    public AnimalDrinkActuator(ISpatialContext spatialContext)
    {
        _spatialContext = spatialContext;
    }

    public bool CanExecute(Character character)
    {
        if (character is not Animal) return false;
        
        var loc = _spatialContext.GetCharacterLocation(character);
        if (loc == null) return false;

        // Is there water nearby? (Search up to 10 tiles away to find water)
        var radius = 10; 
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                var target = (loc.Value.X + dx, loc.Value.Y + dy, loc.Value.Z);
                if (_spatialContext.GetTile(target) == TileType.Water)
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

        character.LastAction = "Drinking";

        // Find nearest water
        (int X, int Y, int Z)? nearestWater = null;
        int nearestDist = int.MaxValue;
        var radius = 15;
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                var target = (loc.Value.X + dx, loc.Value.Y + dy, loc.Value.Z);
                if (_spatialContext.GetTile(target) == TileType.Water)
                {
                    int dist = Math.Abs(dx) + Math.Abs(dy);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestWater = target;
                    }
                }
            }
        }

        if (nearestWater != null)
        {
            // If we are adjacent to water, drink!
            if (nearestDist == 1)
            {
                if (character.Drives.TryGetLevel(DriveType.Thirst, out var thirst))
                {
                    character.Drives[DriveType.Thirst] = Math.Min(1.0m, thirst + 0.33m);
                }
            }
            else
            {
                // Path towards it avoiding entering water itself
                int bestX = loc.Value.X;
                int bestY = loc.Value.Y;
                int minDist = nearestDist; 

                for (int mx = -1; mx <= 1; mx++)
                {
                    for (int my = -1; my <= 1; my++)
                    {
                        if (mx == 0 && my == 0) continue;
                        var testLoc = (loc.Value.X + mx, loc.Value.Y + my, loc.Value.Z);
                        
                        // Don't step INTO the water, just get adjacent
                        if (_spatialContext.GetTile(testLoc) == TileType.Water) continue;

                        int testDist = Math.Abs(testLoc.Item1 - nearestWater.Value.X) + Math.Abs(testLoc.Item2 - nearestWater.Value.Y);
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

