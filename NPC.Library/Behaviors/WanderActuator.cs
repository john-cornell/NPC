namespace NPC.Library.Behaviors;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Spatial;
using NPC.Library.State;

public class WanderActuator : IActuator
{
    private readonly ISpatialContext _spatialContext;

    public string Name => "Wander";
    public string Description => "Wandering aimlessly around the world.";
    public bool IsPersistent => false;

    public WanderActuator(ISpatialContext spatialContext)
    {
        _spatialContext = spatialContext ?? throw new ArgumentNullException(nameof(spatialContext));
    }

    public bool CanExecute(Character character)
    {
        var loc = _spatialContext.GetCharacterLocation(character);
        if (loc == null) return false;
        
        // If we have a destination and we've reached it, the goal is "complete" for this cycle.
        // Return false so the StateMachine drops this as the ActiveActuator and re-evaluates priorities.
        if (character.CurrentDestination.HasValue && character.CurrentDestination.Value == loc.Value)
        {
            // We clear it here so that NEXT time we evaluate available actuators, CanExecute is true again!
            character.CurrentDestination = null;
            return false;
        }

        return true;
    }

    public Task ExecuteAsync(Character character)
    {
        var currentLocation = _spatialContext.GetCharacterLocation(character);
        if (currentLocation == null) return Task.CompletedTask;

        // If no destination or we reached it, pick a new one
        if (!character.CurrentDestination.HasValue || character.CurrentDestination.Value == currentLocation.Value)
        {
            character.CurrentDestination = _spatialContext.GetRandomWalkableLocation();
            character.LastAction = "Decided to Wander";
        }

        var path = _spatialContext.GetPath(currentLocation.Value, character.CurrentDestination.Value).ToList();
        
        if (path.Count > 0)
        {
            _spatialContext.MoveCharacter(character, path[0]);
            character.LastAction = "Wandering Aimlessly";
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
