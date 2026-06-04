namespace NPC.Library.Simulation;

using System;
using NPC.Library.Memory;
using NPC.Library.Spatial;
using NPC.Library.Spatial.Grid;
using NPC.Library.State;

/// <summary>
/// A completely passive listener that performs sensory updates (vision)
/// whenever a character executes an action (e.g. moves).
/// </summary>
public class VisionTracker
{
    private readonly ISpatialContext _spatialContext;

    public VisionTracker(StateMachine stateMachine, ISpatialContext spatialContext)
    {
        _spatialContext = spatialContext ?? throw new ArgumentNullException(nameof(spatialContext));
        
        // Subscribe to the global state machine
        stateMachine.OnActuatorExecuted += HandleActuatorExecuted;
    }

    private void HandleActuatorExecuted(object? sender, ActuatorExecutedEventArgs e)
    {
        var character = e.Character;
        
        // Does this character even have a memory component?
        if (!character.TryGetComponent<IMemory>(out var memory))
            return;

        int visionRadius = 3;
        if (character.TryGetComponent<NPC.Library.Simulation.VisionComponent>(out var vision))
        {
            visionRadius = vision.SightLength;
        }

        var loc = _spatialContext.GetCharacterLocation(character);
        if (loc == null) return;

        int cx = loc.Value.X;
        int cy = loc.Value.Y;

        int cz = loc.Value.Z;

        for (int y = cy - visionRadius; y <= cy + visionRadius; y++)
        {
            for (int x = cx - visionRadius; x <= cx + visionRadius; x++)
            {
                var tile = _spatialContext.GetTile((x, y, cz));
                // Record interesting things
                if (tile == TileType.AppleTree)
                {
                    if (_spatialContext.GetAppleCount((x, y, cz)) > 0)
                    {
                        memory.Remember(tile, (x, y, cz));
                    }
                    else
                    {
                        // memory.Forget only supports x, y, we'll need to pass Z if we update IMemory
                        memory.Forget(tile, x, y, cz); 
                    }
                }
                else if (tile == TileType.Water)
                {
                    memory.Remember(tile, (x, y, cz));
                }
            }
        }
    }
}
