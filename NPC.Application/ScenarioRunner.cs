using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NPC.Library.Character;
using NPC.Library.Simulation;
using NPC.Library.State;
using NPC.Library.Spatial;
using NPC.Library.Spatial.Grid;
using NPC.Library.Behaviors;
using NPC.Village.Behaviors;
using NPC.Village.Map;
using NPC.Village.Memory;

namespace NPC.Application
{
    public class ScenarioContext
    {
        public SimulationEngine Engine { get; set; } = null!;
        public UIState InitialState { get; set; } = null!;
        public NPC.Library.Messaging.MessageDispatcher Dispatcher { get; set; } = null!;
    }

    public static class ScenarioRunner
    {
        public static ScenarioContext SetupTestScenario()
        {
            var services = new ServiceCollection();

            var map = MapGenerator.Generate(width: 800, height: 800, scale: 0.15, waterThreshold: -0.15, treeChance: 0.04);
            var spatialContext = new GridSpatialContext(map);
            services.AddSingleton<ISpatialContext>(spatialContext);

            services.AddSingleton<IActionSelector, PriorityActionSelector>();
            
            var resolver = new CompositeActionResolver();
            resolver.AddResolver(new NPC.Library.Behaviors.SurvivalActuatorGroup(spatialContext));
            services.AddSingleton<IActionResolver>(resolver);

            services.AddSingleton<NPC.Library.Messaging.MessageDispatcher>();
            services.AddSingleton<StateMachine>();
            services.AddSingleton(provider => 
            {
                var machine = provider.GetRequiredService<StateMachine>();
                var ctx = provider.GetRequiredService<ISpatialContext>();
                return new SimulationEngine(machine, ctx)
                {
                    SatietyDecayPerTick = 0.005m
                };
            });

            services.AddSingleton<NPC.Library.Simulation.VisionTracker>();

            var serviceProvider = services.BuildServiceProvider();

            var engine = serviceProvider.GetRequiredService<SimulationEngine>();
            var stateMachine = serviceProvider.GetRequiredService<StateMachine>();
            var visionTracker = serviceProvider.GetRequiredService<NPC.Library.Simulation.VisionTracker>();

            var factory = new CharacterFactory();
            
            var char1 = factory.Create();
            char1.AddComponent<NPC.Library.Memory.IMemory>(new NPC.Library.Memory.SpatialMemory());
            var inv1 = new NPC.Library.Inventory.StandardInventory();
            inv1.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
            char1.AddComponent<NPC.Library.Inventory.IInventory>(inv1);

            var char2 = factory.Create();
            char2.AddComponent<NPC.Library.Memory.IMemory>(new NPC.Library.Memory.SpatialMemory());
            var inv2 = new NPC.Library.Inventory.StandardInventory();
            inv2.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
            char2.AddComponent<NPC.Library.Inventory.IInventory>(inv2);

            var char3 = factory.Create();
            char3.AddComponent<NPC.Library.Memory.IMemory>(new NPC.Library.Memory.GoldfishMemory());
            var inv3 = new NPC.Library.Inventory.StandardInventory();
            inv3.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
            char3.AddComponent<NPC.Library.Inventory.IInventory>(inv3);

            var char4 = factory.Create();
            char4.AddComponent<NPC.Library.Memory.IMemory>(new NPC.Library.Memory.GoldfishMemory());
            var inv4 = new NPC.Library.Inventory.StandardInventory();
            inv4.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
            char4.AddComponent<NPC.Library.Inventory.IInventory>(inv4);

            var char5 = factory.Create();
            var inv5 = new NPC.Library.Inventory.StandardInventory();
            inv5.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
            char5.AddComponent<NPC.Library.Inventory.IInventory>(inv5);

            var char6 = factory.Create();
            var inv6 = new NPC.Library.Inventory.StandardInventory();
            inv6.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
            char6.AddComponent<NPC.Library.Inventory.IInventory>(inv6);

            engine.AddCharacter(char1);
            engine.AddCharacter(char2);
            engine.AddCharacter(char3);
            engine.AddCharacter(char4);
            engine.AddCharacter(char5);
            engine.AddCharacter(char6);

            void PlaceCharacter(Character c)
            {
                var loc = spatialContext.GetRandomWalkableLocation();
                map.Tiles[loc.X, loc.Y] = TileType.House;
                c.AddComponent(new NPC.Library.Character.Components.BedComponent(loc.X, loc.Y));
                spatialContext.MoveCharacter(c, loc);
            }

            PlaceCharacter(char1);
            PlaceCharacter(char2);
            PlaceCharacter(char3);
            PlaceCharacter(char4);
            PlaceCharacter(char5);
            PlaceCharacter(char6);

            var state = new UIState
            {
                SpatialContext = spatialContext,
                SelectedCharacter = char1,
                ViewMode = 1
            };

            return new ScenarioContext { Engine = engine, InitialState = state };
        }

