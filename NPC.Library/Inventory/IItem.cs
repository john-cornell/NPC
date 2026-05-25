namespace NPC.Library.Inventory;

/// <summary>
/// Represents a physical item that can be carried in an inventory.
/// </summary>
public interface IItem
{
    /// <summary>
    /// The tag identifying what this item is.
    /// </summary>
    ItemType Type { get; }
}
