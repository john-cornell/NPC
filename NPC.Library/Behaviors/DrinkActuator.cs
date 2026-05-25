namespace NPC.Library.Behaviors;

using System.Linq;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Inventory;
using NPC.Library.State;

public class DrinkActuator : IActuator
{
    public string Name => "Drink";
    public string Description => "Drinking from a water bottle.";
    public int GetPriority(NPC.Library.Character.Character character, DriveType currentDrive) => 100;

    public bool CanExecute(NPC.Library.Character.Character character)
    {
        if (character.TryGetComponent<IInventory>(out var inventory))
        {
            var waterBottle = inventory.GetItems().OfType<WaterBottleItem>().FirstOrDefault();
            return waterBottle != null && waterBottle.SipsRemaining > 0;
        }
        return false;
    }

    public Task ExecuteAsync(NPC.Library.Character.Character character)
    {
        character.LastAction = "Drinking Water";
        if (character.TryGetComponent<IInventory>(out var inventory))
        {
            var waterBottle = inventory.GetItems().OfType<WaterBottleItem>().FirstOrDefault();
            if (waterBottle != null && waterBottle.SipsRemaining > 0)
            {
                waterBottle.Drink();
                
                // Increase thirst level (reduce need)
                if (character.Drives.TryGetLevel(DriveType.Thirst, out var currentThirst))
                {
                    character.Drives[DriveType.Thirst] = System.Math.Min(1.0m, currentThirst + 0.5m);
                }
                if (character.TryGetComponent<NPC.Library.Character.Components.CharacterMetrics>(out var metrics))
                {
                    metrics.SipsTaken++;
                }
            }
        }
        return Task.CompletedTask;
    }
}
