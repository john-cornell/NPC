namespace NPC.Library.Behaviors;

using System.Collections.Generic;
using NPC.Library.Character;
using NPC.Library.Spatial;
using NPC.Library.State;

public class SurvivalActuatorGroup : ActuatorGroup
{
    private readonly ISpatialContext _spatialContext;

    public SurvivalActuatorGroup(ISpatialContext spatialContext)
    {
        _spatialContext = spatialContext;
    }

    public override IEnumerable<IActuator> GetAvailableActuators(DriveType targetDrive, NPC.Library.Character.Character character)
    {
        var list = new List<IActuator>();
        list.Add(new RestActuator());

        if (targetDrive == DriveType.Idle)
        {
            list.Add(new WanderActuator(_spatialContext));
            list.Add(new GatherFoodActuator(_spatialContext));
            list.Add(new GatherWaterActuator(_spatialContext));
        }

        if (targetDrive == DriveType.Satiety)
        {
            list.Add(new SearchForFoodActuator(_spatialContext));
            list.Add(new GatherFoodActuator(_spatialContext));
            list.Add(new EatActuator());
        }

        if (targetDrive == DriveType.Thirst)
        {
            // Same search actuator can be used if they just wander randomly when thirsty
            // Or we could have a specific SearchForWaterActuator, but for now they just wander
            list.Add(new SearchForFoodActuator(_spatialContext)); // Just wanders
            list.Add(new GatherWaterActuator(_spatialContext));
            list.Add(new DrinkActuator());
        }

        return list;
    }
}
