namespace NPC.Library.Behaviors;

using System;
using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Spatial;
using NPC.Library.Spatial.Grid;
using NPC.Library.State;

public class FoxHuntActuator : IActuator
{
    private readonly ISpatialContext _spatialContext;
    public string Name => "Hunt";
    public string Description => "Hunting a sheep.";
    bool IActuator.IsPersistent => false;

    public FoxHuntActuator(ISpatialContext spatialContext)
    {
        _spatialContext = spatialContext;
    }

    public bool CanExecute(Character character)
    {
        if (character is not Animal a || a.AnimalType != AnimalType.Fox) return false;

        var loc = _spatialContext.GetCharacterLocation(character);
        if (loc == null) return false;

        // Is there a live sheep?
        var sheep = _spatialContext.GetCharacters().OfType<Animal>().Where(x => x.AnimalType == AnimalType.Sheep && !x.IsDead);
        return sheep.Any();
    }

    public Task ExecuteAsync(Character character)
    {
        var loc = _spatialContext.GetCharacterLocation(character);
        if (loc == null) return Task.CompletedTask;

        character.LastAction = "Hunting";

        // Find nearest sheep
        var sheepList = _spatialContext.GetCharacters().OfType<Animal>().Where(x => x.AnimalType == AnimalType.Sheep && !x.IsDead);
        Animal? nearestSheep = null;
        (int X, int Y, int Z)? nearestSheepLoc = null;
        int nearestDist = int.MaxValue;

        foreach (var sheep in sheepList)
        {
            var sheepLoc = _spatialContext.GetCharacterLocation(sheep);
            if (sheepLoc != null)
            {
                int dist = Math.Abs(loc.Value.X - sheepLoc.Value.X) + Math.Abs(loc.Value.Y - sheepLoc.Value.Y);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestSheep = sheep;
                    nearestSheepLoc = sheepLoc;
                }
            }
        }

        if (nearestSheep != null && nearestSheepLoc != null)
        {
            if (nearestDist <= 1) // Adjacent
            {
                // Kill it
                character.LastAction = "Attacking Sheep";
                nearestSheep.IsDead = true;
                nearestSheep.DeathReason = "Eaten";
                nearestSheep.LastAction = "Dead";
                Console.WriteLine($"[WILDLIFE] {character.Name} hunted and killed {nearestSheep.Name}!");
            }
            else
            {
                // Path towards it
                var path = _spatialContext.GetPath(loc.Value, nearestSheepLoc.Value).ToList();
                if (path.Count > 0)
                {
                    _spatialContext.MoveCharacter(character, path[0]);
                }
            }
        }

        return Task.CompletedTask;
    }
}

