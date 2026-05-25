using System;
using System.Threading.Tasks;
using NPC.Application;
using NPC.Library.Character;
using NPC.Library.State;
using System.Linq;
using DriveType = NPC.Library.Character.DriveType;

namespace TestSleepBug
{
    class Program
    {
        static void Main(string[] args)
        {
            var context = ScenarioRunner.SetupVillageScenario();
            var engine = context.Engine;
            var state = context.InitialState;

            var c = state.SpatialContext.GetCharacters().First();

            // Force fatigue high so they sleep
            c.Drives.SetLevel(DriveType.Fatigue, 0.9m);
            c.Drives.SetLevel(DriveType.Satiety, 1.0m);
            c.Drives.SetLevel(DriveType.Thirst, 1.0m);

            engine.OnTickComplete += (sender, e) =>
            {
                c.Drives.TryGetLevel(DriveType.Fatigue, out var f);
                c.Drives.TryGetLevel(DriveType.Satiety, out var s);
                Console.WriteLine($"Tick {e.TickCount}: Fatigue={f:0.00}, Satiety={s:0.00}, Action={c.LastAction}, TargetDrive={c.TargetDrive}");
                
                if (e.TickCount > 30)
                {
                    engine.StopAsync().Wait();
                }
            };

            Console.WriteLine("Starting tick loop...");
            engine.Start(TimeSpan.FromMilliseconds(100));
            
            while(engine.IsRunning)
            {
                System.Threading.Thread.Sleep(100);
            }
        }
    }
}
