namespace NPC.Library.Character.Components;

public class BedComponent
{
    public (int X, int Y, int Z) Location { get; }

    public BedComponent(int x, int y, int z = 0)
    {
        Location = (x, y, z);
    }
}
