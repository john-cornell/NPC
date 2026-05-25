namespace NPC.Library.Simulation;

public class VisionComponent
{
    public int SightLength { get; }

    public VisionComponent(int sightLength)
    {
        SightLength = sightLength;
    }
}
