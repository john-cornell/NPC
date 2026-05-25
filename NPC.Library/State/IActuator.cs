namespace NPC.Library.State;

using System;
using System.Threading.Tasks;
using NPC.Library.Character;

/// <summary>
/// Represents a specific action that fulfills a particular drive/need.
/// </summary>
public interface IActuator
{
    /// <summary>
    /// Check if the actuator can currently be performed by the character.
    /// </summary>
    bool CanExecute(Character character);

    /// <summary>
    /// Perform the action asynchronously.
    /// </summary>
    Task ExecuteAsync(Character character);

    /// <summary>
    /// If true, the StateMachine will commit to this actuator until it completes 
    /// or the character's primary drive changes. If false, the StateMachine will 
    /// re-evaluate available actuators every tick.
    /// </summary>
    bool IsPersistent => true;

    /// <summary>
    /// Higher values denote higher priority when selecting from available actuators.
    /// By providing the targetDrive, actuators can dynamically prioritize themselves (e.g. GatherFood is high priority when hungry, but low priority when idle).
    /// </summary>
    int GetPriority(Character character, DriveType currentDrive) => 0;
}
