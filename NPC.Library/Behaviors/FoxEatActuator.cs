namespace NPC.Library.Behaviors;

using System;
using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Spatial;
using NPC.Library.State;

public class FoxEatActuator : IActuator
{
    private readonly ISpatialContext _spatialContext;
    public string Name => "Eat";
    public string Description => "Eating a dead sheep.";
    bool IActuator.IsPersistent => false;

    public FoxEatActuator(ISpatialContext spatialContext)
    {
        _spatialContext = spatialContext;
    }

    public bool CanExecute(Character character)
    {
        if (character is not Animal a || a.AnimalType != AnimalType.Fox) return false;

        var loc = _spatialContext.GetCharacterLocation(character);
        if (loc == null) return false;

        // Is there a dead sheep nearby with meat?
        var deadSheep = _spatialContext.GetCharacters().OfType<Animal>().Where(x => x.AnimalType == AnimalType.Sheep && x.IsDead && x.MeatAmount > 0);
        foreach (var sheep in deadSheep)
        {
            var sheepLoc = _spatialContext.GetCharacterLocation(sheep);
            if (sheepLoc != null)
            {
                int dist = Math.Abs(loc.Value.X - sheepLoc.Value.X) + Math.Abs(loc.Value.Y - sheepLoc.Value.Y);
                if (dist <= 1) // Must be adjacent to eat
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

        character.LastAction = "Eating Sheep";

        var deadSheep = _spatialContext.GetCharacters().OfType<Animal>().Where(x => x.AnimalType == AnimalType.Sheep && x.IsDead && x.MeatAmount > 0);
        Animal? targetSheep = null;

        foreach (var sheep in deadSheep)
        {
            var sheepLoc = _spatialContext.GetCharacterLocation(sheep);
            if (sheepLoc != null)
            {
                int dist = Math.Abs(loc.Value.X - sheepLoc.Value.X) + Math.Abs(loc.Value.Y - sheepLoc.Value.Y);
                if (dist <= 1)
                {
                    targetSheep = sheep;
                    break;
                }
            }
        }

        if (targetSheep != null)
        {
            targetSheep.MeatAmount -= 1.0m;
            if (character.Drives.TryGetLevel(DriveType.Satiety, out var satiety))
            {
                character.Drives[DriveType.Satiety] = Math.Min(1.0m, satiety + 0.33m);
            }
            Console.WriteLine($"[WILDLIFE] {character.Name} ate meat from {targetSheep.Name}. Remaining meat: {targetSheep.MeatAmount:F1}");
        }

        return Task.CompletedTask;
    }
}
