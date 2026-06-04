namespace NPC.Library.State;

using System.Collections.Generic;
using NPC.Library.Character;

public class SimpleActuatorGroup : IActionResolver
{
    private readonly List<IActuator> _actuators;

    public SimpleActuatorGroup(IEnumerable<IActuator> actuators)
    {
        _actuators = new List<IActuator>(actuators);
    }

    public IEnumerable<IActuator> GetAvailableActuators(DriveType currentDrive, Character character)
    {
        return _actuators;
    }
}
