using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using NPC.Application;
using NPC.Library.Character;
using NPC.Library.Decision;
using NPC.Library.Simulation;
using NPC.Library.State;
using NPC.World.Map;
using NPC.Village.Map;
using Raylib_cs;

namespace NPC.UI.World.Training
{
    public class UIWorldGeneticTrainer
    {
        public static void RunVisualTraining(int generations, int populationSize)
        {
            var npcEvolution = new GeneticEvolutionManager(populationSize, mutationRate: 0.15f, mutationAmount: 0.3f, elitismRatio: 0.2f);
            var foxEvolution = new GeneticEvolutionManager(populationSize, mutationRate: 0.15f, mutationAmount: 0.3f, elitismRatio: 0.2f);
            var sheepEvolution = new GeneticEvolutionManager(populationSize, mutationRate: 0.15f, mutationAmount: 0.3f, elitismRatio: 0.2f);
            
            var popNPC = new List<NeuralNetwork>();
            var popFox = new List<NeuralNetwork>();
            var popSheep = new List<NeuralNetwork>();
            for (int i = 0; i < populationSize; i++)
            {
                popNPC.Add(new NeuralNetwork(new[] { 16, 12, 11 }));
                popFox.Add(new NeuralNetwork(new[] { 16, 12, 11 }));
                popSheep.Add(new NeuralNetwork(new[] { 16, 12, 11 }));
            }

            using var renderer = new WorldRenderer();
            bool rendererInitialized = false;

            var avgHistory = new List<float>();
            var bestHistory = new List<float>();
            var deathCountHistory = new List<float>();
            var dehydrationHistory = new List<float>();
            var starvationHistory = new List<float>();
            var exhaustionHistory = new List<float>();
            var eatenHistory = new List<float>();
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
            var maxItemsInChestHistory = new List<float>();
            var avgItemsInChestHistory = new List<float>();

            var foxAvgHistory = new List<float>();
            var foxBestHistory = new List<float>();
            var foxDeathCountHistory = new List<float>();
            var foxDehydrationHistory = new List<float>();
            var foxStarvationHistory = new List<float>();

            var sheepAvgHistory = new List<float>();
            var sheepBestHistory = new List<float>();
            var sheepDeathCountHistory = new List<float>();
            var sheepDehydrationHistory = new List<float>();
            var sheepStarvationHistory = new List<float>();
            var sheepEatenHistory = new List<float>();

            for (int gen = 1; gen <= generations; gen++)
            {
                Console.WriteLine($"Starting Generation {gen}...");
                
                double noiseOffsetX = Random.Shared.NextDouble() * 10000;
                double noiseOffsetY = Random.Shared.NextDouble() * 10000;

                int width = 200 + (populationSize * 10);
                int height = 100 + (populationSize * 5);
                var villageMapGrid = VillageMapGenerator.Generate(width, height, populationSize, out var bedLocations, out var doorLocations, out var chestLocations, out var wellLocation, null, noiseOffsetX, noiseOffsetY);
                
                var chunkGenerator = new WorldChunkGenerator(villageMapGrid, noiseOffsetX, noiseOffsetY);
                var chunkManager = new ChunkManager(chunkGenerator);
                var worldMap = new WorldMap(chunkManager);
                
                var dispatcher = new NPC.Library.Messaging.MessageDispatcher();
                
                var baseGroup = new NPC.Village.Behaviors.VillageActuatorGroup(worldMap, dispatcher);

                var stateMachine = new StateMachine(new NNActionSelector(worldMap), dispatcher);
                var engine = new SimulationEngine(stateMachine, worldMap, dispatcher);
                var visionTracker = new NPC.Library.Simulation.VisionTracker(stateMachine, worldMap);

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
                var waterTiles = new List<(int X, int Y, int Z)>();
                var treeTiles = new List<(int X, int Y, int Z)>();
                var random = new Random();

                for (int i = 0; i < populationSize; i++)
                {
                    // Spawn NPC
                    var c = factory.Create();
                    c.Name = $"Clone {i}";
                    
                    var mem = new NPC.Village.Memory.VillageMemory((wellLocation.X, wellLocation.Y, 0), (doorLocations[i].X, doorLocations[i].Y, 0), (chestLocations[i].X, chestLocations[i].Y, 0), (bedLocations[i].X, bedLocations[i].Y, 0));
                    c.AddComponent<NPC.Library.Memory.IMemory>(mem);
                    
                    var inv = new NPC.Library.Inventory.StandardInventory();
                    inv.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
                    inv.AddItem(new NPC.Library.Inventory.Item(NPC.Library.Inventory.ItemType.Apple));
                    c.AddComponent<NPC.Library.Inventory.IInventory>(inv);
                    
                    c.AddComponent<NeuralNetwork>(popNPC[i]);
                    c.AddComponent(new NPC.Library.Character.Components.CharacterMetrics());
                    
                    var charResolver = new CompositeActionResolver();
                    charResolver.AddResolver(baseGroup);
                    c.AddComponent<NPC.Library.State.IActionResolver>(charResolver);
                    
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

                var uiState = new UIState { 
                    SpatialContext = worldMap, 
                    WellLocation = wellLocation,
                    AverageFitnessHistory = avgHistory,
                    BestFitnessHistory = bestHistory,
                    DeathCountHistory = deathCountHistory,
                    DehydrationDeathHistory = dehydrationHistory,
                    StarvationDeathHistory = starvationHistory,
                    ExhaustionDeathHistory = exhaustionHistory,
                    EatenDeathHistory = eatenHistory,
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
                    MaxItemsInChestHistory = maxItemsInChestHistory,
                    AvgItemsInChestHistory = avgItemsInChestHistory,
                    CurrentPopulation = popNPC.ToList(),
                    FoxPopulation = popFox.ToList(),
                    SheepPopulation = popSheep.ToList(),
                    
                    FoxAverageFitnessHistory = foxAvgHistory,
                    FoxBestFitnessHistory = foxBestHistory,
                    FoxDeathCountHistory = foxDeathCountHistory,
                    FoxDehydrationDeathHistory = foxDehydrationHistory,
                    FoxStarvationDeathHistory = foxStarvationHistory,
                    
                    SheepAverageFitnessHistory = sheepAvgHistory,
                    SheepBestFitnessHistory = sheepBestHistory,
                    SheepDeathCountHistory = sheepDeathCountHistory,
                    SheepDehydrationDeathHistory = sheepDehydrationHistory,
                    SheepStarvationDeathHistory = sheepStarvationHistory,
                    SheepEatenDeathHistory = sheepEatenHistory,
                    CurrentWorldTime = startTime,
                    StartWorldTime = startTime,
                    CurrentTimeScale = 1.0m,
                    IsTrainerMode = true,
                    CurrentGeneration = gen
                };
                
                engine.OnTickComplete += (sender, e) => 
                {
                    uiState.TickCount = e.TickCount;
                    var currentSimTime = startTime.AddSeconds(e.TickCount * engine.TickDuration.TotalSeconds * (double)engine.TimeScaleMultiplier);
                    worldMap.UpdateSimulationTime(currentSimTime);
                    uiState.CurrentWorldTime = currentSimTime;
                    uiState.CurrentTimeScale = engine.TimeScaleMultiplier;
                };

                if (!rendererInitialized)
                {
                    renderer.Initialize(dispatcher);
                    rendererInitialized = true;
                }
                else
                {
                    // Use UpdateDispatcher to prevent Raylib from closing the window and corrupting ImGui
                    renderer.UpdateDispatcher(dispatcher);
                }

                // Default to max speed
                engine.TimeScaleMultiplier = 3200.0m;
                engine.TickDuration = TimeSpan.FromMilliseconds(10);
                engine.Start(TimeSpan.FromMilliseconds(10));

                while (!Raylib.WindowShouldClose())
                {
                    // Speed controls
                    if (Raylib.IsKeyPressed(KeyboardKey.One)) { engine.TimeScaleMultiplier = 1.0m; engine.TickDuration = TimeSpan.FromMilliseconds(100); engine.StopAsync().Wait(); engine.Start(TimeSpan.FromMilliseconds(100)); }
                    if (Raylib.IsKeyPressed(KeyboardKey.Two)) { engine.TimeScaleMultiplier = 1.0m; engine.TickDuration = TimeSpan.FromMilliseconds(10); engine.StopAsync().Wait(); engine.Start(TimeSpan.FromMilliseconds(10)); }
                    if (Raylib.IsKeyPressed(KeyboardKey.Three)) { engine.TimeScaleMultiplier = 10.0m; engine.TickDuration = TimeSpan.FromMilliseconds(10); engine.StopAsync().Wait(); engine.Start(TimeSpan.FromMilliseconds(10)); } 
                    if (Raylib.IsKeyPressed(KeyboardKey.Four)) { engine.TimeScaleMultiplier = 60.0m; engine.TickDuration = TimeSpan.FromMilliseconds(10); engine.StopAsync().Wait(); engine.Start(TimeSpan.FromMilliseconds(10)); } 
                    if (Raylib.IsKeyPressed(KeyboardKey.Five)) { engine.TimeScaleMultiplier = 3200.0m; engine.TickDuration = TimeSpan.FromMilliseconds(10); engine.StopAsync().Wait(); engine.Start(TimeSpan.FromMilliseconds(10)); } 
                    if (Raylib.IsKeyPressed(KeyboardKey.Six)) { engine.TimeScaleMultiplier = 3200.0m; engine.TickDuration = TimeSpan.FromMilliseconds(10); engine.StopAsync().Wait(); engine.Start(TimeSpan.Zero); } 

                    renderer.Render(uiState);

                    if (uiState.CurrentWorldTime >= startTime.AddHours(50) || npcs.All(c => c.IsDead))
                    {
                        break;
                    }
                }

                engine.StopAsync().Wait();
                engine.Dispose();

                if (Raylib.WindowShouldClose()) break; // User closed window

                // Evaluate Fitness
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
                    float fNPC = c.IsDead ? c.DeathTick : uiState.TickCount;
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
                    float fFox = fox.IsDead ? fox.DeathTick : uiState.TickCount;
                    if (!fox.IsDead)
                    {
                        if (fox.Drives.TryGetLevel(NPC.Library.Character.DriveType.Satiety, out var satiety)) fFox += (float)satiety * 100;
                        if (fox.Drives.TryGetLevel(NPC.Library.Character.DriveType.Thirst, out var thirst)) fFox += (float)thirst * 100;
                    }
                    evalFox.Add((popFox[i], fFox));
                    totalFitnessFox += fFox;

                    // Sheep Fitness
                    var sheep = sheeps[i];
                    float fSheep = sheep.IsDead ? sheep.DeathTick : uiState.TickCount;
                    if (!sheep.IsDead)
                    {
                        if (sheep.Drives.TryGetLevel(NPC.Library.Character.DriveType.Satiety, out var satiety)) fSheep += (float)satiety * 100;
                        if (sheep.Drives.TryGetLevel(NPC.Library.Character.DriveType.Thirst, out var thirst)) fSheep += (float)thirst * 100;
                    }
                    evalSheep.Add((popSheep[i], fSheep));
                    totalFitnessSheep += fSheep;
                }

                float avgFitness = totalFitnessNPC / populationSize;
                float genBest = evalNPC.Max(x => x.Fitness);
                avgHistory.Add(avgFitness);
                bestHistory.Add(genBest);

                float avgSatiety = 0;
                float avgThirst = 0;
                foreach (var c in npcs)
                {
                    if (c.Drives.TryGetLevel(NPC.Library.Character.DriveType.Satiety, out var s)) avgSatiety += (float)s;
                    if (c.Drives.TryGetLevel(NPC.Library.Character.DriveType.Thirst, out var t)) avgThirst += (float)t;
                }
                avgSatiety /= populationSize;
                avgThirst /= populationSize;

                Console.WriteLine($"Generation {gen} Complete! Avg Fitness: {avgFitness:F2} | Best: {genBest:F2} | Avg Satiety: {avgSatiety:P0} | Avg Thirst: {avgThirst:P0}");

                int deathCount = npcs.Count(c => c.IsDead);
                deathCountHistory.Add(deathCount);

                int dehydrationCount = npcs.Count(c => c.IsDead && c.DeathReason == "Dehydration");
                int starvationCount = npcs.Count(c => c.IsDead && c.DeathReason == "Starvation");
                int exhaustionCount = npcs.Count(c => c.IsDead && c.DeathReason == "Exhaustion");
                int eatenCount = npcs.Count(c => c.IsDead && c.DeathReason == "Eaten");
                int survivedCount = npcs.Count(c => !c.IsDead);
                dehydrationHistory.Add(dehydrationCount);
                starvationHistory.Add(starvationCount);
                exhaustionHistory.Add(exhaustionCount);
                uiState.EatenDeathHistory.Add(eatenCount);
                survivedHistory.Add(survivedCount);

                foxAvgHistory.Add(totalFitnessFox / populationSize);
                foxBestHistory.Add(evalFox.Max(x => x.Fitness));
                foxDeathCountHistory.Add(foxes.Count(c => c.IsDead));
                foxDehydrationHistory.Add(foxes.Count(c => c.IsDead && c.DeathReason == "Dehydration"));
                foxStarvationHistory.Add(foxes.Count(c => c.IsDead && c.DeathReason == "Starvation"));

                sheepAvgHistory.Add(totalFitnessSheep / populationSize);
                sheepBestHistory.Add(evalSheep.Max(x => x.Fitness));
                sheepDeathCountHistory.Add(sheeps.Count(c => c.IsDead));
                uiState.SheepDehydrationDeathHistory.Add(sheeps.Count(c => c.IsDead && c.DeathReason == "Dehydration"));
                uiState.SheepStarvationDeathHistory.Add(sheeps.Count(c => c.IsDead && c.DeathReason == "Starvation"));
                uiState.SheepEatenDeathHistory.Add(sheeps.Count(c => c.IsDead && c.DeathReason == "Eaten"));

                var deadChars = npcs.Where(c => c.IsDead).ToList();
                float earliestDeath = deadChars.Count > 0 ? deadChars.Min(c => c.DeathTick) : uiState.TickCount;
                float latestDeath = deadChars.Count > 0 ? deadChars.Max(c => c.DeathTick) : uiState.TickCount;
                
                earliestDeathHistory.Add(earliestDeath);
                latestDeathHistory.Add(latestDeath);

                var genMetrics = npcs.Select(c => c.GetComponent<NPC.Library.Character.Components.CharacterMetrics>()).Where(m => m != null).ToList();
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

                var genChestItems = new List<int>();
                for (int i = 0; i < populationSize; i++)
                {
                    if (villageMapGrid.Chests.TryGetValue(chestLocations[i], out var chestInv))
                    {
                        genChestItems.Add(chestInv.GetItems().Count());
                    }
                }
                if (genChestItems.Count > 0)
                {
                    maxItemsInChestHistory.Add(genChestItems.Max());
                    avgItemsInChestHistory.Add((float)genChestItems.Average());
                }
                else
                {
                    maxItemsInChestHistory.Add(0);
                    avgItemsInChestHistory.Add(0);
                }

                Console.WriteLine($"Generation {gen} finished! Avg: {avgFitness:F2} | Gen Best: {genBest:F2} | Deaths: {deathCount}");

                popNPC = npcEvolution.Evolve(evalNPC);
                popFox = foxEvolution.Evolve(evalFox);
                popSheep = sheepEvolution.Evolve(evalSheep);
            }
        }
    }
}
