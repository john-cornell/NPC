namespace NPC.Village.Behaviors;

using NPC.Library.Behaviors;
using NPC.Library.State;


using System.Collections.Generic;
using NPC.Library.Character;
using NPC.Library.Spatial;
using NPC.Library.State;

public class VillageActuatorGroup : ActuatorGroup
{
    private readonly ISpatialContext _spatialContext;
    private readonly NPC.Library.Messaging.MessageDispatcher _dispatcher;

    public VillageActuatorGroup(ISpatialContext spatialContext, NPC.Library.Messaging.MessageDispatcher dispatcher)
    {
        _spatialContext = spatialContext;
        _dispatcher = dispatcher;
    }

    public override IEnumerable<IActuator> GetAvailableActuators(DriveType targetDrive, NPC.Library.Character.Character character)
    {
        var list = new List<IActuator>();
        list.Add(new RestActuator());
        list.Add(new VillageSleepInBedActuator(_spatialContext));

        if (targetDrive == DriveType.Idle)
        {
            list.Add(new VillageWanderActuator(_spatialContext));
            list.Add(new VillageGatherFoodActuator(_spatialContext));
            list.Add(new VillageGatherWaterActuator(_spatialContext));
            list.Add(new VillageSocializeActuator(_spatialContext, _dispatcher));
            if (_spatialContext is NPC.Library.Spatial.Grid.GridSpatialContext gridCtx)
            {
                list.Add(new VillageStoreItemActuator(gridCtx));
            }
        }

        if (targetDrive == DriveType.Social)
        {
            list.Add(new VillageWanderActuator(_spatialContext)); // Just wanders looking for people
            list.Add(new VillageSocializeActuator(_spatialContext, _dispatcher));
        }

        if (targetDrive == DriveType.Satiety)
        {
            list.Add(new SearchForFoodActuator(_spatialContext));
            list.Add(new VillageGatherFoodActuator(_spatialContext));
            list.Add(new EatActuator());
            if (_spatialContext is NPC.Library.Spatial.Grid.GridSpatialContext gridCtx)
            {
                list.Add(new VillageRetrieveItemActuator(gridCtx, NPC.Library.Inventory.ItemType.Apple));
            }
        }

        if (targetDrive == DriveType.Thirst)
        {
            list.Add(new SearchForFoodActuator(_spatialContext)); // Just wanders
            list.Add(new VillageGatherWaterActuator(_spatialContext));
            list.Add(new DrinkActuator());
            if (_spatialContext is NPC.Library.Spatial.Grid.GridSpatialContext gridCtx)
            {
                list.Add(new VillageRetrieveItemActuator(gridCtx, NPC.Library.Inventory.ItemType.WaterBottle));
            }
        }

        return list;
    }
}
