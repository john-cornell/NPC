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
using Raylib_cs;

namespace NPC.UI.World
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("NPC.World UI & Trainer Startup");
            Console.WriteLine("1. Run World Simulation (Graphical UI)");
            Console.WriteLine("2. Run Genetic Trainer (Headless Training)");
            Console.WriteLine("3. Run Genetic Trainer (Graphical UI)");
            Console.Write("Select Mode [1]: ");
            var modeStr = Console.ReadLine();
            bool runTrainerHeadless = modeStr?.Trim() == "2";
            bool runTrainerVisual = modeStr?.Trim() == "3";

            if (runTrainerHeadless || runTrainerVisual)
            {
                int gens = int.MaxValue;
                if (runTrainerHeadless)
                {
                    Console.Write("\nEnter number of generations [50]: ");
                    var gensStr = Console.ReadLine();
                    gens = 50;
                    if (int.TryParse(gensStr, out int g)) gens = g;
                }

                Console.Write("Enter population size [16]: ");
                var popStr = Console.ReadLine();
                int pop = 16;
                if (int.TryParse(popStr, out int p)) pop = p;

                string saveName = "";
                if (runTrainerHeadless)
                {
                    Console.Write("Enter name for saved population [WorldGenetics]: ");
                    var nameStr = Console.ReadLine();
                    saveName = string.IsNullOrWhiteSpace(nameStr) ? "WorldGenetics" : nameStr.Trim();

                    await NPC.UI.World.Training.WorldGeneticTrainer.RunTrainingAsync(gens, 1000, pop, saveName);
                    Console.WriteLine("\nPress any key to exit...");
                    Console.ReadKey();
                }
                else
                {
                    NPC.UI.World.Training.UIWorldGeneticTrainer.RunVisualTraining(gens, pop);
                }
                
                return;
            }

            Console.WriteLine("\n--- Starting Simulation ---");
            Console.Write("Enter number of characters [6]: ");
            var charsStr = Console.ReadLine();
            int npcCount = 6;
            if (int.TryParse(charsStr, out int count)) npcCount = count;

            var popPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NPC", "Populations");
            var populations = new List<string>();
            if (System.IO.Directory.Exists(popPath))
            {
                var files = System.IO.Directory.GetFiles(popPath, "*.json").Select(System.IO.Path.GetFileNameWithoutExtension).ToList();
                foreach (var f in files)
                {
                    if (f.EndsWith("_npc"))
                    {
                        var baseName = f.Substring(0, f.Length - 4);
                        if (!populations.Contains(baseName)) populations.Add(baseName);
                    }
                    else if (!f.EndsWith("_fox") && !f.EndsWith("_sheep"))
                    {
                        if (!populations.Contains(f)) populations.Add(f); // Legacy files
                    }
                }
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
            List<NeuralNetwork>? foxGenetics = null;
            List<NeuralNetwork>? sheepGenetics = null;

            if (int.TryParse(codeStr, out int codeIndex) && codeIndex > 0 && codeIndex <= populations.Count)
            {
                var baseName = populations[codeIndex - 1];
                try
                {
                    // Try to load multi-species
                    var npcFile = System.IO.Path.Combine(popPath, baseName + "_npc.json");
                    var foxFile = System.IO.Path.Combine(popPath, baseName + "_fox.json");
                    var sheepFile = System.IO.Path.Combine(popPath, baseName + "_sheep.json");

                    if (System.IO.File.Exists(npcFile))
                    {
                        loadedGenetics = System.Text.Json.JsonSerializer.Deserialize<List<NeuralNetwork>>(System.IO.File.ReadAllText(npcFile));
                        if (System.IO.File.Exists(foxFile)) foxGenetics = System.Text.Json.JsonSerializer.Deserialize<List<NeuralNetwork>>(System.IO.File.ReadAllText(foxFile));
                        if (System.IO.File.Exists(sheepFile)) sheepGenetics = System.Text.Json.JsonSerializer.Deserialize<List<NeuralNetwork>>(System.IO.File.ReadAllText(sheepFile));
                        Console.WriteLine($"\nLoaded multi-species population {baseName}!");
                    }
                    else
                    {
                        // Legacy single-species
                        var file = System.IO.Path.Combine(popPath, baseName + ".json");
                        loadedGenetics = System.Text.Json.JsonSerializer.Deserialize<List<NeuralNetwork>>(System.IO.File.ReadAllText(file));
                        Console.WriteLine($"\nLoaded legacy population {baseName}!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nFailed to load population: {ex.Message}");
                }
            }

            Console.WriteLine("\nGenerating World Map Environment...");
            
            var dispatcher = new NPC.Library.Messaging.MessageDispatcher();
            
            var villageMapGrid = NPC.Village.Map.VillageMapGenerator.Generate(200, 100, npcCount + 1, out var bedLocations, out var doorLocations, out var chestLocations, out var wellLocation);
            var chunkGenerator = new WorldChunkGenerator(villageMapGrid);
            var chunkManager = new ChunkManager(chunkGenerator);
            var worldMap = new WorldMap(chunkManager);
            
            var stateMachine = new StateMachine(new NNActionSelector(worldMap), dispatcher);
            var visionTracker = new NPC.Library.Simulation.VisionTracker(stateMachine, worldMap);
            
            var engine = new SimulationEngine(stateMachine, worldMap, dispatcher)
            {
                TimeScaleMultiplier = 1.0m,
                TickDuration = TimeSpan.FromSeconds(1)
            };
            
            var startTime = DateTime.Now;
            worldMap.UpdateSimulationTime(startTime);

            var factory = new CharacterFactory(dispatcher);

            // --- PLAYER CHARACTER ---
            var playerController = new NPC.Library.Behaviors.Player.PlayerController(worldMap);
            var playerChar = factory.Create();
            playerChar.Name = "Player";
            
            var pSpawnLoc = bedLocations[0];
            var pMem = new NPC.Village.Memory.VillageMemory(
                (wellLocation.X, wellLocation.Y, 0), 
                doorLocations.Count > 0 ? (doorLocations[0].X, doorLocations[0].Y, 0) : (pSpawnLoc.X, pSpawnLoc.Y, 0), 
                chestLocations.Count > 0 ? (chestLocations[0].X, chestLocations[0].Y, 0) : (pSpawnLoc.X, pSpawnLoc.Y, 0), 
                (pSpawnLoc.X, pSpawnLoc.Y, 0));
            playerChar.AddComponent<NPC.Library.Memory.IMemory>(pMem);
            
            var pInv = new NPC.Library.Inventory.StandardInventory();
            pInv.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
            pInv.AddItem(new NPC.Library.Inventory.DaggerItem());
            pInv.AddItem(new NPC.Library.Inventory.LightHealingPotionItem());
            playerChar.AddComponent<NPC.Library.Inventory.IInventory>(pInv);
            
            playerChar.AddComponent(new NPC.Library.Character.Components.StatsComponent(str: 14, dex: 12, con: 12, maxHp: 25));
            
            playerChar.AddComponent(new NPC.Library.Character.Components.CharacterMetrics());
            playerChar.AddComponent<NPC.Library.Behaviors.Player.IPlayerController>(playerController);
            
            engine.AddCharacter(playerChar);
            playerChar.AddComponent(new NPC.Library.Character.Components.BedComponent(pSpawnLoc.X, pSpawnLoc.Y));
            worldMap.MoveCharacter(playerChar, (pSpawnLoc.X, pSpawnLoc.Y, 0));
            // ------------------------
            for (int i = 0; i < npcCount; i++)
            {
                var c = factory.Create();
                c.Name = $"World NPC {i+1}";
                
                if (loadedGenetics != null && i < loadedGenetics.Count)
                {
                    c.AddComponent<NeuralNetwork>(loadedGenetics[i]);
                }
                else
                {
                    // Default randomly wired brain (16 Inputs -> 12 Hidden -> 11 Outputs)
                    c.AddComponent<NeuralNetwork>(new NeuralNetwork(new[] { 16, 12, 11 }));
                }
                
                (int X, int Y) spawnLoc = bedLocations.Count > i + 1 ? bedLocations[i + 1] : (Random.Shared.Next(40, 60), Random.Shared.Next(40, 60));
                var mem = new NPC.Village.Memory.VillageMemory(
                    (wellLocation.X, wellLocation.Y, 0), 
                    doorLocations.Count > i + 1 ? (doorLocations[i + 1].X, doorLocations[i + 1].Y, 0) : (spawnLoc.X, spawnLoc.Y, 0), 
                    chestLocations.Count > i + 1 ? (chestLocations[i + 1].X, chestLocations[i + 1].Y, 0) : (spawnLoc.X, spawnLoc.Y, 0), 
                    bedLocations.Count > i + 1 ? (bedLocations[i + 1].X, bedLocations[i + 1].Y, 0) : (spawnLoc.X, spawnLoc.Y, 0));
                c.AddComponent<NPC.Library.Memory.IMemory>(mem);
                
                var inv = new NPC.Library.Inventory.StandardInventory();
                inv.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
                inv.AddItem(new NPC.Library.Inventory.Item(NPC.Library.Inventory.ItemType.Apple));
                c.AddComponent<NPC.Library.Inventory.IInventory>(inv);
                
                c.AddComponent(new NPC.Library.Character.Components.CharacterMetrics());

                var charResolver = new CompositeActionResolver();
                charResolver.AddResolver(new NPC.Village.Behaviors.VillageActuatorGroup(worldMap, dispatcher));
                c.AddComponent<NPC.Library.State.IActionResolver>(charResolver);
                
                engine.AddCharacter(c);
                c.AddComponent(new NPC.Library.Character.Components.BedComponent(spawnLoc.X, spawnLoc.Y));
                worldMap.MoveCharacter(c, (spawnLoc.X, spawnLoc.Y, 0));
            }

            Console.WriteLine("Spawning wildlife...");
            var animalResolver = new CompositeActionResolver();
            animalResolver.AddResolver(new NPC.Library.State.SimpleActuatorGroup(new List<IActuator> {
                new NPC.Library.Behaviors.SheepGrazeActuator(worldMap),
                new NPC.Library.Behaviors.SheepFleeActuator(worldMap),
                new NPC.Library.Behaviors.FoxHuntActuator(worldMap),
                new NPC.Library.Behaviors.FoxEatActuator(worldMap),
                new NPC.Library.Behaviors.AnimalSleepActuator(),
                new NPC.Library.Behaviors.AnimalDrinkActuator(worldMap)
            }));

            var animalSelector = new AnimalActionSelector();

            for (int i = 0; i < 15; i++)
            {
                var sheep = new Animal($"Sheep {i+1}", AnimalType.Sheep);
                sheep.AddComponent<IActionResolver>(animalResolver);
                
                if (sheepGenetics != null && i < sheepGenetics.Count)
                {
                    sheep.AddComponent<NeuralNetwork>(sheepGenetics[i]);
                }
                else
                {
                    sheep.AddComponent<NeuralNetwork>(new NeuralNetwork(new[] { 16, 12, 11 }));
                }

                engine.AddCharacter(sheep);
                worldMap.MoveCharacter(sheep, (250 + Random.Shared.Next(0, 50), 50 + Random.Shared.Next(0, 50), 0));
            }

            for (int i = 0; i < 5; i++)
            {
                var fox = new Animal($"Fox {i+1}", AnimalType.Fox);
                fox.AddComponent<IActionResolver>(animalResolver);
                
                if (foxGenetics != null && i < foxGenetics.Count)
                {
                    fox.AddComponent<NeuralNetwork>(foxGenetics[i]);
                }
                else
                {
                    fox.AddComponent<NeuralNetwork>(new NeuralNetwork(new[] { 16, 12, 11 }));
                }

                engine.AddCharacter(fox);
                worldMap.MoveCharacter(fox, (250 + Random.Shared.Next(0, 50), 50 + Random.Shared.Next(0, 50), 0));
            }

            Console.WriteLine("Spawning Goblins in the dungeon...");
            for (int i = 0; i < 3; i++)
            {
                var goblin = factory.Create();
                goblin.Name = $"Goblin {i+1}";
                goblin.AddComponent(new NPC.Library.Character.Components.StatsComponent(str: 8, dex: 14, con: 8, maxHp: 10));
                
                // Pick a random home location for this goblin's "favoured room"
                int gx, gy;
                while (true)
                {
                    gx = 110 + Random.Shared.Next(-20, 21);
                    gy = 50 + Random.Shared.Next(-20, 21);
                    var tile = worldMap.GetTile((gx, gy, -1));
                    if (tile == NPC.Library.Spatial.Grid.TileType.CaveFloor)
                    {
                        break;
                    }
                }

                var aiController = new NPC.Library.Behaviors.AI.GoblinAIController(worldMap)
                {
                    HomeLocation = (gx, gy, -1)
                };
                goblin.AddComponent<NPC.Library.Behaviors.AI.IAIController>(aiController);
                engine.AddCharacter(goblin);
                
                worldMap.MoveCharacter(goblin, (gx, gy, -1));
            }

            var uiState = new UIState { 
                SpatialContext = worldMap, 
                WellLocation = wellLocation,
                CurrentPopulation = loadedGenetics ?? new List<NeuralNetwork>(),
                AverageFitnessHistory = new List<float>(),
                BestFitnessHistory = new List<float>(),
                DeathCountHistory = new List<float>(),
                DehydrationDeathHistory = new List<float>(),
                StarvationDeathHistory = new List<float>(),
                ExhaustionDeathHistory = new List<float>(),
                SurvivedCountHistory = new List<float>(),
                EarliestDeathHistory = new List<float>(),
                LatestDeathHistory = new List<float>(),
                MaxApplesCollectedHistory = new List<float>(),
                AvgApplesCollectedHistory = new List<float>(),
                MaxApplesEatenHistory = new List<float>(),
                AvgApplesEatenHistory = new List<float>(),
                MaxWaterCollectedHistory = new List<float>(),
                AvgWaterCollectedHistory = new List<float>(),
                MaxSipsTakenHistory = new List<float>(),
                AvgSipsTakenHistory = new List<float>(),
                CurrentWorldTime = startTime,
                StartWorldTime = startTime,
                CurrentTimeScale = 1.0m,
                PlayerCharacter = playerChar,
                SelectedCharacter = playerChar,
                LockCameraToSelectedCharacter = true
            };

            using var renderer = new WorldRenderer();
            renderer.Initialize(dispatcher);

            engine.OnTickComplete += (sender, e) => 
            {
                uiState.TickCount = e.TickCount;
                var currentSimTime = startTime.AddSeconds(e.TickCount * engine.TickDuration.TotalSeconds * (double)engine.TimeScaleMultiplier);
                worldMap.UpdateSimulationTime(currentSimTime);
                uiState.CurrentWorldTime = currentSimTime;
                uiState.CurrentTimeScale = engine.TimeScaleMultiplier;
            };

            engine.Start(TimeSpan.FromMilliseconds(200));

            bool wasInCombat = false;

            while (!Raylib.WindowShouldClose())
            {
                // Key bindings for time scale
                if (Raylib.IsKeyPressed(KeyboardKey.One)) engine.TimeScaleMultiplier = 1.0m;
                if (Raylib.IsKeyPressed(KeyboardKey.Two)) engine.TimeScaleMultiplier = 10.0m;
                if (Raylib.IsKeyPressed(KeyboardKey.Three)) engine.TimeScaleMultiplier = 60.0m; // 1 min per sec
                if (Raylib.IsKeyPressed(KeyboardKey.Four)) engine.TimeScaleMultiplier = 600.0m; // 10 min per sec
                if (Raylib.IsKeyPressed(KeyboardKey.Five)) { engine.TimeScaleMultiplier = 3200.0m; engine.StopAsync().Wait(); engine.Start(TimeSpan.FromMilliseconds(10)); } // Fast
                if (Raylib.IsKeyPressed(KeyboardKey.Six)) { engine.TimeScaleMultiplier = 3200.0m; engine.StopAsync().Wait(); engine.Start(TimeSpan.Zero); } // Unrestrained

                // Determine if in combat (Hostiles within 10 tiles)
                var pLoc = worldMap.GetCharacterLocation(playerChar);
                bool inCombat = false;
                if (pLoc != null)
                {
                    inCombat = worldMap.GetCharacters().Any(c => 
                        c.Name.Contains("Goblin") && !c.IsDead && 
                        worldMap.GetCharacterLocation(c).HasValue && 
                        worldMap.GetCharacterLocation(c).Value.Z == pLoc.Value.Z &&
                        Math.Abs(worldMap.GetCharacterLocation(c).Value.X - pLoc.Value.X) <= 1 &&
                        Math.Abs(worldMap.GetCharacterLocation(c).Value.Y - pLoc.Value.Y) <= 1);
                }

                if (inCombat && !wasInCombat)
                {
                    await engine.StopAsync();
                }
                else if (!inCombat && wasInCombat)
                {
                    engine.Start(TimeSpan.FromMilliseconds(200));
                }

                wasInCombat = inCombat;

                // Player Input
                NPC.Library.Behaviors.Player.PlayerIntent moveIntent = NPC.Library.Behaviors.Player.PlayerIntent.None;
                bool playerActed = false;

                if (inCombat)
                {
                    // Discrete input for turn-based combat
                    if (Raylib.IsKeyPressed(KeyboardKey.W) || Raylib.IsKeyPressedRepeat(KeyboardKey.W)) { moveIntent = NPC.Library.Behaviors.Player.PlayerIntent.MoveLeft; playerActed = true; }
                    else if (Raylib.IsKeyPressed(KeyboardKey.S) || Raylib.IsKeyPressedRepeat(KeyboardKey.S)) { moveIntent = NPC.Library.Behaviors.Player.PlayerIntent.MoveRight; playerActed = true; }
                    else if (Raylib.IsKeyPressed(KeyboardKey.A) || Raylib.IsKeyPressedRepeat(KeyboardKey.A)) { moveIntent = NPC.Library.Behaviors.Player.PlayerIntent.MoveDown; playerActed = true; }
                    else if (Raylib.IsKeyPressed(KeyboardKey.D) || Raylib.IsKeyPressedRepeat(KeyboardKey.D)) { moveIntent = NPC.Library.Behaviors.Player.PlayerIntent.MoveUp; playerActed = true; }
                    else if (Raylib.IsKeyPressed(KeyboardKey.Space) || Raylib.IsKeyPressedRepeat(KeyboardKey.Space)) { playerActed = true; } // Wait a turn
                    
                    if (Raylib.IsKeyPressed(KeyboardKey.E)) { playerController.EnqueueIntent(NPC.Library.Behaviors.Player.PlayerIntent.Interact); playerActed = true; }
                    if (Raylib.IsKeyPressed(KeyboardKey.R)) { playerController.EnqueueIntent(NPC.Library.Behaviors.Player.PlayerIntent.Eat); playerActed = true; }
                    if (Raylib.IsKeyPressed(KeyboardKey.Q)) { playerController.EnqueueIntent(NPC.Library.Behaviors.Player.PlayerIntent.Drink); playerActed = true; }

                    if (playerActed)
                    {
                        playerController.SetMovementIntent(moveIntent);
                        await engine.TickOnceAsync();
                    }
                    else
                    {
                        playerController.SetMovementIntent(NPC.Library.Behaviors.Player.PlayerIntent.None);
                    }
                }
                else
                {
                    // Continuous input for real-time mode
                    if (Raylib.IsKeyDown(KeyboardKey.W)) moveIntent = NPC.Library.Behaviors.Player.PlayerIntent.MoveLeft;
                    else if (Raylib.IsKeyDown(KeyboardKey.S)) moveIntent = NPC.Library.Behaviors.Player.PlayerIntent.MoveRight;
                    else if (Raylib.IsKeyDown(KeyboardKey.A)) moveIntent = NPC.Library.Behaviors.Player.PlayerIntent.MoveDown;
                    else if (Raylib.IsKeyDown(KeyboardKey.D)) moveIntent = NPC.Library.Behaviors.Player.PlayerIntent.MoveUp;
                    
                    playerController.SetMovementIntent(moveIntent);

                    if (Raylib.IsKeyPressed(KeyboardKey.E)) playerController.EnqueueIntent(NPC.Library.Behaviors.Player.PlayerIntent.Interact);
                    if (Raylib.IsKeyPressed(KeyboardKey.R)) playerController.EnqueueIntent(NPC.Library.Behaviors.Player.PlayerIntent.Eat);
                    if (Raylib.IsKeyPressed(KeyboardKey.Q)) playerController.EnqueueIntent(NPC.Library.Behaviors.Player.PlayerIntent.Drink);
                }

                renderer.Render(uiState);
            }
            
            await engine.StopAsync();
            engine.Dispose();
        }
    }
}
