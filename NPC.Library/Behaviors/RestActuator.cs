namespace NPC.Library.Behaviors;

using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Character.Components;
using NPC.Library.State;

public class RestActuator : IActuator
{
    public int GetPriority(NPC.Library.Character.Character character, DriveType currentDrive) => 50;

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
        if (character.Drives.TryGetLevel(DriveType.Fatigue, out var currentFatigue))
        {
            character.Drives.SetLevel(DriveType.Fatigue, System.Math.Max(0m, currentFatigue - 0.05m));
        }
        return Task.CompletedTask;
    }
}
