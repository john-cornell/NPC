namespace NPC.Library.Inventory;

using System.Collections.Generic;

/// <summary>
/// A component attached to a character that tracks held items.
/// </summary>
public interface IInventory
{
    /// <summary>
    /// Adds a physical item to the inventory. Returns true if successful.
    /// </summary>
    bool AddItem(IItem item);

    /// <summary>
    /// Removes a specific physical item from the inventory. Returns true if successful.
    /// </summary>
    bool RemoveItem(IItem item);

    /// <summary>
    /// Checks if the inventory contains at least one item of the specified type.
    /// </summary>
    bool HasItem(ItemType type);

    /// <summary>
    /// Removes and returns one item of the specified type from the inventory, if it exists.
    /// </summary>
    IItem? ConsumeItem(ItemType type);

    /// <summary>
    /// Retrieves all items currently in the inventory.
    /// </summary>
    IEnumerable<IItem> GetItems();
}
