namespace NPC.Village.Behaviors;

using NPC.Library.Behaviors;
using NPC.Library.State;
using NPC.Village.Memory;
using NPC.Library.Character.Components;


using System;
using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Character.Components;
using NPC.Library.Spatial;
using NPC.Library.State;

public class VillageSleepInBedActuator : IActuator
{
    private readonly ISpatialContext _spatialContext;

    public VillageSleepInBedActuator(ISpatialContext spatialContext)
    {
        _spatialContext = spatialContext;
    }

    public int GetPriority(Character character, DriveType currentDrive) => 100;

    public bool CanExecute(NPC.Library.Character.Character character)
    {
        if (character.TryGetComponent<BedComponent>(out var home))
        {
            // If another drive takes priority, wake up.
            if (character.TargetDrive != DriveType.Idle)
                return false;

            if (character.Drives.TryGetLevel(DriveType.Fatigue, out var fatigue))
            {
                // Start sleeping at 0.7, OR continue sleeping until nearly 0 (to account for passive tick decay pushing it slightly above 0)
                if (fatigue >= 0.7m || (fatigue > 0.1m && character.LastAction == "Sleeping in Bed"))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public Task ExecuteAsync(NPC.Library.Character.Character character)
    {
        if (!character.TryGetComponent<BedComponent>(out var home)) return Task.CompletedTask;
        var loc = _spatialContext.GetCharacterLocation(character);
        if (loc == null) return Task.CompletedTask;

        if (loc.Value == home.Location)
        {
            // At home, sleep
            character.LastAction = "Sleeping in Bed";
            if (character.Drives.TryGetLevel(DriveType.Fatigue, out var currentFatigue))
            {
                character.Drives.SetLevel(DriveType.Fatigue, Math.Max(0m, currentFatigue - 0.15m));
            }
        }
        else
        {
            // Pathfind home
            character.LastAction = "Pathfinding Home to Sleep";
            var path = _spatialContext.GetPath(loc.Value, home.Location).ToList();
            if (path.Count > 0)
            {
                _spatialContext.MoveCharacter(character, path[0]);
            }
        }

        return Task.CompletedTask;
    }
}
