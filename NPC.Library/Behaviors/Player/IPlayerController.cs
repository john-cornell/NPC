namespace NPC.Library.Behaviors.Player;
using System.Threading.Tasks;
using NPC.Library.Character;

public interface IPlayerController
{
    Task TickAsync(Character character);
    void EnqueueIntent(PlayerIntent intent);
}
