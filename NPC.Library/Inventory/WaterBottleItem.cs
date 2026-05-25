namespace NPC.Library.Inventory;

public class WaterBottleItem : IItem
{
    public ItemType Type => ItemType.WaterBottle;
    
    public int SipsRemaining { get; private set; }

    public WaterBottleItem(int startingSips = 0)
    {
        SipsRemaining = startingSips;
    }

    public void Drink()
    {
        if (SipsRemaining > 0) SipsRemaining--;
    }

    public void Refill()
    {
        SipsRemaining = 3;
    }
}
