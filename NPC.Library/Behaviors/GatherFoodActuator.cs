namespace NPC.Library.Behaviors;

using System;
using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Inventory;
using NPC.Library.Memory;
using NPC.Library.Spatial;
using NPC.Library.Spatial.Grid;
using NPC.Library.State;

public class GatherFoodActuator : IActuator
{
    private readonly ISpatialContext _spatialContext;

    public GatherFoodActuator(ISpatialContext spatialContext)
    {
        _spatialContext = spatialContext;
    }

    public int GetPriority(NPC.Library.Character.Character character, DriveType currentDrive)
    {
        if (currentDrive == DriveType.Satiety) return 50;
        return 0; // If Idle, it doesn't hijack!
    }

    public bool CanExecute(NPC.Library.Character.Character character)
    {
        // If we are hungry, and we already have an apple, we should eat it instead of gathering more!
        if (character.TargetDrive == DriveType.Satiety && 
            character.TryGetComponent<IInventory>(out var inv) && 
            inv.HasItem(ItemType.Apple))
        {
            return false;
        }

        // Only if we have memory of an apple tree
        if (character.TryGetComponent<IMemory>(out var memory))
        {
            var trees = memory.Recall(TileType.AppleTree);
            if (trees.Any())
            {
                return true;
            }
        }
        return false;
    }

    public Task ExecuteAsync(NPC.Library.Character.Character character)
    {
        if (!character.TryGetComponent<IMemory>(out var memory)) return Task.CompletedTask;
        if (!character.TryGetComponent<IInventory>(out var inventory)) return Task.CompletedTask;

        var loc = _spatialContext.GetCharacterLocation(character);
        if (loc == null) return Task.CompletedTask;

        // Simple approach: get the first known tree
        var trees = memory.Recall(TileType.AppleTree).ToList();
        if (trees.Count == 0) return Task.CompletedTask;

        var targetTree = trees[0]; // In the future, could find nearest
        character.CurrentDestination = targetTree;

        // Are we adjacent?
        int dx = Math.Abs(loc.Value.X - targetTree.X);
        int dy = Math.Abs(loc.Value.Y - targetTree.Y);
        
        if (dx <= 1 && dy <= 1)
        {
            // Adjacent! Grab an apple!
            character.CurrentDestination = null;
            
            if (_spatialContext.TryGatherApple(targetTree))
            {
                character.LastAction = "Gathering Apple";
                inventory.AddItem(new Item(ItemType.Apple));
            }
            else
            {
                character.LastAction = "Failed to Gather (Depleted)";
                memory.Forget(TileType.AppleTree, targetTree.X, targetTree.Y);
            }
        }
        else
        {
            character.LastAction = "Pathfinding to Apple Tree";
            var path = _spatialContext.GetPath(loc.Value, targetTree).ToList();
            if (path.Count > 0) 
            {
                _spatialContext.MoveCharacter(character, path[0]);
            }
        }

        return Task.CompletedTask;
    }
}
