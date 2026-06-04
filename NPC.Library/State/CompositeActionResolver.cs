namespace NPC.Library.State;

using System;
using System.Collections.Generic;
using System.Linq;
using NPC.Library.Character;

/// <summary>
/// Aggregates multiple IActionResolvers (or ActuatorGroups) and queries all of them.
/// </summary>
public class CompositeActionResolver : IActionResolver
{
    private readonly List<IActionResolver> _resolvers = new();

    public void AddResolver(IActionResolver resolver)
    {
        if (resolver == null) throw new ArgumentNullException(nameof(resolver));
        _resolvers.Add(resolver);
    }

    public void RemoveResolver(IActionResolver resolver)
    {
        _resolvers.Remove(resolver);
    }

    public void ClearResolvers()
    {
        _resolvers.Clear();
    }

    public IEnumerable<IActuator> GetAvailableActuators(DriveType drive, Character character)
    {
        return _resolvers.SelectMany(r => r.GetAvailableActuators(drive, character));
    }
}
