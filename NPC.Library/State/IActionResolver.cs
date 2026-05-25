namespace NPC.Library.State;

using System.Collections.Generic;
using NPC.Library.Character;

/// <summary>
/// Looks up available actuators for a given drive from across the system.
/// </summary>
public interface IActionResolver
{
    /// <summary>
    /// Returns 0 to N actuators capable of addressing the given drive state.
    /// </summary>
    IEnumerable<IActuator> GetAvailableActuators(DriveType drive, Character character);
}
