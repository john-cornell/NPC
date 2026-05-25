namespace NPC.Library.State;

using System;
using System.Collections.Generic;
using System.Linq;
using NPC.Library.Character;

/// <summary>
/// Randomly selects an actuator from the available options.
/// </summary>
public sealed class RandomActionSelector : IActionSelector
{
    private readonly Random _random;

    public RandomActionSelector(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public IActuator? Select(IEnumerable<IActuator> actuators, NPC.Library.Character.Character character, DriveType currentDrive)
    {
        var list = actuators.ToList();
        if (list.Count == 0) return null;
        if (list.Count == 1) return list[0];

        // Simple heuristics
        var rest = list.FirstOrDefault(a => a is NPC.Library.Behaviors.RestActuator);
        
        if (character.Drives.Levels.TryGetValue(NPC.Library.Character.DriveType.Fatigue, out var fatigue))
        {
            if (fatigue >= 0.7m) 
            {
                if (rest != null) return rest;
            }
            if (fatigue >= 0.5m && _random.NextDouble() < (double)fatigue) 
            {
                if (rest != null) return rest;
            }
        }

        var eat = list.FirstOrDefault(a => a is NPC.Library.Behaviors.EatActuator);
        if (eat != null) return eat;

        var gather = list.FirstOrDefault(a => a is NPC.Library.Behaviors.GatherFoodActuator);
        if (gather != null && character.Drives.Levels.TryGetValue(NPC.Library.Character.DriveType.Satiety, out var satiety) && satiety < 0.5m)
        {
            // If we are very hungry and can gather, strongly prefer gathering over searching
            return gather;
        }

        var drink = list.FirstOrDefault(a => a is NPC.Library.Behaviors.DrinkActuator);
        if (drink != null) return drink;

        var gatherWater = list.FirstOrDefault(a => a is NPC.Library.Behaviors.GatherWaterActuator);
        if (gatherWater != null && character.Drives.Levels.TryGetValue(NPC.Library.Character.DriveType.Thirst, out var thirst) && thirst < 0.5m)
        {
            return gatherWater;
        }

        int index = _random.Next(list.Count);
        return list[index];
    }
}
