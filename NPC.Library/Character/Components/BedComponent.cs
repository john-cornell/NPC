namespace NPC.Library.Character.Components;

public class BedComponent
{
    public (int X, int Y) Location { get; }

    public BedComponent(int x, int y)
    {
        Location = (x, y);
    }
}
