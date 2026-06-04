namespace NPC.Library.Behaviors.Player;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Spatial;
using NPC.Library.Inventory;
using NPC.Library.Spatial.Grid;

public enum PlayerIntent
{
    None,
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Interact,
    Eat,
    Drink
}

public class PlayerController : IPlayerController
{
    private readonly ISpatialContext _spatialContext;
    private readonly ConcurrentQueue<PlayerIntent> _intentQueue = new();
    private PlayerIntent _currentMovementIntent = PlayerIntent.None;

    public PlayerController(ISpatialContext spatialContext)
    {
        _spatialContext = spatialContext;
    }

    public void EnqueueIntent(PlayerIntent intent)
    {
        if (_intentQueue.Count < 2)
        {
            _intentQueue.Enqueue(intent);
        }
    }

    public void SetMovementIntent(PlayerIntent intent)
    {
        _currentMovementIntent = intent;
    }

    public Task TickAsync(Character character)
    {
        if (_intentQueue.TryDequeue(out var intent))
        {
            switch (intent)
            {
                case PlayerIntent.Eat:
                    if (character.TryGetComponent<IInventory>(out var inv) && inv.HasItem(ItemType.Apple))
                    {
                        inv.ConsumeItem(ItemType.Apple);
                        if (character.Drives.TryGetLevel(DriveType.Satiety, out var s))
                            character.Drives[DriveType.Satiety] = Math.Min(1.0m, s + 0.10m);
                        if (character.TryGetComponent<NPC.Library.Character.Components.CharacterMetrics>(out var metrics))
                            metrics.ApplesEaten++;
                        character.LastAction = "Eating Apple";
                    }
                    break;
                case PlayerIntent.Drink:
                    var dLoc = _spatialContext.GetCharacterLocation(character);
                    bool nearWell = false;
                    if (dLoc.HasValue)
                    {
                        var adjs = new[]
                        {
                            dLoc.Value,
                            (X: dLoc.Value.X, Y: dLoc.Value.Y - 1, Z: dLoc.Value.Z),
                            (X: dLoc.Value.X, Y: dLoc.Value.Y + 1, Z: dLoc.Value.Z),
                            (X: dLoc.Value.X - 1, Y: dLoc.Value.Y, Z: dLoc.Value.Z),
                            (X: dLoc.Value.X + 1, Y: dLoc.Value.Y, Z: dLoc.Value.Z)
                        };
                        foreach (var a in adjs)
                        {
                            var t = _spatialContext.GetTile(a);
                            if (t == TileType.Well || t == TileType.Water) nearWell = true;
                        }
                    }

                    if (nearWell)
                    {
                        if (character.Drives.TryGetLevel(DriveType.Thirst, out var t))
                        {
                            character.Drives[DriveType.Thirst] = Math.Min(1.0m, t + 0.50m);
                            character.LastAction = "Drinking Water from Well";
                            if (character.TryGetComponent<NPC.Library.Character.Components.CharacterMetrics>(out var metrics2))
                                metrics2.SipsTaken++;
                        }
                    }
                    else if (character.TryGetComponent<IInventory>(out var inv2))
                    {
                        var bottle = inv2.GetItems().OfType<WaterBottleItem>().FirstOrDefault(b => b.SipsRemaining > 0);
                        if (bottle != null)
                        {
                            bottle.Drink();
                            if (bottle.SipsRemaining == 0) inv2.RemoveItem(bottle);
                            if (character.Drives.TryGetLevel(DriveType.Thirst, out var t))
                                character.Drives[DriveType.Thirst] = Math.Min(1.0m, t + 0.25m);
                            if (character.TryGetComponent<NPC.Library.Character.Components.CharacterMetrics>(out var metrics2))
                                metrics2.SipsTaken++;
                            character.LastAction = "Drinking Water from Bottle";
                        }
                    }
                    break;
                case PlayerIntent.Interact:
                    Interact(character);
                    break;
            }
        }
        else if (_currentMovementIntent != PlayerIntent.None)
        {
            switch (_currentMovementIntent)
            {
                case PlayerIntent.MoveUp: Move(character, 0, -1); break;
                case PlayerIntent.MoveDown: Move(character, 0, 1); break;
                case PlayerIntent.MoveLeft: Move(character, -1, 0); break;
                case PlayerIntent.MoveRight: Move(character, 1, 0); break;
            }
        }
        else
        {
            character.LastAction = "Idle";
        }
        return Task.CompletedTask;
    }

    private void Move(Character character, int dx, int dy)
    {
        var loc = _spatialContext.GetCharacterLocation(character);
        if (loc.HasValue)
        {
            var target = (X: loc.Value.X + dx, Y: loc.Value.Y + dy, Z: loc.Value.Z);
            
            // Check for combat bump
            var chars = _spatialContext.GetCharacters().Where(c => c != character && !c.IsDead).ToList();
            var targetCharacter = chars.FirstOrDefault(c => 
            {
                var cLoc = _spatialContext.GetCharacterLocation(c);
                return cLoc.HasValue && cLoc.Value == target;
            });
            
            if (targetCharacter != null)
            {
                character.LastAction = "Attacking " + targetCharacter.Name;
                Attack(character, targetCharacter);
                return;
            }

            var tile = _spatialContext.GetTile(target);
            if (tile == TileType.Grass || tile == TileType.Road || tile == TileType.Floor || tile == TileType.Door || tile == TileType.House || tile == TileType.Bed || tile == TileType.CaveFloor || tile == TileType.CaveEntrance || tile == TileType.StairsUp)
            {
                _spatialContext.MoveCharacter(character, target);
                character.LastAction = "Walking";
            }
        }
    }

