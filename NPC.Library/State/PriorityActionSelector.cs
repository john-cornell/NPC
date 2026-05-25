namespace NPC.Library.State;

using System;
using System.Collections.Generic;
using System.Linq;
using NPC.Library.Character;

/// <summary>
/// Selects an actuator based on priority. If multiple actuators share the same highest priority, it picks randomly among them.
/// </summary>
public class PriorityActionSelector : IActionSelector
{
    private readonly Random _random = new();

    public IActuator? Select(IEnumerable<IActuator> actuators, Character character, DriveType currentDrive)
    {
        var list = actuators.ToList();
        if (list.Count == 0) return null;

        var highestPriority = list.Max(a => a.GetPriority(character, currentDrive));
        var bestActuators = list.Where(a => a.GetPriority(character, currentDrive) == highestPriority).ToList();
        
        var chosen = bestActuators[_random.Next(bestActuators.Count)];

        // We no longer log directly here; we could dispatch a Message if needed.

        return chosen;
    }
}
