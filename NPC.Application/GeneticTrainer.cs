using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NPC.Library.Character;
using NPC.Library.Decision;
using NPC.Library.Simulation;
using NPC.Library.State;
using NPC.Library.Spatial;
using NPC.Library.Spatial.Grid;
using NPC.Village.Map;

namespace NPC.Application
{
    public class GeneticTrainer
    {
        public static async Task RunTrainingAsync(int generations, int ticksPerGeneration, int populationSize)
        {
            Console.WriteLine($"Starting Genetic Training: {generations} Gens, {populationSize} Pop, {ticksPerGeneration} Ticks");
            
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
                Console.WriteLine($"--- Generation {gen} ---");
                
                // Scale map up based on population
                int width = 200 + (populationSize * 10);
                int height = 100 + (populationSize * 5);
                var map = VillageMapGenerator.Generate(width, height, populationSize, out var bedLocations, out var doorLocations, out var chestLocations, out var wellLocation);
                var spatialContext = new GridSpatialContext(map);
                
                var dispatcher = new NPC.Library.Messaging.MessageDispatcher();
                
                var baseGroup = new NPC.Village.Behaviors.VillageActuatorGroup(spatialContext, dispatcher);

                var stateMachine = new StateMachine(new NNActionSelector(spatialContext), dispatcher);
                var engine = new SimulationEngine(stateMachine, spatialContext, dispatcher);

                var factory = new CharacterFactory(dispatcher);
                var animalResolver = new CompositeActionResolver();
                animalResolver.AddResolver(new NPC.Library.State.SimpleActuatorGroup(new List<IActuator> {
                    new NPC.Library.Behaviors.SheepGrazeActuator(spatialContext),
                    new NPC.Library.Behaviors.SheepFleeActuator(spatialContext),
                    new NPC.Library.Behaviors.FoxHuntActuator(spatialContext),
                    new NPC.Library.Behaviors.FoxEatActuator(spatialContext),
                    new NPC.Library.Behaviors.AnimalSleepActuator(),
                    new NPC.Library.Behaviors.AnimalDrinkActuator(spatialContext)
                }));

                var npcs = new List<Character>();
                var foxes = new List<Character>();
                var sheeps = new List<Character>();
                var random = new Random();

                for (int i = 0; i < populationSize; i++)
                {
                    // Spawn NPC
                    var c = factory.Create();
                    c.Name = $"NPC {i}";
                    
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
                    
                    npcs.Add(c);
                    engine.AddCharacter(c);
                    var loc = bedLocations[i];
                    c.AddComponent(new NPC.Library.Character.Components.BedComponent(loc.X, loc.Y));
                    spatialContext.MoveCharacter(c, (loc.X, loc.Y, 0));

                    if (map.Chests.TryGetValue(chestLocations[i], out var chestInv))
                        chestInv.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));

                    // Spawn Fox
                    var fox = new Animal($"Fox {i}", AnimalType.Fox);
                    fox.AddComponent<NPC.Library.State.IActionResolver>(animalResolver);
                    fox.AddComponent<NeuralNetwork>(popFox[i]);
                    foxes.Add(fox);
                    engine.AddCharacter(fox);
                    spatialContext.MoveCharacter(fox, (random.Next(width), random.Next(height), 0));

                    // Spawn Sheep
                    var sheep = new Animal($"Sheep {i}", AnimalType.Sheep);
                    sheep.AddComponent<NPC.Library.State.IActionResolver>(animalResolver);
                    sheep.AddComponent<NeuralNetwork>(popSheep[i]);
                    sheeps.Add(sheep);
                    engine.AddCharacter(sheep);
                    spatialContext.MoveCharacter(sheep, (random.Next(width), random.Next(height), 0));
                }

                // Run fast-forward simulation
                for (int t = 0; t < ticksPerGeneration; t++)
                {
                    await engine.TickOnceAsync();
                    
                    // If all villagers died, end early
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
                    if (!c.IsDead)
                    {
                        if (c.Drives.TryGetLevel(NPC.Library.Character.DriveType.Satiety, out var satiety)) fNPC += (float)satiety * 100;
                        if (c.Drives.TryGetLevel(NPC.Library.Character.DriveType.Thirst, out var thirst)) fNPC += (float)thirst * 100;
                    }
                    else
                    {
                        var loc = spatialContext.GetCharacterLocation(c);
                        if (loc.HasValue)
                        {
                            float distX = Math.Abs(loc.Value.X - wellLocation.X);
                            float distY = Math.Abs(loc.Value.Y - wellLocation.Y);
                            float bonus = 50f - Math.Min((float)Math.Sqrt(distX*distX + distY*distY), 50f);
                            if (bonus > 0) fNPC += bonus;
                        }
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
            
            // Save the best networks
            System.IO.File.WriteAllText("genes_npc.json", System.Text.Json.JsonSerializer.Serialize(new List<NeuralNetwork> { npcEvolution.BestEverNetwork }));
            System.IO.File.WriteAllText("genes_fox.json", System.Text.Json.JsonSerializer.Serialize(new List<NeuralNetwork> { foxEvolution.BestEverNetwork }));
            System.IO.File.WriteAllText("genes_sheep.json", System.Text.Json.JsonSerializer.Serialize(new List<NeuralNetwork> { sheepEvolution.BestEverNetwork }));
            Console.WriteLine("Saved genes_npc.json, genes_fox.json, genes_sheep.json");
            
            Console.WriteLine("Training Complete!");
        }
    }
}
