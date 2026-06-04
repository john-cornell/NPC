using System;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Simulation;
using NPC.Library.State;
using NPC.Library.Spatial;
using NPC.Library.Messaging;
using Xunit;
using System.Collections.Generic;

using Moq;

namespace NPC.Tests
{
    public class SimulationEngineTests
    {
        private class MockSpatialContext : ISpatialContext
        {
            public DateTime CurrentTime { get; set; } = DateTime.MinValue;
            public IEnumerable<(int X, int Y, int Z)> GetPath((int X, int Y, int Z) start, (int X, int Y, int Z) target) => new List<(int X, int Y, int Z)>();
            public (int X, int Y, int Z) GetRandomWalkableLocation() => (0, 0, 0);
            public (int X, int Y, int Z)? GetCharacterLocation(NPC.Library.Character.Character character) => (0, 0, 0);
            public void MoveCharacter(NPC.Library.Character.Character character, (int X, int Y, int Z) newLocation) { }
            public IEnumerable<NPC.Library.Character.Character> GetCharacters() => new List<NPC.Library.Character.Character>();
            public int GetAppleCount((int X, int Y, int Z) location) => 0;
            public bool TryGatherApple((int X, int Y, int Z) location) => false;
            public NPC.Library.Spatial.Grid.TileType GetTile((int X, int Y, int Z) location) => NPC.Library.Spatial.Grid.TileType.Grass;
            public NPC.Library.Inventory.IInventory? GetChest((int X, int Y, int Z) location) => null;
            public void TickEnvironment() { }

            public void DropItem((int X, int Y, int Z) location, NPC.Library.Inventory.IItem item) { }
            public IEnumerable<NPC.Library.Inventory.IItem> GetGroundItems((int X, int Y, int Z) location) => System.Array.Empty<NPC.Library.Inventory.IItem>();
            public void RemoveGroundItem((int X, int Y, int Z) location, NPC.Library.Inventory.IItem item) { }
        }

        [Fact]
        public async Task TimeDecayTests_StarvationAndThirst_TriggersCorrectly()
        {
            // Arrange
            var dispatcher = new MessageDispatcher();
            var stateMachine = new StateMachine(new RandomActionSelector(), dispatcher);
            var spatial = new MockSpatialContext();
            var engine = new SimulationEngine(stateMachine, spatial, dispatcher)
            {
                TimeScaleMultiplier = 1.0m,
                TickDuration = TimeSpan.FromSeconds(1)
            };

            var factory = new CharacterFactory(dispatcher);
            var character = factory.Create();
            character.AddComponent<NPC.Library.State.IActionResolver>(new CompositeActionResolver());
            
            engine.AddCharacter(character);

            // Act - Fast forward slightly over 2 days to account for decimal precision
            engine.TimeScaleMultiplier = 173000m; // 2 days is 172800s
            await engine.TickOnceAsync();

            // Assert
            Assert.True(character.IsDead);
            Assert.Equal("Dehydration", character.DeathReason);
        }

        [Fact]
        public async Task PassOutTests_FatigueMax_AppliesModifier()
        {
            // Arrange
            var dispatcher = new MessageDispatcher();
            var stateMachine = new StateMachine(new RandomActionSelector(), dispatcher);
            var spatial = new MockSpatialContext();
            var engine = new SimulationEngine(stateMachine, spatial, dispatcher)
            {
                TimeScaleMultiplier = 1.0m,
                TickDuration = TimeSpan.FromSeconds(1)
            };

            var factory = new CharacterFactory(dispatcher);
            var character = factory.Create(new[] { new KeyValuePair<NPC.Library.Character.DriveType, decimal>(NPC.Library.Character.DriveType.Fatigue, 0.999m) });
            character.AddComponent<NPC.Library.State.IActionResolver>(new CompositeActionResolver());
            engine.AddCharacter(character);

            // Fast forward 1 day so fatigue definitely hits 1.0
            engine.TimeScaleMultiplier = 86400m; // 1 tick = 1 day
            
            // Because passing out is a 5% chance per tick when fatigue >= 1, we might need multiple ticks
            bool passedOut = false;
            for (int i = 0; i < 1000; i++)
            {
                // Force fatigue back to 1.0 to simulate no bed
                character.Drives.SetLevel(NPC.Library.Character.DriveType.Fatigue, 1.0m);
                // Keep satiety and thirst high so they don't die while we wait for them to pass out
                character.Drives.SetLevel(NPC.Library.Character.DriveType.Satiety, 1.0m);
                character.Drives.SetLevel(NPC.Library.Character.DriveType.Thirst, 1.0m);

                await engine.TickOnceAsync();
                
                if (character.HasBadSleepModifier)
                {
                    passedOut = true;
                    break;
                }
            }

            // Assert
            Assert.True(passedOut, "Character should have passed out and received HasBadSleepModifier.");
        }
    [Fact]
    public async Task Animal_FoxHuntsAndEatsSheep()
    {
        var worldMap = new Mock<ISpatialContext>();
        var dispatcher = new Mock<NPC.Library.Messaging.MessageDispatcher>();
        var stateMachine = new StateMachine(new AnimalActionSelector(), dispatcher.Object);
        var engine = new SimulationEngine(stateMachine, worldMap.Object, dispatcher.Object);

        var animalResolver = new CompositeActionResolver();
        animalResolver.AddResolver(new SimpleActuatorGroup(new List<IActuator> {
            new NPC.Library.Behaviors.SheepGrazeActuator(worldMap.Object),
            new NPC.Library.Behaviors.SheepFleeActuator(worldMap.Object),
            new NPC.Library.Behaviors.FoxHuntActuator(worldMap.Object),
            new NPC.Library.Behaviors.FoxEatActuator(worldMap.Object),
            new NPC.Library.Behaviors.AnimalSleepActuator()
        }));

        var fox = new Animal("Fox", AnimalType.Fox);
        fox.AddComponent<IActionResolver>(animalResolver);
        fox.AddComponent<IActionSelector>(new AnimalActionSelector());
        fox.Drives[NPC.Library.Character.DriveType.Satiety] = 0.5m; // Hungry
        engine.AddCharacter(fox);

        var sheep = new Animal("Sheep", AnimalType.Sheep);
        sheep.AddComponent<IActionResolver>(animalResolver);
        sheep.AddComponent<IActionSelector>(new AnimalActionSelector());
        engine.AddCharacter(sheep);
        
        // Mock spatial context to return characters
        worldMap.Setup(x => x.GetCharacters()).Returns(new List<Character> { fox, sheep });
        worldMap.Setup(x => x.GetCharacterLocation(fox)).Returns((50, 50, 0));
        worldMap.Setup(x => x.GetCharacterLocation(sheep)).Returns((50, 51, 0)); // Adjacent

        // Tick once, fox should hunt and kill
        await engine.TickOnceAsync();
        
        Assert.True(sheep.IsDead);
        Assert.Equal("Attacking Sheep", fox.LastAction);
        
        // Tick again, fox should eat
        await engine.TickOnceAsync();
        
        Assert.Equal("Eating Sheep", fox.LastAction);
        Assert.True(fox.Drives[NPC.Library.Character.DriveType.Satiety] > 0.5m);
    }
}
}
