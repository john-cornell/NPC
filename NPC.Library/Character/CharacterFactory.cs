namespace NPC.Library.Character;

/// <summary>
/// Creates <see cref="Character"/> instances. Central place to wire new subsystems at spawn.
/// </summary>
public sealed class CharacterFactory
{
    private static readonly Random _random = new();
    private readonly NPC.Library.Messaging.MessageDispatcher? _dispatcher;

    public CharacterFactory(NPC.Library.Messaging.MessageDispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>Creates a character with the universal drive set at baseline levels.</summary>
    public Character Create()
    {
        var character = new Character(DriveDefaults.CreateBaseline(), _dispatcher);
        int sight = _random.Next(1, 6);
        character.AddComponent<NPC.Library.Simulation.VisionComponent>(new NPC.Library.Simulation.VisionComponent(sight));
        return character;
    }

    /// <summary>Creates a character with universal drives at baseline, then applies overrides.</summary>
    public Character Create(IEnumerable<KeyValuePair<DriveType, decimal>> driveOverrides)
    {
        var character = new Character(DriveDefaults.CreateBaseline(driveOverrides), _dispatcher);
        int sight = _random.Next(1, 6);
        character.AddComponent<NPC.Library.Simulation.VisionComponent>(new NPC.Library.Simulation.VisionComponent(sight));
        return character;
    }
}
