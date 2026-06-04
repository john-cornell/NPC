namespace NPC.Library.Behaviors.AI;

using System;
using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Character.Components;
using NPC.Library.Spatial;
using NPC.Library.Inventory;

public class GoblinAIController : IAIController
{
    private readonly ISpatialContext _spatialContext;
    private readonly Random _random = new Random();

    public GoblinAIController(ISpatialContext spatialContext)
    {
        _spatialContext = spatialContext;
    }

    private (int X, int Y, int Z)? _wanderTarget = null;
    private int _tickCounter = 0;
    
    public (int X, int Y, int Z) HomeLocation { get; set; } = (110, 50, -1);

    public Task TickAsync(Character character)
    {
        // 50% speed reduction: only process AI every other tick
        if (_tickCounter++ % 2 != 0)
        {
            return Task.CompletedTask;
        }

        var stats = character.GetComponent<StatsComponent>();
        if (stats != null && stats.CurrentHP < stats.MaxHP * 0.2f)
        {
            character.LastAction = "Fleeing";
            MoveDirected(character);
            return Task.CompletedTask;
        }

        var myLoc = _spatialContext.GetCharacterLocation(character);
        if (myLoc != null)
        {
            var chars = _spatialContext.GetCharacters().Where(c => c != character && !c.IsDead).ToList();
            
            // Find closest target
            Character closestTarget = null;
            int minDistance = 15; // Aggro radius
            
            foreach (var target in chars)
            {
                var tLoc = _spatialContext.GetCharacterLocation(target);
                if (tLoc != null && tLoc.Value.Z == myLoc.Value.Z)
                {
                    bool isGoblin = target.Name.Contains("Goblin");
                    if (!isGoblin) // Only aggro on non-goblins (player, animals, etc)
                    {
                        int dist = Math.Abs(tLoc.Value.X - myLoc.Value.X) + Math.Abs(tLoc.Value.Y - myLoc.Value.Y);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            closestTarget = target;
                        }
                    }
                }
            }
            
            // If we have a target, chase or attack
            if (closestTarget != null)
            {
                var tLoc = _spatialContext.GetCharacterLocation(closestTarget).Value;
                int dx = Math.Abs(tLoc.X - myLoc.Value.X);
                int dy = Math.Abs(tLoc.Y - myLoc.Value.Y);
                
                if (dx <= 1 && dy <= 1)
                {
                    character.LastAction = "Attacking"; // Trigger combat flair
                    Attack(character, closestTarget, myLoc.Value);
                    return Task.CompletedTask;
                }
                else
                {
                    character.LastAction = "Chasing";
                    MoveTo(character, myLoc.Value, tLoc);
                    return Task.CompletedTask;
                }
            }
            
            // Random infighting adjacent (10% chance if goblin is next to us)
            foreach (var target in chars)
            {
                var tLoc = _spatialContext.GetCharacterLocation(target);
                if (tLoc != null && tLoc.Value.Z == myLoc.Value.Z && target.Name.Contains("Goblin"))
                {
                    if (Math.Abs(tLoc.Value.X - myLoc.Value.X) <= 1 && Math.Abs(tLoc.Value.Y - myLoc.Value.Y) <= 1)
                    {
                        if (_random.NextDouble() < 0.05) // Reduced to 5% chance
                        {
                            character.LastAction = "Attacking";
                            Attack(character, target, myLoc.Value);
                            return Task.CompletedTask;
                        }
                    }
                }
            }
        }

        // Sleep: 10% chance to just stay still
        if (_random.NextDouble() < 0.1)
        {
            character.LastAction = "Sleeping";
            return Task.CompletedTask;
        }

        // Wander
        character.LastAction = "Wandering";
        MoveDirected(character);
        return Task.CompletedTask;
    }

    private void MoveTo(Character character, (int X, int Y, int Z) myLoc, (int X, int Y, int Z) targetLoc)
    {
        var path = _spatialContext.GetPath(myLoc, targetLoc).ToList();
        if (path.Count > 0)
        {
            var nextStep = path[0];
            bool occupied = _spatialContext.GetCharacters().Any(c => 
                !c.IsDead && c != character && _spatialContext.GetCharacterLocation(c) == nextStep);
            
            if (!occupied)
            {
                _spatialContext.MoveCharacter(character, nextStep);
            }
            else
            {
                _wanderTarget = null; // Blocked, re-evaluate
            }
        }
    }

    private void MoveDirected(Character character)
    {
        var loc = _spatialContext.GetCharacterLocation(character);
        if (loc != null)
        {
            if (_wanderTarget == null || _wanderTarget.Value == loc.Value)
            {
                int tx, ty;
                if (_random.NextDouble() < 0.75)
                {
                    // 75% chance: stay in favored room (small radius around home)
                    tx = HomeLocation.X + _random.Next(-4, 5);
                    ty = HomeLocation.Y + _random.Next(-4, 5);
                }
                else
                {
                    // 25% chance: wander outside (larger radius around home)
                    tx = HomeLocation.X + _random.Next(-15, 16);
                    ty = HomeLocation.Y + _random.Next(-15, 16);
                }
                
                // Ensure target is on a walkable tile
                var tile = _spatialContext.GetTile((tx, ty, loc.Value.Z));
                if (tile == NPC.Library.Spatial.Grid.TileType.CaveFloor || tile == NPC.Library.Spatial.Grid.TileType.Floor || tile == NPC.Library.Spatial.Grid.TileType.Grass)
                {
                    _wanderTarget = (tx, ty, loc.Value.Z);
                }
            }

            if (_wanderTarget != null)
            {
                MoveTo(character, loc.Value, _wanderTarget.Value);
            }
        }
    }

    private void Attack(Character attacker, Character defender, (int X, int Y, int Z) myLoc)
    {
        var attackerStats = attacker.GetComponent<StatsComponent>();
        var defenderStats = defender.GetComponent<StatsComponent>();
        if (attackerStats != null && defenderStats != null)
        {
            // Simple 1d4 + Str damage
            int damage = _random.Next(1, 5) + (attackerStats.Strength - 10) / 2;
            if (damage < 1) damage = 1;

            defenderStats.TakeDamage(damage);
            if (defenderStats.CurrentHP <= 0)
            {
                defender.IsDead = true;
                defender.DeathReason = "Killed by " + attacker.Name;
                defender.DeathTick = 0; // Usually set by SimulationEngine, but we can bypass for now.
                
                DropLoot(defender);
            }
        }
        else if (defenderStats != null)
        {
             // Base 1 damage if attacker has no stats
             defenderStats.TakeDamage(1);
             if (defenderStats.CurrentHP <= 0)
             {
                 defender.IsDead = true;
                 defender.DeathReason = "Killed by " + attacker.Name;
                 DropLoot(defender);
             }
        }
    }
    
    private void DropLoot(Character defender)
    {
        var loc = _spatialContext.GetCharacterLocation(defender);
        if (loc == null) return;
        
        // 10% chance to drop a dagger
        if (_random.NextDouble() < 0.1)
        {
            _spatialContext.DropItem(loc.Value, new DaggerItem());
        }
        
        // Drop gold (0-5 weighted to 0)
        // A simple way is to roll multiple times or use a power curve
        double roll = _random.NextDouble();
        int gold = 0;
        if (roll > 0.95) gold = 5;
        else if (roll > 0.85) gold = 4;
        else if (roll > 0.7) gold = 3;
        else if (roll > 0.5) gold = 2;
        else if (roll > 0.3) gold = 1;
        
        if (gold > 0)
        {
            _spatialContext.DropItem(loc.Value, new GoldItem(gold));
        }
    }
}
