namespace NPC.Village.Behaviors;

using NPC.Library.Behaviors;
using NPC.Library.State;
using NPC.Village.Memory;
using NPC.Library.Character.Components;


using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Inventory;
using NPC.Library.Spatial.Grid;
using NPC.Library.State;

public class VillageRetrieveItemActuator : IActuator
{
    private readonly GridSpatialContext _spatialContext;
    private readonly ItemType _targetItem;

    public VillageRetrieveItemActuator(GridSpatialContext spatialContext, ItemType targetItem)
    {
        _spatialContext = spatialContext;
        _targetItem = targetItem;
    }

    public int GetPriority(Character character, DriveType currentDrive) => 75;

    public bool CanExecute(Character character)
    {
        if (character.TryGetComponent<IInventory>(out var inv) && character.TryGetComponent<NPC.Library.Memory.IMemory>(out var memory))
        {
            var chestLocs = memory.Recall(TileType.Chest).ToList();
            if (chestLocs.Count == 0) return false;

            var chestLoc = chestLocs.First();
            if (_spatialContext.Map.Chests.TryGetValue(chestLoc, out var chestInv))
            {
                // Check if chest actually has the item
                bool hasItem = chestInv.GetItems().Any(i => i.Type == _targetItem);
                
                // If we already have the item in our inventory, no need to retrieve
                bool needItem = !inv.GetItems().Any(i => i.Type == _targetItem);
                
                // For water bottles specifically, check if we only have empty ones
                if (_targetItem == ItemType.WaterBottle)
                {
                    var bottles = inv.GetItems().OfType<WaterBottleItem>();
                    needItem = !bottles.Any(b => b.SipsRemaining > 0);
                    
                    hasItem = chestInv.GetItems().OfType<WaterBottleItem>().Any(b => b.SipsRemaining > 0);
                }

                if (hasItem && needItem)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public Task ExecuteAsync(Character character)
    {
        if (!character.TryGetComponent<IInventory>(out var inv) || !character.TryGetComponent<NPC.Library.Memory.IMemory>(out var memory))
            return Task.CompletedTask;

        var loc = _spatialContext.GetCharacterLocation(character);
        if (loc == null) return Task.CompletedTask;

        var chestLoc = memory.Recall(TileType.Chest).First();

        // If not adjacent to chest, pathfind to it
        int dist = System.Math.Abs(loc.Value.X - chestLoc.X) + System.Math.Abs(loc.Value.Y - chestLoc.Y);
        if (dist > 1)
        {
            character.LastAction = $"Going to Chest for {_targetItem}";
            var path = _spatialContext.GetPath(loc.Value, chestLoc).ToList();
            if (path.Count > 0)
            {
                _spatialContext.MoveCharacter(character, path[0]);
            }
        }
        else
        {
            character.LastAction = $"Retrieving {_targetItem} from Chest";
            if (_spatialContext.Map.Chests.TryGetValue(chestLoc, out var chestInv))
            {
                IItem? itemToTake = null;
                if (_targetItem == ItemType.WaterBottle)
                {
                    itemToTake = chestInv.GetItems().OfType<WaterBottleItem>().FirstOrDefault(b => b.SipsRemaining > 0);
                }
                else
                {
                    itemToTake = chestInv.GetItems().FirstOrDefault(i => i.Type == _targetItem);
                }

                if (itemToTake != null && inv.AddItem(itemToTake))
                {
                    chestInv.RemoveItem(itemToTake);
                }
            }
        }

        return Task.CompletedTask;
    }
}
