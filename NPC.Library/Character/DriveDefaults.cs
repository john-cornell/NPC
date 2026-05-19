namespace NPC.Library.Character;

/// <summary>
/// Universal drives and baseline starting levels for a typical character.
/// Add new <see cref="DriveType"/> values to the enum freely; only drives listed in
/// <see cref="Universal"/> are initialized on every character until you opt in elsewhere.
/// </summary>
public static class DriveDefaults
{
    /// <summary>Drives every standard character is expected to have.</summary>
    public static IReadOnlyList<DriveType> Universal { get; } =
    [
        DriveType.Satiety,
        DriveType.Thirst,
        DriveType.Fatigue,
        DriveType.Social,
        DriveType.Safety,
        DriveType.Comfort,
    ];

    /// <summary>Starting levels for <see cref="Universal"/> drives (healthy, rested baseline).</summary>
    public static IReadOnlyDictionary<DriveType, decimal> BaselineLevels { get; } =
        new Dictionary<DriveType, decimal>
        {
            [DriveType.Satiety] = 1.0m,
            [DriveType.Thirst] = 0m,
            [DriveType.Fatigue] = 0m,
            [DriveType.Social] = 0m,
            [DriveType.Safety] = 0m,
            [DriveType.Comfort] = 0m,
        };

    /// <summary>Creates drives with all universal types set to <see cref="BaselineLevels"/>.</summary>
    public static Drives CreateBaseline() => new(BaselineLevels);

    /// <summary>Baseline drives with per-drive overrides applied on top.</summary>
    public static Drives CreateBaseline(IEnumerable<KeyValuePair<DriveType, decimal>> overrides)
    {
        var drives = CreateBaseline();
        foreach (var (drive, level) in overrides)
            drives.SetLevel(drive, level);
        return drives;
    }
}
