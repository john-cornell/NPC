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

        // If the spatial context is a Grid, we can check surrounding tiles
        if (_spatialContext is GridSpatialContext gridContext)
        {
            int cx = loc.Value.X;
            int cy = loc.Value.Y;
            var map = gridContext.Map;

            for (int y = Math.Max(0, cy - visionRadius); y <= Math.Min(map.Height - 1, cy + visionRadius); y++)
            {
                for (int x = Math.Max(0, cx - visionRadius); x <= Math.Min(map.Width - 1, cx + visionRadius); x++)
                {
                    var tile = map.Tiles[x, y];
                    // Record interesting things
                    if (tile == TileType.AppleTree)
                    {
                        if (gridContext.GetAppleCount((x, y)) > 0)
                        {
                            memory.Remember(tile, (x, y));
                        }
                        else
                        {
                            memory.Forget(tile, x, y);
                        }
                    }
                    else if (tile == TileType.Water)
                    {
                        memory.Remember(tile, (x, y));
                    }
                }
            }
        }
    }
}
