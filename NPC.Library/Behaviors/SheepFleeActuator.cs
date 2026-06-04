namespace NPC.Library.Behaviors;

using System;
using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Spatial;
using NPC.Library.Spatial.Grid;
using NPC.Library.State;

public class SheepFleeActuator : IActuator
{
    private readonly ISpatialContext _spatialContext;
    public string Name => "Flee";
    public string Description => "Fleeing from a predator.";
    bool IActuator.IsPersistent => false;

    public SheepFleeActuator(ISpatialContext spatialContext)
    {
        _spatialContext = spatialContext;
    }

    public bool CanExecute(Character character)
    {
        if (character is not Animal a || a.AnimalType != AnimalType.Sheep) return false;

        var loc = _spatialContext.GetCharacterLocation(character);
        if (loc == null) return false;

        // Is there a fox nearby?
        var foxes = _spatialContext.GetCharacters().OfType<Animal>().Where(x => x.AnimalType == AnimalType.Fox && !x.IsDead);
        foreach (var fox in foxes)
        {
            var foxLoc = _spatialContext.GetCharacterLocation(fox);
            if (foxLoc != null)
            {
                int dist = Math.Abs(loc.Value.X - foxLoc.Value.X) + Math.Abs(loc.Value.Y - foxLoc.Value.Y);
                if (dist <= 6) // Flee radius
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

        character.LastAction = "Fleeing";

        // Find nearest fox
        var foxes = _spatialContext.GetCharacters().OfType<Animal>().Where(x => x.AnimalType == AnimalType.Fox && !x.IsDead);
        (int X, int Y, int Z)? nearestFox = null;
        int nearestDist = int.MaxValue;

        foreach (var fox in foxes)
        {
            var foxLoc = _spatialContext.GetCharacterLocation(fox);
            if (foxLoc != null)
            {
                int dist = Math.Abs(loc.Value.X - foxLoc.Value.X) + Math.Abs(loc.Value.Y - foxLoc.Value.Y);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestFox = foxLoc;
                }
            }
        }

        if (nearestFox != null)
        {
            // Move away from fox, avoiding water
            int bestX = loc.Value.X;
            int bestY = loc.Value.Y;
            int maxDist = nearestDist;

            for (int mx = -1; mx <= 1; mx++)
            {
                for (int my = -1; my <= 1; my++)
                {
                    if (mx == 0 && my == 0) continue;
                    var testLoc = (loc.Value.X + mx, loc.Value.Y + my, loc.Value.Z);
                    
                    // Don't enter water
                    if (_spatialContext.GetTile(testLoc) == TileType.Water) continue;

                    int testDist = Math.Abs(testLoc.Item1 - nearestFox.Value.X) + Math.Abs(testLoc.Item2 - nearestFox.Value.Y);
                    if (testDist > maxDist)
                    {
                        maxDist = testDist;
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

        return Task.CompletedTask;
    }
}

