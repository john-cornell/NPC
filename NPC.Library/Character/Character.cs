namespace NPC.Library.Character;

/// <summary>
/// Runtime state for one NPC. Create via <see cref="CharacterFactory"/>.
/// </summary>
public sealed class Character
{
    internal Character(Drives drives) => Drives = drives;

    public Drives Drives { get; }
}
