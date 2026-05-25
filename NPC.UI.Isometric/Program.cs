using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using NPC.Application;
using NPC.Library.Character;
using NPC.Library.Decision;
using NPC.Library.Simulation;
using NPC.Library.State;
using NPC.Library.Spatial.Grid;
using NPC.Village.Map;
using Raylib_cs;

namespace NPC.UI.Isometric
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Select Mode:");
            Console.WriteLine("1. Standard Village Scenario");
            Console.WriteLine("2. Visual Genetic Training");
            Console.Write("Choice [1]: ");
            var choice = Console.ReadLine();
            
            if (choice == "2")
            {
                RunVisualTraining();
            }
            else
            {
                RunStandardScenario();
            }
        }

        static void RunStandardScenario()
        {
            Console.Write("Enter number of characters [6]: ");
            var charsStr = Console.ReadLine();
            int npcCount = 6;
            if (int.TryParse(charsStr, out int count)) npcCount = count;

            var popPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NPC", "Populations");
            var populations = new List<string>();
            if (System.IO.Directory.Exists(popPath))
            {
                populations = System.IO.Directory.GetFiles(popPath, "*.json").Select(System.IO.Path.GetFileNameWithoutExtension).ToList();
            }

            Console.WriteLine("\nAvailable Genetic Codes (Populations):");
            Console.WriteLine("0. Default (Randomly Wired)");
            for (int i = 0; i < populations.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {populations[i]}");
            }
            Console.Write("Select Genetic Code [0]: ");
            var codeStr = Console.ReadLine();
            List<NeuralNetwork>? loadedGenetics = null;
            if (int.TryParse(codeStr, out int codeIndex) && codeIndex > 0 && codeIndex <= populations.Count)
            {
                try
                {
                    var file = System.IO.Path.Combine(popPath, populations[codeIndex - 1] + ".json");
                    var json = System.IO.File.ReadAllText(file);
                    loadedGenetics = System.Text.Json.JsonSerializer.Deserialize<List<NeuralNetwork>>(json);
                    Console.WriteLine($"\nLoaded population {populations[codeIndex - 1]}!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nFailed to load population: {ex.Message}");
                }
            }

            Console.WriteLine("\nGenerating Village Map...");
            var context = ScenarioRunner.SetupVillageScenario(npcCount, loadedGenetics);
            
            using var renderer = new IsometricRenderer();
            renderer.Initialize(context.Dispatcher);

            context.Engine.OnTickComplete += (sender, e) => 
            {
                context.InitialState.TickCount = e.TickCount;
            };

            context.Engine.Start(TimeSpan.FromMilliseconds(200));

            while (!Raylib.WindowShouldClose())
            {
                renderer.Render(context.InitialState);
            }
            
            context.Engine.StopAsync().Wait();
            context.Engine.Dispose();
        }

        static void RunVisualTraining()
        {
            int generations = int.MaxValue;
            int populationSize = 16;
            int maxTicks = 1000;
            
            var evolutionManager = new GeneticEvolutionManager(populationSize, mutationRate: 0.15f, mutationAmount: 0.3f, elitismRatio: 0.2f);
            
            var currentPopulation = new List<NeuralNetwork>();
            for (int i = 0; i < populationSize; i++) currentPopulation.Add(new NeuralNetwork(new[] { 16, 12, 11 }));

            using var renderer = new IsometricRenderer();
            bool rendererInitialized = false;

            var avgHistory = new List<float>();
            var bestHistory = new List<float>();
            var deathCountHistory = new List<float>();
            var dehydrationHistory = new List<float>();
            var starvationHistory = new List<float>();
            var exhaustionHistory = new List<float>();
            var survivedHistory = new List<float>();
            var earliestDeathHistory = new List<float>();
            var latestDeathHistory = new List<float>();
            
            var maxApplesColHistory = new List<float>();
            var avgApplesColHistory = new List<float>();
            var maxApplesEatenHistory = new List<float>();
            var avgApplesEatenHistory = new List<float>();
            var maxWaterColHistory = new List<float>();
            var avgWaterColHistory = new List<float>();
            var maxSipsHistory = new List<float>();
            var avgSipsHistory = new List<float>();

            for (int gen = 1; gen <= generations; gen++)
            {
                Console.WriteLine($"Starting Generation {gen}...");
                
                int width = 200 + (populationSize * 10);
                int height = 100 + (populationSize * 5);
                var map = VillageMapGenerator.Generate(width, height, populationSize, out var bedLocations, out var doorLocations, out var chestLocations, out var wellLocation);
                var spatialContext = new GridSpatialContext(map);
                
                var dispatcher = new NPC.Library.Messaging.MessageDispatcher();
                
                var resolver = new CompositeActionResolver();
                resolver.AddResolver(new NPC.Village.Behaviors.VillageActuatorGroup(spatialContext, dispatcher));

                var stateMachine = new StateMachine(resolver, new NNActionSelector(spatialContext), dispatcher);
                var engine = new SimulationEngine(stateMachine, spatialContext, dispatcher)
                {
                    SatietyDecayPerTick = 0.005m
                };
                
                var visionTracker = new NPC.Library.Simulation.VisionTracker(stateMachine, spatialContext);

                var factory = new CharacterFactory(dispatcher);
                var characters = new List<Character>();

                for (int i = 0; i < populationSize; i++)
                {
                    var c = factory.Create();
                    c.Name = $"Clone {i}";
                    
                    var mem = new NPC.Village.Memory.VillageMemory(wellLocation, doorLocations[i], chestLocations[i], bedLocations[i]);
                    c.AddComponent<NPC.Library.Memory.IMemory>(mem);
                    
                    var inv = new NPC.Library.Inventory.StandardInventory();
                    inv.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
                    inv.AddItem(new NPC.Library.Inventory.Item(NPC.Library.Inventory.ItemType.Apple));
                    c.AddComponent<NPC.Library.Inventory.IInventory>(inv);
                    
                    c.AddComponent<NeuralNetwork>(currentPopulation[i]);
                    c.AddComponent(new NPC.Library.Character.Components.CharacterMetrics());
                    
                    characters.Add(c);
                    engine.AddCharacter(c);

                    var loc = bedLocations[i];
                    c.AddComponent(new NPC.Library.Character.Components.BedComponent(loc.X, loc.Y));
                    spatialContext.MoveCharacter(c, loc);

                    if (map.Chests.TryGetValue(chestLocations[i], out var chestInv))
                    {
                        chestInv.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
                    }
                }

                var uiState = new UIState { 
                    SpatialContext = spatialContext, 
                    WellLocation = wellLocation,
                    AverageFitnessHistory = avgHistory,
                    BestFitnessHistory = bestHistory,
                    DeathCountHistory = deathCountHistory,
                    DehydrationDeathHistory = dehydrationHistory,
                    StarvationDeathHistory = starvationHistory,
                    ExhaustionDeathHistory = exhaustionHistory,
                    SurvivedCountHistory = survivedHistory,
                    EarliestDeathHistory = earliestDeathHistory,
                    LatestDeathHistory = latestDeathHistory,
                    MaxApplesCollectedHistory = maxApplesColHistory,
                    AvgApplesCollectedHistory = avgApplesColHistory,
                    MaxApplesEatenHistory = maxApplesEatenHistory,
                    AvgApplesEatenHistory = avgApplesEatenHistory,
                    MaxWaterCollectedHistory = maxWaterColHistory,
                    AvgWaterCollectedHistory = avgWaterColHistory,
                    MaxSipsTakenHistory = maxSipsHistory,
                    AvgSipsTakenHistory = avgSipsHistory,
                    CurrentPopulation = currentPopulation.ToList()
                };
                engine.OnTickComplete += (sender, e) => 
                {
                    uiState.TickCount = e.TickCount;
                };

                if (!rendererInitialized)
                {
                    renderer.Initialize(dispatcher);
                    rendererInitialized = true;
                }
                else
                {
                    renderer.UpdateDispatcher(dispatcher);
                }

                // Run fast visually (10ms per tick = 100 ticks/sec)
                engine.Start(TimeSpan.FromMilliseconds(10));

                while (!Raylib.WindowShouldClose())
                {
                    renderer.Render(uiState);
                    
                    if (uiState.SlowModeChanged)
                    {
                        uiState.SlowModeChanged = false;
                        engine.StopAsync().Wait();
                        engine.Start(uiState.SlowMode ? TimeSpan.FromMilliseconds(200) : TimeSpan.FromMilliseconds(10));
                    }

                    if (uiState.TickCount >= maxTicks || characters.All(c => c.IsDead))
                    {
                        break;
                    }
                }

                engine.StopAsync().Wait();
                engine.Dispose();

                if (Raylib.WindowShouldClose()) break; // User closed window

                // Evaluate Fitness
                var evaluatedPop = new List<(NeuralNetwork Brain, float Fitness)>();
                float totalFitness = 0;
                
                for (int i = 0; i < populationSize; i++)
                {
                    var c = characters[i];
                    float fitness = c.IsDead ? c.DeathTick : maxTicks;
                    
                    if (!c.IsDead)
                    {
                        if (c.Drives.TryGetLevel(NPC.Library.Character.DriveType.Satiety, out var satiety)) fitness += (float)satiety * 100;
                        if (c.Drives.TryGetLevel(NPC.Library.Character.DriveType.Thirst, out var thirst)) fitness += (float)thirst * 100;
                    }
                    else
                    {
                        // Proximity bonus to well to encourage movement and break the 101 flatline
                        var loc = spatialContext.GetCharacterLocation(c);
                        if (loc.HasValue)
                        {
                            float distX = Math.Abs(loc.Value.X - wellLocation.X);
                            float distY = Math.Abs(loc.Value.Y - wellLocation.Y);
                            float dist = (float)Math.Sqrt(distX * distX + distY * distY);
                            
                            // Give up to 50 points for getting closer to the well
                            float bonus = 50f - Math.Min(dist, 50f);
                            if (bonus > 0) fitness += bonus;
                        }

                        // Proximity bonus to Apple Trees to encourage finding food
                        if (loc.HasValue)
                        {
                            float closestTreeDist = float.MaxValue;
                            foreach (var treeLoc in spatialContext.Map.TreeApples.Keys)
                            {
                                if (spatialContext.Map.TreeApples[treeLoc] > 0)
                                {
                                    float dx = Math.Abs(loc.Value.X - treeLoc.X);
                                    float dy = Math.Abs(loc.Value.Y - treeLoc.Y);
                                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                                    if (d < closestTreeDist) closestTreeDist = d;
                                }
                            }

                            if (closestTreeDist < 50f)
                            {
                                float appleBonus = 50f - closestTreeDist;
                                if (appleBonus > 0) fitness += appleBonus;
                            }
                        }
                    }
                    
                    evaluatedPop.Add((currentPopulation[i], fitness));
                    totalFitness += fitness;
                }

                float avgFitness = totalFitness / populationSize;
                float genBest = evaluatedPop.Max(x => x.Fitness);
                avgHistory.Add(avgFitness);
                bestHistory.Add(genBest);

                int deathCount = characters.Count(c => c.IsDead);
                deathCountHistory.Add(deathCount);

                int dehydrationCount = characters.Count(c => c.IsDead && c.DeathReason == "Dehydration");
                int starvationCount = characters.Count(c => c.IsDead && c.DeathReason == "Starvation");
                int exhaustionCount = characters.Count(c => c.IsDead && c.DeathReason == "Exhaustion");
                int survivedCount = characters.Count(c => !c.IsDead);
                dehydrationHistory.Add(dehydrationCount);
                starvationHistory.Add(starvationCount);
                exhaustionHistory.Add(exhaustionCount);
                survivedHistory.Add(survivedCount);

                var deadChars = characters.Where(c => c.IsDead).ToList();
                float earliestDeath = deadChars.Count > 0 ? deadChars.Min(c => c.DeathTick) : maxTicks;
                float latestDeath = deadChars.Count > 0 ? deadChars.Max(c => c.DeathTick) : maxTicks;
                
                earliestDeathHistory.Add(earliestDeath);
                latestDeathHistory.Add(latestDeath);

                var genMetrics = characters.Select(c => c.GetComponent<NPC.Library.Character.Components.CharacterMetrics>()).Where(m => m != null).ToList();
                if (genMetrics.Count > 0)
                {
                    maxApplesColHistory.Add(genMetrics.Max(m => m!.ApplesCollected));
                    avgApplesColHistory.Add((float)genMetrics.Average(m => m!.ApplesCollected));
                    maxApplesEatenHistory.Add(genMetrics.Max(m => m!.ApplesEaten));
                    avgApplesEatenHistory.Add((float)genMetrics.Average(m => m!.ApplesEaten));
                    maxWaterColHistory.Add(genMetrics.Max(m => m!.WaterCollected));
                    avgWaterColHistory.Add((float)genMetrics.Average(m => m!.WaterCollected));
                    maxSipsHistory.Add(genMetrics.Max(m => m!.SipsTaken));
                    avgSipsHistory.Add((float)genMetrics.Average(m => m!.SipsTaken));
                }
                else
                {
                    maxApplesColHistory.Add(0);
                    avgApplesColHistory.Add(0);
                    maxApplesEatenHistory.Add(0);
                    avgApplesEatenHistory.Add(0);
                    maxWaterColHistory.Add(0);
                    avgWaterColHistory.Add(0);
                    maxSipsHistory.Add(0);
                    avgSipsHistory.Add(0);
                }

                Console.WriteLine($"Generation {gen} finished! Avg: {avgFitness:F2} | Gen Best: {genBest:F2} | Deaths: {deathCount}");
                
                currentPopulation = evolutionManager.Evolve(evaluatedPop);
            }
        }
    }
}
