namespace NPC.Village.Behaviors;

using NPC.Library.Behaviors;
using NPC.Library.State;
using NPC.Village.Memory;
using NPC.Library.Character.Components;


using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Character.Components;
using NPC.Library.Inventory;
using NPC.Library.Spatial;
using NPC.Library.Spatial.Grid;
using NPC.Library.State;

public class VillageStoreItemActuator : IActuator
{
    private readonly GridSpatialContext _spatialContext;

    public VillageStoreItemActuator(GridSpatialContext spatialContext)
    {
        _spatialContext = spatialContext;
    }

    public int GetPriority(Character character, DriveType currentDrive) => 50;

    public bool CanExecute(Character character)
    {
        if (character.TryGetComponent<IInventory>(out var inv) && character.TryGetComponent<NPC.Library.Memory.IMemory>(out var memory))
        {
            var chestLocs = memory.Recall(TileType.Chest).ToList();
            if (chestLocs.Count == 0) return false;

            var items = inv.GetItems().ToList();
            
            var bottles = items.OfType<WaterBottleItem>().ToList();
            var apples = items.Where(i => i.Type == ItemType.Apple).ToList();

            if (bottles.Count > 1 || apples.Count > 0)
            {
                return true;
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
            character.LastAction = "Going to Chest to Store Items";
            var path = _spatialContext.GetPath(loc.Value, chestLoc).ToList();
            if (path.Count > 0)
            {
                _spatialContext.MoveCharacter(character, path[0]);
            }
        }
        else
        {
            character.LastAction = "Storing Items in Chest";
            if (_spatialContext.Map.Chests.TryGetValue(chestLoc, out var chestInv))
            {
                var items = inv.GetItems().ToList();
                var bottles = items.OfType<WaterBottleItem>().ToList();
                var apples = items.Where(i => i.Type == ItemType.Apple).ToList();

                // Store excess bottles
                for (int i = 1; i < bottles.Count; i++)
                {
                    if (chestInv.AddItem(bottles[i]))
                    {
                        inv.RemoveItem(bottles[i]);
                    }
                }

                // Store apples
                foreach (var apple in apples)
                {
                    if (chestInv.AddItem(apple))
                    {
                        inv.RemoveItem(apple);
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}
