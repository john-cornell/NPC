namespace NPC.Library.Inventory;

public class MeatItem : Item
{
    public int BitesRemaining { get; set; } = 3;

    public MeatItem() : base(ItemType.Meat)
    {
    }

    public void Eat()
    {
        if (BitesRemaining > 0)
        {
            BitesRemaining--;
        }
    }
}
