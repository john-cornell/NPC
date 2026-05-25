namespace NPC.Village.Behaviors;

using System;
using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Behaviors;
using NPC.Library.Character;
using NPC.Library.Spatial;
using NPC.Library.State;

public class VillageSocializeActuator : IActuator
{
    private readonly ISpatialContext _spatialContext;
    private readonly NPC.Library.Messaging.MessageDispatcher _dispatcher;

    public VillageSocializeActuator(ISpatialContext spatialContext, NPC.Library.Messaging.MessageDispatcher dispatcher)
    {
        _spatialContext = spatialContext;
        _dispatcher = dispatcher;
    }

    public int GetPriority(Character character, DriveType currentDrive)
    {
        // Only trigger if Social is our primary target need, or if Idle and slightly lonely
        if (currentDrive == DriveType.Social) return 60;
        
        if (currentDrive == DriveType.Idle && character.Drives.TryGetLevel(DriveType.Social, out var social) && social < 0.9m)
        {
            return 20;
        }

        return 0;
    }

    public bool CanExecute(Character character)
    {
        var otherLivingChars = _spatialContext.GetCharacters().Where(c => c != character && !c.IsDead).ToList();
        return otherLivingChars.Any();
    }

    public Task ExecuteAsync(Character character)
    {
        var myLoc = _spatialContext.GetCharacterLocation(character);
        if (myLoc == null) return Task.CompletedTask;

        // Find nearest living character
        var otherChars = _spatialContext.GetCharacters().Where(c => c != character && !c.IsDead).ToList();
        if (otherChars.Count == 0) return Task.CompletedTask;

        Character? targetCharacter = null;
        (int X, int Y)? targetLoc = null;
        int minDistance = int.MaxValue;

        foreach (var c in otherChars)
        {
            var loc = _spatialContext.GetCharacterLocation(c);
            if (loc != null)
            {
                int dist = Math.Abs(myLoc.Value.X - loc.Value.X) + Math.Abs(myLoc.Value.Y - loc.Value.Y);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    targetCharacter = c;
                    targetLoc = loc;
                }
            }
        }

        if (targetCharacter == null || !targetLoc.HasValue) return Task.CompletedTask;

        character.CurrentDestination = targetLoc.Value;

        if (minDistance <= 2) // Close enough to talk (Manhattan distance 1 or 2, e.g. adjacent or diagonal)
        {
            character.CurrentDestination = null;
            character.LastAction = $"Socializing with {targetCharacter.Name}";
            
            // Replenish Social Drive for BOTH characters
            if (character.Drives.TryGetLevel(DriveType.Social, out var mySocial))
            {
                character.Drives.SetLevel(DriveType.Social, Math.Min(1.0m, mySocial + 0.1m));
            }
            if (targetCharacter.Drives.TryGetLevel(DriveType.Social, out var theirSocial))
            {
                targetCharacter.Drives.SetLevel(DriveType.Social, Math.Min(1.0m, theirSocial + 0.1m));
            }

            // Emit socializing message occasionally so LLM doesn't spam every tick
            // Only emit if this is the first tick they started socializing with them, or randomly 10% chance
            bool justStarted = character.LastAction != $"Socializing with {targetCharacter.Name}";
            if (justStarted || new Random().NextDouble() < 0.05)
            {
                _dispatcher.DispatchImmediate(new NPC.Library.Messaging.CharacterSocializingMessage(character, targetCharacter));
            }
        }
        else
        {
            character.LastAction = $"Pathfinding to {targetCharacter.Name}";
            var path = _spatialContext.GetPath(myLoc.Value, targetLoc.Value).ToList();
            if (path.Count > 0)
            {
                _spatialContext.MoveCharacter(character, path[0]);
            }
        }

        return Task.CompletedTask;
    }
}
