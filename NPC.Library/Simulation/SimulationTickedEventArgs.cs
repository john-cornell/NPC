namespace NPC.Library.Simulation;

using System;

/// <summary>
/// Event arguments fired when the simulation loop completes a full tick.
/// </summary>
public class SimulationTickedEventArgs : EventArgs
{
    public TimeSpan DeltaTime { get; }
    public long TickCount { get; }
    public int ActiveCharactersCount { get; }

    public SimulationTickedEventArgs(TimeSpan deltaTime, long tickCount, int activeCharactersCount)
    {
        DeltaTime = deltaTime;
        TickCount = tickCount;
        ActiveCharactersCount = activeCharactersCount;
    }
}
