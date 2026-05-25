namespace NPC.Library.Behaviors;

using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Inventory;
using NPC.Library.State;

public class EatActuator : IActuator
{
    public int GetPriority(NPC.Library.Character.Character character, DriveType currentDrive) => 100;

    public bool CanExecute(NPC.Library.Character.Character character)
    {
        if (character.TryGetComponent<IInventory>(out var inventory))
        {
            return inventory.HasItem(ItemType.Apple);
        }
        return false;
    }

    public Task ExecuteAsync(NPC.Library.Character.Character character)
    {
        character.LastAction = "Eating Apple";
        if (character.TryGetComponent<IInventory>(out var inventory))
        {
            var apple = inventory.ConsumeItem(ItemType.Apple);
            if (apple != null)
            {
                // Increase satiety
                if (character.Drives.TryGetLevel(DriveType.Satiety, out var currentSatiety))
                {
                    character.Drives[DriveType.Satiety] = System.Math.Min(1.0m, currentSatiety + 0.5m);
                }
                if (character.TryGetComponent<NPC.Library.Character.Components.CharacterMetrics>(out var metrics))
                {
                    metrics.ApplesEaten++;
                }
            }
        }
        return Task.CompletedTask;
    }
}
