namespace NPC.Library.State;

using System;
using System.Collections.Generic;
using System.Linq;
using NPC.Library.Character;

/// <summary>
/// A rule-based action selector specifically designed for Animals (Foxes and Sheep)
/// to prioritize survival actions (Fleeing, Hunting, Eating, Grazing) without a neural network.
/// </summary>
public sealed class AnimalActionSelector : IActionSelector
{
    private readonly Random _random = new();

    public IActuator? Select(IEnumerable<IActuator> actuators, NPC.Library.Character.Character character, DriveType currentDrive)
    {
        var list = actuators.ToList();
        if (list.Count == 0) return null;

        // Fleeing is highest priority for prey
        var flee = list.FirstOrDefault(a => a.GetType().Name == "SheepFleeActuator");
        if (flee != null && flee.CanExecute(character)) return flee;

        // Hunting is highest priority for predators (if hungry)
        var hunt = list.FirstOrDefault(a => a.GetType().Name == "FoxHuntActuator");
        if (hunt != null && hunt.CanExecute(character))
        {
            if (character.Drives.TryGetLevel(DriveType.Satiety, out var satiety) && satiety < 0.8m)
            {
                return hunt;
            }
        }
        
        // Drinking when thirsty
        var drink = list.FirstOrDefault(a => a.GetType().Name == "AnimalDrinkActuator");
        if (drink != null && drink.CanExecute(character))
        {
            if (character.Drives.TryGetLevel(DriveType.Thirst, out var thirst) && thirst < 0.8m)
            {
                return drink;
            }
        }
        
        // Eating dead prey is high priority
        var foxEat = list.FirstOrDefault(a => a.GetType().Name == "FoxEatActuator");
        if (foxEat != null && foxEat.CanExecute(character))
        {
            if (character.Drives.TryGetLevel(DriveType.Satiety, out var satiety) && satiety < 0.9m)
            {
                return foxEat;
            }
        }

        // Grazing is high priority for herbivores
        var graze = list.FirstOrDefault(a => a.GetType().Name == "SheepGrazeActuator");
        if (graze != null && graze.CanExecute(character))
        {
            if (character.Drives.TryGetLevel(DriveType.Satiety, out var satiety) && satiety < 0.7m)
            {
                return graze;
            }
        }

        // Sleeping when tired
        var sleep = list.FirstOrDefault(a => a.GetType().Name == "AnimalSleepActuator");
        if (sleep != null && sleep.CanExecute(character))
        {
            if (character.Drives.TryGetLevel(DriveType.Fatigue, out var fatigue) && fatigue > 0.6m)
            {
                return sleep;
            }
        }

        // Default: Random valid action (maybe just wandering or idle)
        var valid = list.Where(a => a.CanExecute(character)).ToList();
        if (valid.Count > 0)
        {
            return valid[_random.Next(valid.Count)];
        }

        return null;
    }
}
