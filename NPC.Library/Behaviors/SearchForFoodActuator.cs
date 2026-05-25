namespace NPC.Library.Behaviors;

using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Spatial;
using NPC.Library.State;

public class SearchForFoodActuator : IActuator
{
    private readonly ISpatialContext _spatialContext;

    public SearchForFoodActuator(ISpatialContext spatialContext)
    {
        _spatialContext = spatialContext;
    }

    public int GetPriority(NPC.Library.Character.Character character, DriveType currentDrive) => 10;

    public bool CanExecute(NPC.Library.Character.Character character)
    {
        // Can always blindly search if hungry or thirsty.
        return _spatialContext.GetCharacterLocation(character) != null; 
    }

    public Task ExecuteAsync(NPC.Library.Character.Character character)
    {
        var currentLocation = _spatialContext.GetCharacterLocation(character);
        if (currentLocation == null) return Task.CompletedTask;

        // If no destination or we reached it, pick a new one
        if (!character.CurrentDestination.HasValue || character.CurrentDestination.Value == currentLocation.Value)
        {
            character.CurrentDestination = _spatialContext.GetRandomWalkableLocation();
            character.LastAction = "Finished Searching";
        }

        var path = System.Linq.Enumerable.ToList(_spatialContext.GetPath(currentLocation.Value, character.CurrentDestination.Value));
        
        if (path.Count > 0)
        {
            _spatialContext.MoveCharacter(character, path[0]);
            character.LastAction = "Searching (Wandering)";
        }
        else
        {
            // Stuck! Pick a new destination next tick
            character.CurrentDestination = null;
            character.LastAction = "Stuck (Recalculating)";
        }

        return Task.CompletedTask;
    }
}
