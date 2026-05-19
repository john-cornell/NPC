namespace NPC.Library.Character;

/// <summary>
/// Holds current levels for innate drives and reserves. Uses <see cref="decimal"/> for stable
/// arithmetic when levels are adjusted repeatedly over simulation ticks.
/// See <see cref="DriveType"/> for reserve vs pressure semantics.
/// </summary>
public sealed class Drives
{
    private readonly Dictionary<DriveType, decimal> _levels = new();

    public Drives()
    {
    }

    public Drives(IEnumerable<KeyValuePair<DriveType, decimal>> initialLevels)
    {
        foreach (var (drive, level) in initialLevels)
            _levels[drive] = level;
    }

    public Drives(Drives other)
    {
        foreach (var pair in other._levels)
            _levels[pair.Key] = pair.Value;
    }

    /// <summary>All drives that currently have a stored level.</summary>
    public IReadOnlyDictionary<DriveType, decimal> Levels => _levels;

    public decimal this[DriveType drive]
    {
        get => _levels.TryGetValue(drive, out var level) ? level : 0m;
        set => _levels[drive] = value;
    }

    public bool TryGetLevel(DriveType drive, out decimal level) => _levels.TryGetValue(drive, out level);

    public void SetLevel(DriveType drive, decimal level) => _levels[drive] = level;

    public void Adjust(DriveType drive, decimal delta) =>
        _levels[drive] = this[drive] + delta;

    public void Clear(DriveType drive) => _levels.Remove(drive);

    public void ClearAll() => _levels.Clear();

    public Drives Clone() => new(this);
}
