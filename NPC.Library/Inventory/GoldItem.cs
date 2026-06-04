namespace NPC.Library.Inventory;

public class GoldItem : Item
{
    public int Amount { get; set; }

    public GoldItem(int amount) : base(ItemType.Gold)
    {
        Amount = amount;
    }
}