    private void Attack(Character attacker, Character defender)
    {
        var attackerStats = attacker.GetComponent<NPC.Library.Character.Components.StatsComponent>();
        var defenderStats = defender.GetComponent<NPC.Library.Character.Components.StatsComponent>();
        
        if (attackerStats != null && defenderStats != null)
        {
            int damage = Random.Shared.Next(1, 5) + (attackerStats.Strength - 10) / 2;
            if (damage < 1) damage = 1;
            defenderStats.TakeDamage(damage);
            
            if (defenderStats.CurrentHP <= 0)
            {
                defender.IsDead = true;
                defender.DeathReason = "Killed by " + attacker.Name;
                DropLoot(defender);
            }
        }
        else if (defenderStats != null)
        {
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
        
        if (Random.Shared.NextDouble() < 0.1)
        {
            _spatialContext.DropItem(loc.Value, new DaggerItem());
        }
        
        double roll = Random.Shared.NextDouble();
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

    private void Interact(Character character)
    {
        var loc = _spatialContext.GetCharacterLocation(character);
        if (!loc.HasValue) return;

        Console.WriteLine($"[Interact] Player at {loc.Value}");

        var adjacents = new[]
        {
            loc.Value, // Check the tile we are standing on FIRST (like CaveEntrance / Stairs)
            (X: loc.Value.X, Y: loc.Value.Y - 1, Z: loc.Value.Z),
            (X: loc.Value.X, Y: loc.Value.Y + 1, Z: loc.Value.Z),
            (X: loc.Value.X - 1, Y: loc.Value.Y, Z: loc.Value.Z),
            (X: loc.Value.X + 1, Y: loc.Value.Y, Z: loc.Value.Z)
        };

        foreach (var adj in adjacents)
        {
            var tile = _spatialContext.GetTile(adj);
            Console.WriteLine($"[Interact] Checking adjacent {adj} -> {tile}");
            
            if (tile == TileType.AppleTree)
            {
                if (_spatialContext.TryGatherApple(adj))
                {
                    if (character.TryGetComponent<IInventory>(out var inv))
                    {
                        inv.AddItem(new Item(ItemType.Apple));
                        character.LastAction = "Gathering Apple";
                        if (character.TryGetComponent<NPC.Library.Character.Components.CharacterMetrics>(out var metrics))
                            metrics.ApplesCollected++;
                        Console.WriteLine($"[Interact] Gathered Apple at {adj}");
                        return;
                    }
                }
            }
            else if (tile == TileType.Water || tile == TileType.Well)
            {
                if (character.TryGetComponent<IInventory>(out var inv))
                {
                    var emptyBottle = inv.GetItems().OfType<WaterBottleItem>().FirstOrDefault(b => b.SipsRemaining < 3);
                    if (emptyBottle != null)
                    {
                        emptyBottle.Refill();
                        character.LastAction = "Refilled Water Bottle";
                        if (character.TryGetComponent<NPC.Library.Character.Components.CharacterMetrics>(out var metrics))
                            metrics.WaterCollected++;
                        Console.WriteLine($"[Interact] Refilled bottle at {adj}");
                        return;
                    }
                    else if (inv.GetItems().Count(i => i is WaterBottleItem w && w.SipsRemaining == 3) < 5)
                    {
                        inv.AddItem(new WaterBottleItem(3));
                        character.LastAction = "Collected New Water Bottle";
                        if (character.TryGetComponent<NPC.Library.Character.Components.CharacterMetrics>(out var metrics))
                            metrics.WaterCollected++;
                        Console.WriteLine($"[Interact] Collected new water bottle at {adj}");
                        return;
                    }
                }
            }
            else if (tile == TileType.Bed)
            {
                if (character.Drives.TryGetLevel(DriveType.Fatigue, out var f))
                {
                    character.Drives[DriveType.Fatigue] = Math.Max(0.0m, f - 0.30m);
                    character.LastAction = "Sleeping";
                    Console.WriteLine($"[Interact] Slept at {adj}");
                    return;
                }
            }
            else if (tile == TileType.Chest)
            {
                var chest = _spatialContext.GetChest(adj);
                if (chest != null && character.TryGetComponent<IInventory>(out var charInv))
                {
                    if (chest.HasItem(ItemType.Apple))
                    {
                        var apple = chest.ConsumeItem(ItemType.Apple);
                        if (apple != null) charInv.AddItem(apple);
                        character.LastAction = "Took Apple from Chest";
                    }
                    else if (charInv.HasItem(ItemType.Apple))
                    {
                        var apple = charInv.ConsumeItem(ItemType.Apple);
                        if (apple != null) chest.AddItem(apple);
                        character.LastAction = "Stored Apple in Chest";
                    }
                    Console.WriteLine($"[Interact] Used chest at {adj}");
                    return;
                }
            }
            else if (tile == TileType.CaveEntrance)
            {
                _spatialContext.MoveCharacter(character, (adj.X, adj.Y, -1));
                character.LastAction = "Entered Cave";
                Console.WriteLine($"[Interact] Entered Cave at {adj}. Player moved to Z=-1.");
                return;
            }
            else if (tile == TileType.StairsUp)
            {
                _spatialContext.MoveCharacter(character, (adj.X, adj.Y, 0));
                character.LastAction = "Climbed Stairs";
                Console.WriteLine($"[Interact] Climbed Stairs at {adj}. Player moved to Z=0.");
                return;
            }
        }
        Console.WriteLine($"[Interact] No actionable tile found near {loc.Value}");
    }
}
