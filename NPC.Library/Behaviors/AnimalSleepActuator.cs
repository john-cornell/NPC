namespace NPC.Library.Behaviors;

using System;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.State;

public class AnimalSleepActuator : IActuator
{
    public string Name => "Sleep";
    public string Description => "Sleeping on the ground.";
    bool IActuator.IsPersistent => false;

    public bool CanExecute(Character character)
    {
        if (character is not Animal) return false;
        return true;
    }

    public Task ExecuteAsync(Character character)
    {
        character.LastAction = "Sleeping";

        return Task.CompletedTask;
    }
}
