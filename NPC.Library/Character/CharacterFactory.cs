namespace NPC.Library.Character;

/// <summary>
/// Creates <see cref="Character"/> instances. Central place to wire new subsystems at spawn.
/// </summary>
public sealed class CharacterFactory
{
    /// <summary>Creates a character with the universal drive set at baseline levels.</summary>
    public Character Create() => new(DriveDefaults.CreateBaseline());

    /// <summary>Creates a character with universal drives at baseline, then applies overrides.</summary>
    public Character Create(IEnumerable<KeyValuePair<DriveType, decimal>> driveOverrides) =>
        new(DriveDefaults.CreateBaseline(driveOverrides));
}