        public static ScenarioContext SetupVillageScenario(int npcCount = 6, List<NPC.Library.Decision.NeuralNetwork>? loadedGenetics = null)
        {
            var services = new ServiceCollection();

            var map = VillageMapGenerator.Generate(width: 200, height: 100, npcCount: npcCount, out var bedLocations, out var doorLocations, out var chestLocations, out var wellLocation);
            var spatialContext = new GridSpatialContext(map);
            services.AddSingleton<ISpatialContext>(spatialContext);

            if (loadedGenetics != null && loadedGenetics.Count > 0)
            {
                services.AddSingleton<IActionSelector, NNActionSelector>();
            }
            else
            {
                services.AddSingleton<IActionSelector, PriorityActionSelector>();
            }
            
            services.AddSingleton<NPC.Library.Messaging.MessageDispatcher>();
            
            var resolver = new CompositeActionResolver();
            services.AddSingleton<IActionResolver>(provider => 
            {
                var dispatcher = provider.GetRequiredService<NPC.Library.Messaging.MessageDispatcher>();
                var ctx = provider.GetRequiredService<ISpatialContext>();
                resolver.AddResolver(new NPC.Village.Behaviors.VillageActuatorGroup(ctx, dispatcher));
                return resolver;
            });

            services.AddSingleton<StateMachine>();
            services.AddSingleton(provider => 
            {
                var machine = provider.GetRequiredService<StateMachine>();
                var ctx = provider.GetRequiredService<ISpatialContext>();
                var dispatcher = provider.GetRequiredService<NPC.Library.Messaging.MessageDispatcher>();
                return new SimulationEngine(machine, ctx, dispatcher)
                {
                    SatietyDecayPerTick = 0.01m
                };
            });

            services.AddSingleton<NPC.Library.Simulation.VisionTracker>();

            var serviceProvider = services.BuildServiceProvider();

            var engine = serviceProvider.GetRequiredService<SimulationEngine>();
            var stateMachine = serviceProvider.GetRequiredService<StateMachine>();
            var visionTracker = serviceProvider.GetRequiredService<NPC.Library.Simulation.VisionTracker>();
            var dispatcher = serviceProvider.GetRequiredService<NPC.Library.Messaging.MessageDispatcher>();

            var factory = new CharacterFactory(dispatcher);
            
            var characters = new List<Character>();
            
            var aiSettings = AISettingsManager.LoadSettings();

            string runLogDir = Path.Combine(aiSettings.LogDirectory, $"Run_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(runLogDir);
            
            var logger = new SimulationLogger(runLogDir, dispatcher);
            engine.OnTickComplete += (s, e) => logger.UpdateTick(e.TickCount);

            for (int i = 0; i < npcCount; i++)
            {
                var c = factory.Create();
                c.Name = $"NPC {i + 1}";
                
                var mem = new NPC.Village.Memory.VillageMemory(wellLocation, doorLocations[i], chestLocations[i], bedLocations[i]);
                c.AddComponent<NPC.Library.Memory.IMemory>(mem);
                
                if (loadedGenetics != null && loadedGenetics.Count > 0)
                {
                    var brain = loadedGenetics[i % loadedGenetics.Count].Clone();
                    c.AddComponent<NPC.Library.Decision.NeuralNetwork>(brain);
                }

                characters.Add(c);
            }

            foreach (var c in characters)
            {
                var inv = new NPC.Library.Inventory.StandardInventory();
                inv.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
                c.AddComponent<NPC.Library.Inventory.IInventory>(inv);
                
                if (aiSettings.IndividualOverrides.TryGetValue(c.Name, out var overrideConfig))
                {
                    c.AddComponent(new NPC.Library.Character.Components.LLMComponent { Config = overrideConfig });
                }

                engine.AddCharacter(c);
            }

            for (int i = 0; i < characters.Count; i++)
            {
                var loc = bedLocations[i];
                characters[i].AddComponent(new NPC.Library.Character.Components.BedComponent(loc.X, loc.Y));
                spatialContext.MoveCharacter(characters[i], loc);

                // Add 2 empty water bottles to their chest
                var chestLoc = chestLocations[i];
                if (map.Chests.TryGetValue(chestLoc, out var chestInv))
                {
                    chestInv.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
                    chestInv.AddItem(new NPC.Library.Inventory.WaterBottleItem(0));
                }
            }

            var state = new UIState
            {
                SpatialContext = spatialContext,
                SelectedCharacter = characters[0],
                ViewMode = 1,
                WellLocation = wellLocation,
                GlobalLLMConfig = aiSettings.GlobalConfig
            };

            state.CameraX = Math.Max(0, wellLocation.X - state.CameraWidth / 2);
            state.CameraY = Math.Max(0, wellLocation.Y - state.CameraHeight / 2);

            // Instantiate reasoning service
            var reasoningService = new LLMReasoningService(dispatcher, state, spatialContext);
            // It will keep itself alive via the dispatcher subscription, but we could add it to state if needed.

            return new ScenarioContext { Engine = engine, InitialState = state, Dispatcher = dispatcher };
        }
    }
}
