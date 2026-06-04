using System;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.Behaviors.Player;
using NPC.Library.Spatial;
using NPC.Library.Spatial.Grid;
using NPC.World.Map;
using Xunit;
using Moq;
using System.Collections.Generic;

namespace NPC.Tests
{
    public class PlayerCaveInteractionTests
    {
        [Fact]
        public async Task PlayerController_Interact_OnCaveEntrance_MovesToZMinusOne()
        {
            // Arrange
            var mockSpatial = new Mock<ISpatialContext>();
            var factory = new CharacterFactory(new NPC.Library.Messaging.MessageDispatcher());
            var player = factory.Create();
            player.Name = "Player";
            var controller = new PlayerController(mockSpatial.Object);

            var startLoc = (110, 50, 0);
            mockSpatial.Setup(s => s.GetCharacterLocation(player)).Returns(startLoc);
            
            // Setup adjacent tiles: the player is STANDING ON the CaveEntrance
            mockSpatial.Setup(s => s.GetTile(startLoc)).Returns(TileType.CaveEntrance);

            // Act
            controller.EnqueueIntent(PlayerIntent.Interact);
            await controller.TickAsync(player);

            // Assert
            mockSpatial.Verify(s => s.MoveCharacter(player, It.Is<ValueTuple<int, int, int>>(loc => loc.Item3 == -1 && loc.Item1 == 110 && loc.Item2 == 50)), Times.Once);
            Assert.Equal("Entered Cave", player.LastAction);
        }

        [Fact]
        public void WorldMap_MoveCharacter_ChangesZCoordinate()
        {
            // Arrange
            var chunkManager = new ChunkManager();
            var worldMap = new WorldMap(chunkManager);
            var factory = new CharacterFactory(new NPC.Library.Messaging.MessageDispatcher());
            var player = factory.Create();
            player.Name = "Player";

            // Initialize player at (110, 50, 0)
            worldMap.MoveCharacter(player, (110, 50, 0));
            Assert.Equal(0, worldMap.GetCharacterLocation(player).Value.Z);

            // Act: Move to Z=-1
            worldMap.MoveCharacter(player, (110, 50, -1));

            // Assert
            var newLoc = worldMap.GetCharacterLocation(player);
            Assert.True(newLoc.HasValue);
            Assert.Equal(-1, newLoc.Value.Z);
        }
    }
}
