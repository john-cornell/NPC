namespace NPC.Library.Spatial;

using System.Collections.Generic;
using NPC.Library.Character;

/// <summary>
/// A generic interface representing the spatial environment.
/// </summary>
public interface ISpatialContext
{
    /// <summary>
    /// Gets the path from start to target. Returns empty if unreachable.
    /// </summary>
    IEnumerable<(int X, int Y)> GetPath((int X, int Y) start, (int X, int Y) target);

    /// <summary>
    /// Returns a random location that a character can physically traverse.
    /// </summary>
    (int X, int Y) GetRandomWalkableLocation();

    /// <summary>
    /// Retrieves the current physical coordinate of a character.
    /// </summary>
    (int X, int Y)? GetCharacterLocation(Character character);

    /// <summary>
    /// Updates the character's location in the world.
    /// </summary>
    void MoveCharacter(Character character, (int X, int Y) newLocation);
    /// <summary>
    /// Gets all characters currently tracked in the spatial context.
    /// </summary>
    IEnumerable<Character> GetCharacters();

    /// <summary>
    /// Gets the number of apples currently on a tree tile.
    /// </summary>
    int GetAppleCount((int X, int Y) location);

    /// <summary>
    /// Attempts to gather an apple from a tree. Returns true if successful.
    /// </summary>
    bool TryGatherApple((int X, int Y) location);

    /// <summary>
    /// Ticks the environment (e.g., regrowing resources).
    /// </summary>
    void TickEnvironment();
}
