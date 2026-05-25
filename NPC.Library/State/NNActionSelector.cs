using System;
using System.Collections.Generic;
using System.Linq;
using NPC.Library.Character;
using NPC.Library.Decision;

namespace NPC.Library.State
{
    public sealed class NNActionSelector : IActionSelector
    {
        private readonly NPC.Library.Spatial.ISpatialContext _spatialContext;

        public NNActionSelector(NPC.Library.Spatial.ISpatialContext spatialContext)
        {
            _spatialContext = spatialContext;
        }

        public IActuator? Select(IEnumerable<IActuator> actuators, NPC.Library.Character.Character character, NPC.Library.Character.DriveType currentDrive)
        {
            var brain = character.GetComponent<NeuralNetwork>();
            if (brain == null) return null; // Or fallback to random

            var list = actuators.ToList();
            if (list.Count == 0) return null;

            float[] inputs = BuildInputs(character);
            float[] outputs = brain.FeedForward(inputs);

            IActuator bestActuator = list[0];
            float bestScore = float.MinValue;

            foreach (var actuator in list)
            {
                int outputIndex = GetActuatorOutputIndex(actuator);
                if (outputIndex >= 0 && outputIndex < outputs.Length)
                {
                    if (outputs[outputIndex] > bestScore)
                    {
                        bestScore = outputs[outputIndex];
                        bestActuator = actuator;
                    }
                }
            }

            return bestActuator;
        }

        private float[] BuildInputs(NPC.Library.Character.Character character)
        {
            float[] inputs = new float[16];

            // 1. Hunger (Satiety is 1.0 when full, 0.0 when starving)
            // Inverted so 1.0 means extremely hungry
            inputs[0] = 1.0f - GetDriveValue(character, NPC.Library.Character.DriveType.Satiety, 1.0f);
            
            // 2. Thirst
            inputs[1] = 1.0f - GetDriveValue(character, NPC.Library.Character.DriveType.Thirst, 1.0f);
            
            // 3. Fatigue (1.0 means exhausted)
            inputs[2] = GetDriveValue(character, NPC.Library.Character.DriveType.Fatigue, 0.0f);

            // 4. Social Deprivation
            inputs[3] = 1.0f - GetDriveValue(character, NPC.Library.Character.DriveType.Social, 1.0f);

            // 5. Overall Health or baseline drive
            inputs[4] = 1.0f; 

            // 6-7. Inventory Checks
            var inventory = character.GetComponent<NPC.Library.Inventory.IInventory>();
            inputs[5] = (inventory != null && inventory.GetItems().Any(i => i.Type == NPC.Library.Inventory.ItemType.Apple)) ? 1.0f : 0.0f; 
            inputs[6] = (inventory != null && inventory.GetItems().Any(i => i.Type == NPC.Library.Inventory.ItemType.WaterBottle)) ? 1.0f : 0.0f;

            // Remaining inputs 7-15 are reserved for environmental/spatial senses
            if (_spatialContext is NPC.Library.Spatial.Grid.GridSpatialContext gridCtx)
            {
                var loc = gridCtx.GetCharacterLocation(character);
                if (loc.HasValue)
                {
                    // Normalized Distance X, Y to Well
                    var well = gridCtx.Map.WellLocation;
                    inputs[7] = Math.Clamp((well.X - loc.Value.X) / 100f, -1.0f, 1.0f);
                    inputs[8] = Math.Clamp((well.Y - loc.Value.Y) / 100f, -1.0f, 1.0f);

                    // Normalized Distance X, Y to closest Apple Tree
                    float closestTreeDist = float.MaxValue;
                    int closestTreeX = loc.Value.X;
                    int closestTreeY = loc.Value.Y;

                    foreach (var treeLoc in gridCtx.Map.TreeApples.Keys)
                    {
                        if (gridCtx.Map.TreeApples[treeLoc] > 0)
                        {
                            float dx = treeLoc.X - loc.Value.X;
                            float dy = treeLoc.Y - loc.Value.Y;
                            float d = (float)Math.Sqrt(dx * dx + dy * dy);
                            if (d < closestTreeDist)
                            {
                                closestTreeDist = d;
                                closestTreeX = treeLoc.X;
                                closestTreeY = treeLoc.Y;
                            }
                        }
                    }

                    if (closestTreeDist < float.MaxValue)
                    {
                        inputs[9] = Math.Clamp((closestTreeX - loc.Value.X) / 100f, -1.0f, 1.0f);
                        inputs[10] = Math.Clamp((closestTreeY - loc.Value.Y) / 100f, -1.0f, 1.0f);
                    }
                }
            }
            
            return inputs;
        }

        private float GetDriveValue(NPC.Library.Character.Character character, NPC.Library.Character.DriveType drive, float defaultValue)
        {
            if (character.Drives.Levels.TryGetValue(drive, out var val))
            {
                return (float)val;
            }
            return defaultValue;
        }

        private int GetActuatorOutputIndex(IActuator actuator)
        {
            string name = actuator.GetType().Name;
            return name switch
            {
                "WanderActuator" or "VillageWanderActuator" => 0,
                "SearchForFoodActuator" => 1,
                "GatherFoodActuator" or "VillageGatherFoodActuator" => 2,
                "EatActuator" => 3,
                "RestActuator" or "VillageSleepInBedActuator" => 4,
                "GatherWaterActuator" or "VillageGatherWaterActuator" => 5,
                "DrinkActuator" => 6,
                "VillageSocializeActuator" => 7,
                "VillageStoreItemActuator" => 8,
                "VillageRetrieveItemActuator" => 9,
                _ => 10
            };
        }
    }
}
