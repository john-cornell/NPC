namespace NPC.Library.Inventory;

public class Item : IItem
{
    public ItemType Type { get; }

    public Item(ItemType type)
    {
        Type = type;
    }
}
