using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.IO;
using NPC.Library.Character;
using NPC.Library.Decision;
using NPC.Library.Simulation;
using NPC.Library.State;
using NPC.Library.Spatial;
using NPC.World.Map;
using NPC.Village.Map;

namespace NPC.UI.World.Training
{
    public class WorldGeneticTrainer
    {
        public static async Task RunTrainingAsync(int generations, int ticksPerGeneration, int populationSize, string saveName)
        {
            Console.WriteLine($"Starting World Genetic Training: {generations} Gens, {populationSize} Pop, {ticksPerGeneration} Ticks");
            
            var npcEvolution = new GeneticEvolutionManager(populationSize, mutationRate: 0.15f, mutationAmount: 0.3f, elitismRatio: 0.2f);
            var foxEvolution = new GeneticEvolutionManager(populationSize, mutationRate: 0.15f, mutationAmount: 0.3f, elitismRatio: 0.2f);
            var sheepEvolution = new GeneticEvolutionManager(populationSize, mutationRate: 0.15f, mutationAmount: 0.3f, elitismRatio: 0.2f);
            
            // Generate initial random population
            var popNPC = new List<NeuralNetwork>();
            var popFox = new List<NeuralNetwork>();
            var popSheep = new List<NeuralNetwork>();
            for (int i = 0; i < populationSize; i++)
            {
                // 16 Inputs -> 12 Hidden -> 11 Outputs (Actuators)
                popNPC.Add(new NeuralNetwork(new[] { 16, 12, 11 }));
                popFox.Add(new NeuralNetwork(new[] { 16, 12, 11 }));
                popSheep.Add(new NeuralNetwork(new[] { 16, 12, 11 }));
            }

            for (int gen = 1; gen <= generations; gen++)
            {
                Console.WriteLine($"\n--- Generation {gen} ---");
                
                // We use the same baseline village map to seed the infinite WorldMap chunk generator
                int width = 200 + (populationSize * 10);
                int height = 100 + (populationSize * 5);
                var villageMapGrid = VillageMapGenerator.Generate(width, height, populationSize, out var bedLocations, out var doorLocations, out var chestLocations, out var wellLocation);
                
                var chunkGenerator = new WorldChunkGenerator(villageMapGrid);
                var chunkManager = new ChunkManager(chunkGenerator);
                var worldMap = new WorldMap(chunkManager);
                
                var dispatcher = new NPC.Library.Messaging.MessageDispatcher();
                
                var baseGroup = new NPC.Village.Behaviors.VillageActuatorGroup(worldMap, dispatcher);

                var stateMachine = new StateMachine(new NNActionSelector(worldMap), dispatcher);
                var engine = new SimulationEngine(stateMachine, worldMap, dispatcher);
                // Fast execution settings (match graphical GA)
                engine.TickDuration = TimeSpan.FromMilliseconds(10); 
                engine.TimeScaleMultiplier = 3200.0m;

                var startTime = DateTime.Now;
                worldMap.UpdateSimulationTime(startTime);

                var factory = new CharacterFactory(dispatcher);
                var animalResolver = new CompositeActionResolver();
                animalResolver.AddResolver(new NPC.Library.State.SimpleActuatorGroup(new List<IActuator> {
                    new NPC.Library.Behaviors.SheepGrazeActuator(worldMap),
                    new NPC.Library.Behaviors.SheepFleeActuator(worldMap),
                    new NPC.Library.Behaviors.FoxHuntActuator(worldMap),
                    new NPC.Library.Behaviors.FoxEatActuator(worldMap),
                    new NPC.Library.Behaviors.AnimalSleepActuator(),
                    new NPC.Library.Behaviors.AnimalDrinkActuator(worldMap)
                }));

                var npcs = new List<Character>();
                var foxes = new List<Character>();
                var sheeps = new List<Character>();
                var random = new Random();

                for (int i = 0; i < populationSize; i++)
                {
                    // Spawn NPC
                    var c = factory.Create();
                    c.Name = $"World Clone {i}";
                    
                    var mem = new NPC.Village.Memory.VillageMemory((wellLocation.X, wellLocation.Y, 0), (doorLocations[i].X, doorLocations[i].Y, 0), (chestLocations[i].X, chestLocations[i].Y, 0), (bedLocations[i].X, bedLocations[i].Y, 0));
                    c.AddComponent<NPC.Library.Memory.IMemory>(mem);
                    
                    var inv = new NPC.Library.Inventory.StandardInventory();
                    inv.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
                    inv.AddItem(new NPC.Library.Inventory.Item(NPC.Library.Inventory.ItemType.Apple));
                    c.AddComponent<NPC.Library.Inventory.IInventory>(inv);
                    
                    var charResolver = new CompositeActionResolver();
                    charResolver.AddResolver(baseGroup);
                    c.AddComponent<NPC.Library.State.IActionResolver>(charResolver);
                    c.AddComponent<NeuralNetwork>(popNPC[i]);
                    c.AddComponent(new NPC.Library.Character.Components.CharacterMetrics());
                    
                    if (i == 0 && gen == 1)
                    {
                        c.Drives.SetLevel(NPC.Library.Character.DriveType.Thirst, 0.50m);
                        c.Drives.SetLevel(NPC.Library.Character.DriveType.Satiety, 0.50m);

                        stateMachine.OnActuatorExecuted += (sender, args) => 
                        {
                            if (args.Character.Name == "World Clone 0")
                            {
                                if (args.Actuator is NPC.Library.Behaviors.DrinkActuator || args.Actuator is NPC.Library.Behaviors.EatActuator || args.Actuator is NPC.Library.Behaviors.SearchForFoodActuator || args.Actuator is NPC.Village.Behaviors.VillageGatherWaterActuator)
                                {
                                    args.Character.Drives.TryGetLevel(NPC.Library.Character.DriveType.Thirst, out var t);
                                    args.Character.Drives.TryGetLevel(NPC.Library.Character.DriveType.Satiety, out var s);
                                    Console.WriteLine($"[CLONE 0] Executed {args.Actuator.GetType().Name}. T:{t:F2}, S:{s:F2}");
                                }
                            };
                        };
                    }

                    npcs.Add(c);
                    engine.AddCharacter(c);

                    var loc = bedLocations[i];
                    c.AddComponent(new NPC.Library.Character.Components.BedComponent(loc.X, loc.Y));
                    worldMap.MoveCharacter(c, (loc.X, loc.Y, 0));

                    if (villageMapGrid.Chests.TryGetValue(chestLocations[i], out var chestInv))
                    {
                        chestInv.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
                    }

                    // Spawn Fox
                    var fox = new Animal($"Fox {i}", AnimalType.Fox);
                    fox.AddComponent<NPC.Library.State.IActionResolver>(animalResolver);
                    fox.AddComponent<NeuralNetwork>(popFox[i]);
                    foxes.Add(fox);
                    engine.AddCharacter(fox);
                    worldMap.MoveCharacter(fox, (random.Next(width), random.Next(height), 0));

                    // Spawn Sheep
                    var sheep = new Animal($"Sheep {i}", AnimalType.Sheep);
                    sheep.AddComponent<NPC.Library.State.IActionResolver>(animalResolver);
                    sheep.AddComponent<NeuralNetwork>(popSheep[i]);
                    sheeps.Add(sheep);
                    engine.AddCharacter(sheep);
                    worldMap.MoveCharacter(sheep, (random.Next(width), random.Next(height), 0));
                }

                // Run fast-forward simulation
                for (int t = 0; t < ticksPerGeneration; t++)
                {
                    await engine.TickOnceAsync();
                    worldMap.UpdateSimulationTime(startTime.AddSeconds(t)); // Update physics time
                    
                    // If all NPCs died, end early
                    if (npcs.All(c => c.IsDead)) break;
                }

                // Evaluate Fitness
                var evalNPC = new List<(NeuralNetwork Brain, float Fitness)>();
                var evalFox = new List<(NeuralNetwork Brain, float Fitness)>();
                var evalSheep = new List<(NeuralNetwork Brain, float Fitness)>();
                
                float totalFitnessNPC = 0;
                float totalFitnessFox = 0;
                float totalFitnessSheep = 0;
                
                for (int i = 0; i < populationSize; i++)
                {
                    // NPC Fitness
                    var c = npcs[i];
                    float fNPC = c.IsDead ? c.DeathTick : ticksPerGeneration;
                    var metrics = c.GetComponent<NPC.Library.Character.Components.CharacterMetrics>();
                    
                    if (!c.IsDead)
                    {
                        if (c.Drives.TryGetLevel(NPC.Library.Character.DriveType.Satiety, out var satiety)) fNPC += (float)satiety * 100;
                        if (c.Drives.TryGetLevel(NPC.Library.Character.DriveType.Thirst, out var thirst)) fNPC += (float)thirst * 100;
                    }
                    else
                    {
                        var loc = worldMap.GetCharacterLocation(c);
                        if (loc.HasValue)
                        {
                            float distX = Math.Abs(loc.Value.X - wellLocation.X);
                            float distY = Math.Abs(loc.Value.Y - wellLocation.Y);
                            float bonus = 50f - Math.Min((float)Math.Sqrt(distX*distX + distY*distY), 50f);
                            if (bonus > 0) fNPC += bonus;
                        }
                    }
                    
                    if (metrics != null)
                    {
                        fNPC += metrics.ApplesCollected * 10;
                        fNPC += metrics.ApplesEaten * 20;
                        fNPC += metrics.WaterCollected * 10;
                        fNPC += metrics.SipsTaken * 20;
                    }
                    evalNPC.Add((popNPC[i], fNPC));
                    totalFitnessNPC += fNPC;

                    // Fox Fitness
                    var fox = foxes[i];
                    float fFox = fox.IsDead ? fox.DeathTick : ticksPerGeneration;
                    if (!fox.IsDead)
                    {
                        if (fox.Drives.TryGetLevel(NPC.Library.Character.DriveType.Satiety, out var satiety)) fFox += (float)satiety * 100;
                        if (fox.Drives.TryGetLevel(NPC.Library.Character.DriveType.Thirst, out var thirst)) fFox += (float)thirst * 100;
                    }
                    evalFox.Add((popFox[i], fFox));
                    totalFitnessFox += fFox;

                    // Sheep Fitness
                    var sheep = sheeps[i];
                    float fSheep = sheep.IsDead ? sheep.DeathTick : ticksPerGeneration;
                    if (!sheep.IsDead)
                    {
                        if (sheep.Drives.TryGetLevel(NPC.Library.Character.DriveType.Satiety, out var satiety)) fSheep += (float)satiety * 100;
                        if (sheep.Drives.TryGetLevel(NPC.Library.Character.DriveType.Thirst, out var thirst)) fSheep += (float)thirst * 100;
                    }
                    evalSheep.Add((popSheep[i], fSheep));
                    totalFitnessSheep += fSheep;
                }

                Console.WriteLine($"NPC Avg: {totalFitnessNPC / populationSize:F2} | Fox Avg: {totalFitnessFox / populationSize:F2} | Sheep Avg: {totalFitnessSheep / populationSize:F2}");
                
                // Breed next generation
                popNPC = npcEvolution.Evolve(evalNPC);
                popFox = foxEvolution.Evolve(evalFox);
                popSheep = sheepEvolution.Evolve(evalSheep);
            }
            
            Console.WriteLine("\nTraining Complete!");

            // Save the final population
            if (npcEvolution.BestEverNetwork != null) popNPC[0] = npcEvolution.BestEverNetwork;
            if (foxEvolution.BestEverNetwork != null) popFox[0] = foxEvolution.BestEverNetwork;
            if (sheepEvolution.BestEverNetwork != null) popSheep[0] = sheepEvolution.BestEverNetwork;

            var popPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NPC", "Populations");
            Directory.CreateDirectory(popPath);
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(Path.Combine(popPath, $"{saveName}_npc.json"), JsonSerializer.Serialize(popNPC, options));
            File.WriteAllText(Path.Combine(popPath, $"{saveName}_fox.json"), JsonSerializer.Serialize(popFox, options));
            File.WriteAllText(Path.Combine(popPath, $"{saveName}_sheep.json"), JsonSerializer.Serialize(popSheep, options));
            
            Console.WriteLine($"Saved {saveName}_npc.json, {saveName}_fox.json, {saveName}_sheep.json to {popPath}");
        }
    }
}
