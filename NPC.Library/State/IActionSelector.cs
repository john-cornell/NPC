namespace NPC.Library.State;

using System.Collections.Generic;
using NPC.Library.Character;

/// <summary>
/// Selects a single actuator to perform from a list of possibilities.
/// </summary>
public interface IActionSelector
{
    /// <summary>
    /// Selects one actuator from the available choices using the character's context and current drive.
    /// Returns null if the collection is empty.
    /// </summary>
    IActuator? Select(IEnumerable<IActuator> actuators, Character character, DriveType currentDrive);
}
