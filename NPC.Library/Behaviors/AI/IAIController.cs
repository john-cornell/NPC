namespace NPC.Library.Behaviors.AI;

using System.Threading.Tasks;
using NPC.Library.Character;

public interface IAIController
{
    Task TickAsync(Character character);
}
