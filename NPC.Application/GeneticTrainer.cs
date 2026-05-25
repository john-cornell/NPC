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
            
            var evolutionManager = new GeneticEvolutionManager(populationSize, mutationRate: 0.15f, mutationAmount: 0.3f, elitismRatio: 0.2f);
            
            // Generate initial random population
            var currentPopulation = new List<NeuralNetwork>();
            for (int i = 0; i < populationSize; i++)
            {
                // 16 Inputs -> 12 Hidden -> 11 Outputs (Actuators)
                currentPopulation.Add(new NeuralNetwork(new[] { 16, 12, 11 }));
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
                
                var resolver = new CompositeActionResolver();
                resolver.AddResolver(new NPC.Village.Behaviors.VillageActuatorGroup(spatialContext, dispatcher));

                var stateMachine = new StateMachine(resolver, new NNActionSelector(spatialContext), dispatcher);
                var engine = new SimulationEngine(stateMachine, spatialContext, dispatcher)
                {
                    SatietyDecayPerTick = 0.005m
                };

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
                    
                    // Assign the specific neural network to this clone
                    var brain = currentPopulation[i];
                    c.AddComponent<NeuralNetwork>(brain);
                    
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

                // Run fast-forward simulation
                for (int t = 0; t < ticksPerGeneration; t++)
                {
                    await engine.TickOnceAsync();
                    
                    // If all died, end early
                    if (characters.All(c => c.IsDead)) break;
                }

                // Evaluate Fitness
                var evaluatedPop = new List<(NeuralNetwork Brain, float Fitness)>();
                float totalFitness = 0;
                
                for (int i = 0; i < populationSize; i++)
                {
                    var c = characters[i];
                    var loc = spatialContext.GetCharacterLocation(c);
                    
                    // Fitness heuristic:
                    // 1 point per tick lived.
                    // Bonus points for remaining satiety and thirst if survived to the end.
                    float fitness = c.IsDead ? c.DeathTick : ticksPerGeneration;
                    
                    if (!c.IsDead)
                    {
                        if (c.Drives.TryGetLevel(NPC.Library.Character.DriveType.Satiety, out var satiety)) fitness += (float)satiety * 100;
                        if (c.Drives.TryGetLevel(NPC.Library.Character.DriveType.Thirst, out var thirst)) fitness += (float)thirst * 100;
                    }
                    else
                    {
                        // Proximity bonus to well to encourage movement and break the 101 flatline
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

                Console.WriteLine($"Average Fitness: {totalFitness / populationSize:F2} | Best All-Time: {evolutionManager.BestEverFitness:F2}");
                
                // Breed next generation
                currentPopulation = evolutionManager.Evolve(evaluatedPop);
            }
            
            Console.WriteLine("Training Complete!");
        }
    }
}
