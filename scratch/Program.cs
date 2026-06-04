using System;
using System.Collections.Generic;
using NPC.Library.Character;
using NPC.Library.Inventory;
using NPC.Library.Behaviors;

class Program
{
    static async System.Threading.Tasks.Task Main(string[] args)
    {
        var dispatcher = new NPC.Library.Messaging.MessageDispatcher();
        var factory = new CharacterFactory(dispatcher);
        var c = factory.Create();
        
        // Set Thirst to 0.11m
        c.Drives.SetLevel(NPC.Library.Character.DriveType.Thirst, 0.11m);
        
        var inv = new StandardInventory();
        var bottle = new WaterBottleItem(3);
        inv.AddItem(bottle);
        c.AddComponent<IInventory>(inv);
        
        var drinkActuator = new DrinkActuator();
        
        Console.WriteLine($"Before Drink: Thirst = {c.Drives[NPC.Library.Character.DriveType.Thirst]}");
        
        if (drinkActuator.CanExecute(c))
        {
            await drinkActuator.ExecuteAsync(c);
            Console.WriteLine($"After Drink 1: Thirst = {c.Drives[NPC.Library.Character.DriveType.Thirst]}");
        }
        else
        {
            Console.WriteLine("Cannot execute DrinkActuator!");
        }
    }
}
