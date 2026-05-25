namespace NPC.Library.Inventory;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A basic list-backed inventory implementation.
/// </summary>
public class StandardInventory : IInventory
{
    private readonly List<IItem> _items = new();
    private readonly int _capacity;

    public StandardInventory(int capacity = int.MaxValue)
    {
        _capacity = capacity;
    }

    public bool AddItem(IItem item)
    {
        if (_items.Count >= _capacity) return false;
        _items.Add(item);
        return true;
    }

    public bool RemoveItem(IItem item)
    {
        return _items.Remove(item);
    }

    public bool HasItem(ItemType type)
    {
        return _items.Any(i => i.Type == type);
    }

    public IItem? ConsumeItem(ItemType type)
    {
        var item = _items.FirstOrDefault(i => i.Type == type);
        if (item != null)
        {
            _items.Remove(item);
            return item;
        }
        return null;
    }

    public IEnumerable<IItem> GetItems()
    {
        return _items.AsReadOnly();
    }
}
