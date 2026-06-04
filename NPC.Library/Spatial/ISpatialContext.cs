namespace NPC.Library.Spatial;

using System.Collections.Generic;
using NPC.Library.Character;

/// <summary>
/// A generic interface representing the spatial environment.
/// </summary>
public interface ISpatialContext
{
    /// <summary>
    /// Gets the current in-simulation time.
    /// </summary>
    System.DateTime CurrentTime { get; }

    /// <summary>
    /// Gets the path from start to target. Returns empty if unreachable.
    /// </summary>
    IEnumerable<(int X, int Y, int Z)> GetPath((int X, int Y, int Z) start, (int X, int Y, int Z) target);

    /// <summary>
    /// Returns a random location that a character can physically traverse.
    /// </summary>
    (int X, int Y, int Z) GetRandomWalkableLocation();

    /// <summary>
    /// Retrieves the current physical coordinate of a character.
    /// </summary>
    (int X, int Y, int Z)? GetCharacterLocation(Character character);

    /// <summary>
    /// Updates the character's location in the world.
    /// </summary>
    void MoveCharacter(Character character, (int X, int Y, int Z) newLocation);
    /// <summary>
    /// Gets all characters currently tracked in the spatial context.
    /// </summary>
    IEnumerable<Character> GetCharacters();

    /// <summary>
    /// Gets the number of apples currently on a tree tile.
    /// </summary>
    int GetAppleCount((int X, int Y, int Z) location);

    /// <summary>
    /// Attempts to gather an apple from a tree. Returns true if successful.
    /// </summary>
    bool TryGatherApple((int X, int Y, int Z) location);

    /// <summary>
    /// Gets the chest inventory at the specified location, if one exists.
    /// </summary>
    NPC.Library.Inventory.IInventory? GetChest((int X, int Y, int Z) location);

    /// <summary>
    /// Gets the TileType at the specified location.
    /// </summary>
    NPC.Library.Spatial.Grid.TileType GetTile((int X, int Y, int Z) location);

    /// <summary>
    /// Drops an item on the ground at the specified location.
    /// </summary>
    void DropItem((int X, int Y, int Z) location, NPC.Library.Inventory.IItem item);

    /// <summary>
    /// Gets items on the ground at the specified location.
    /// </summary>
    IEnumerable<NPC.Library.Inventory.IItem> GetGroundItems((int X, int Y, int Z) location);

    /// <summary>
    /// Removes an item from the ground at the specified location.
    /// </summary>
    void RemoveGroundItem((int X, int Y, int Z) location, NPC.Library.Inventory.IItem item);

    /// <summary>
    /// Ticks the environment (e.g., regrowing resources).
    /// </summary>
    void TickEnvironment();
}
