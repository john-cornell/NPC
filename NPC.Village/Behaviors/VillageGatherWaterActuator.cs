namespace NPC.Village.Behaviors;

using NPC.Library.Behaviors;
using NPC.Library.State;
using NPC.Village.Memory;
using NPC.Library.Character.Components;


using System;
using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Inventory;
using NPC.Library.Memory;
using NPC.Library.Spatial;
using NPC.Library.Spatial.Grid;
using NPC.Library.State;

public class VillageGatherWaterActuator : IActuator
{
    private readonly ISpatialContext _spatialContext;
    public VillageGatherWaterActuator(ISpatialContext spatialContext)
    {
        _spatialContext = spatialContext;
    }

    public int GetPriority(NPC.Library.Character.Character character, DriveType currentDrive)
    {
        if (currentDrive == DriveType.Thirst) return 50;
        return 0;
    }

    public bool CanExecute(NPC.Library.Character.Character character)
    {
        // If we are thirsty, and we already have water, we should drink it instead of gathering more!
        if (character.TargetDrive == DriveType.Thirst && 
            character.TryGetComponent<IInventory>(out var inv))
        {
            var waterBottle = inv.GetItems().OfType<WaterBottleItem>().FirstOrDefault();
            if (waterBottle != null && waterBottle.SipsRemaining > 0)
            {
                return false;
            }
        }

        // Only if we have memory of water AND we have an empty or partially empty water bottle
        if (character.TryGetComponent<IMemory>(out var memory) && character.TryGetComponent<IInventory>(out var inventory))
        {
            var waterTiles = memory.Recall(TileType.Water).Concat(memory.Recall(TileType.Well)).ToList();
            var waterBottle = inventory.GetItems().OfType<WaterBottleItem>().FirstOrDefault();
            
            if (waterTiles.Any() && waterBottle != null && waterBottle.SipsRemaining < 3)
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

        var waterBottle = inventory.GetItems().OfType<WaterBottleItem>().FirstOrDefault();
        if (waterBottle == null) return Task.CompletedTask;

        var waterTiles = memory.Recall(TileType.Water).Concat(memory.Recall(TileType.Well)).ToList();
        if (waterTiles.Count == 0) return Task.CompletedTask;

        // Pathfind to nearest known water
        var targetWater = waterTiles.OrderBy(t => Math.Abs(t.X - loc.Value.X) + Math.Abs(t.Y - loc.Value.Y)).First();
        character.CurrentDestination = targetWater;

        int dx = Math.Abs(loc.Value.X - targetWater.X);
        int dy = Math.Abs(loc.Value.Y - targetWater.Y);
        
        if (dx <= 1 && dy <= 1)
        {
            // Adjacent! Refill the bottle!
            character.CurrentDestination = null;
            character.LastAction = "Refilling Water Bottle";
            waterBottle.Refill();
            if (character.TryGetComponent<CharacterMetrics>(out var metrics))
            {
                metrics.WaterCollected++;
            }
        }
        else
        {
            // Find a walkable tile adjacent to the targetWater
            var candidates = new[] { 
                (targetWater.X, targetWater.Y - 1), 
                (targetWater.X, targetWater.Y + 1), 
                (targetWater.X - 1, targetWater.Y), 
                (targetWater.X + 1, targetWater.Y) 
            };
            
            var gridCtx = _spatialContext as GridSpatialContext;
            if (gridCtx == null) return Task.CompletedTask;

            var validTargets = candidates.Where(c => 
                c.Item1 >= 0 && c.Item1 < gridCtx.Map.Width &&
                c.Item2 >= 0 && c.Item2 < gridCtx.Map.Height &&
                gridCtx.Map.Tiles[c.Item1, c.Item2] != TileType.Water)
                .OrderBy(c => Math.Abs(c.Item1 - loc.Value.X) + Math.Abs(c.Item2 - loc.Value.Y))
                .ToList();

            if (validTargets.Count == 0)
            {
                // Unreachable water! Forget it
                character.LastAction = "Failed to Gather (Unreachable)";
                memory.Forget(TileType.Water, targetWater.X, targetWater.Y);
                return Task.CompletedTask;
            }

            var actualTarget = validTargets.First();
            character.LastAction = "Pathfinding to Water";
            var path = _spatialContext.GetPath(loc.Value, actualTarget).ToList();
            if (path.Count > 0) 
            {
                _spatialContext.MoveCharacter(character, path[0]);
            }
            else if (loc.Value == actualTarget)
            {
                // We are already at the adjacent tile, but somehow didn't trigger adjacency?
                // The dx <= 1 && dy <= 1 above should have caught this.
            }
        }

        return Task.CompletedTask;
    }
}
