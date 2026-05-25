namespace NPC.Library.State;

using System;
using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Character;

/// <summary>
/// Event arguments fired when the state machine executes an actuator.
/// </summary>
public class ActuatorExecutedEventArgs : EventArgs
{
    public Character Character { get; }
    public DriveType Drive { get; }
    public IActuator Actuator { get; }

    public ActuatorExecutedEventArgs(Character character, DriveType drive, IActuator actuator)
    {
        Character = character;
        Drive = drive;
        Actuator = actuator;
    }
}

/// <summary>
/// The core engine that evaluates drives, resolves available actuators, 
/// selects one, and executes it.
/// </summary>
public sealed class StateMachine
{
    private readonly IActionResolver _resolver;
    private readonly IActionSelector _selector;
    private readonly NPC.Library.Messaging.MessageDispatcher _dispatcher;

    /// <summary>
    /// Fired whenever an actuator completes execution.
    /// </summary>
    public event EventHandler<ActuatorExecutedEventArgs>? OnActuatorExecuted;

    public StateMachine(IActionResolver resolver, IActionSelector selector, NPC.Library.Messaging.MessageDispatcher dispatcher)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Evaluates the character's drives, picks the most pressing need, 
    /// and attempts to execute one available actuator asynchronously.
    /// </summary>
    /// <returns>True if an action was executed, false otherwise.</returns>
    public async Task<bool> TickAsync(Character character)
    {
        var drives = character.Drives.Levels;
        if (drives.Count == 0) return false;

        var targetDrive = DriveType.Idle;
        
        // For Satiety and Thirst, lower is more pressing.
        // If both are low, we pick the lowest one to survive.
        decimal minLevel = 0.8m;
        
        if (drives.TryGetValue(DriveType.Satiety, out var satietyLevel) && satietyLevel < minLevel)
        {
            targetDrive = DriveType.Satiety;
            minLevel = satietyLevel;
        }
        
        if (drives.TryGetValue(DriveType.Thirst, out var thirstLevel) && thirstLevel < minLevel)
        {
            targetDrive = DriveType.Thirst;
            minLevel = thirstLevel;
        }

        if (drives.TryGetValue(DriveType.Social, out var socialLevel) && socialLevel < minLevel)
        {
            targetDrive = DriveType.Social;
            minLevel = socialLevel;
        }

        // Fatigue is reversed (high level means you are tired), so we flip it to compare.
        // We evaluate it against minLevel so that exhaustion overrides hunger.
        if (drives.TryGetValue(DriveType.Fatigue, out var fatigueLevel))
        {
            var restUrge = 1.0m - fatigueLevel; // 1.0 fatigue = 0.0 restUrge (extremely pressing)
            if (restUrge < minLevel)
            {
                targetDrive = DriveType.Fatigue;
                minLevel = restUrge;
            }
        }

        // CRITICAL LETHALITY OVERRIDES
        // If they are literally about to die of thirst or exhaustion, FORCE them to stop 
        // whatever they are doing and save themselves. This prevents them from dying while pathfinding.
        if (drives.TryGetValue(DriveType.Thirst, out var critThirst) && critThirst < 0.15m)
        {
            targetDrive = DriveType.Thirst;
        }
        else if (drives.TryGetValue(DriveType.Fatigue, out var critFatigue) && critFatigue > 0.85m)
        {
            targetDrive = DriveType.Fatigue;
        }

        if (targetDrive != character.TargetDrive)
        {
            character.TargetDrive = targetDrive;
            character.ActiveActuator = null;
        }

        return await TryExecuteForDriveAsync(character, targetDrive);
    }

    /// <summary>
    /// Attempts to execute an action for a specific drive asynchronously.
    /// </summary>
    public async Task<bool> TryExecuteForDriveAsync(Character character, DriveType drive)
    {
        if (character.ActiveActuator != null && character.ActiveActuator.CanExecute(character))
        {
            // character.LastAction is set by the actuator itself or previously
            await character.ActiveActuator.ExecuteAsync(character);
            OnActuatorExecuted?.Invoke(this, new ActuatorExecutedEventArgs(character, drive, character.ActiveActuator));
            return true;
        }

        var available = _resolver.GetAvailableActuators(drive, character)
                                 .Where(a => a.CanExecute(character));

        var chosen = _selector.Select(available, character, drive);
        if (chosen != null)
        {
            if (chosen.IsPersistent)
            {
                character.ActiveActuator = chosen;
            }
            else
            {
                character.ActiveActuator = null;
            }
            var newActionName = chosen.GetType().Name;
            if (character.LastAction != newActionName)
            {
                _dispatcher.DispatchImmediate(new NPC.Library.Messaging.ActuatorChangedMessage(character, character.LastAction ?? "None", newActionName));
                character.LastAction = newActionName;
            }
            
            await chosen.ExecuteAsync(character);
            
            // Firing the event after the async execution completes
            OnActuatorExecuted?.Invoke(this, new ActuatorExecutedEventArgs(character, drive, chosen));
            return true;
        }

        if (character.ActiveActuator != null)
        {
            // The active actuator yielded (CanExecute returned false).
        }

        character.ActiveActuator = null;
        return false;
    }
}
