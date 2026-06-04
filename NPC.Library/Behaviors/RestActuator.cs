namespace NPC.Library.Behaviors;

using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Character.Components;
using NPC.Library.State;

public class RestActuator : IActuator
{

    public bool CanExecute(NPC.Library.Character.Character character)
    {
        if (character.TryGetComponent<BedComponent>(out _))
        {
            // If they have a bed, they should use SleepInBedActuator instead of resting on the ground
            return false;
        }
        return true; 
    }

    public Task ExecuteAsync(NPC.Library.Character.Character character)
    {
        character.LastAction = "Resting";
        return Task.CompletedTask;
    }
}
