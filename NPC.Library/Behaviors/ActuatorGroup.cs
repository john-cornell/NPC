namespace NPC.Library.Behaviors;

using System.Collections.Generic;
using System.Linq;
using NPC.Library.Character;
using NPC.Library.State;

/// <summary>
/// A base class for grouping related actuators together.
/// </summary>
public abstract class ActuatorGroup : IActionResolver
{
    protected readonly List<IActuator> Actuators = new();

    public abstract IEnumerable<IActuator> GetAvailableActuators(DriveType targetDrive, NPC.Library.Character.Character character);
}
