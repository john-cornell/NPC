namespace NPC.Library.Behaviors;

using System.Collections.Generic;
using NPC.Library.Character;
using NPC.Library.Spatial;
using NPC.Library.State;

public class TestActuatorGroup : ActuatorGroup
{
    public TestActuatorGroup(ISpatialContext spatialContext)
    {
        Actuators.Add(new WanderActuator(spatialContext));
    }

    public override IEnumerable<IActuator> GetAvailableActuators(DriveType targetDrive, NPC.Library.Character.Character character)
    {
        return Actuators;
    }
}
