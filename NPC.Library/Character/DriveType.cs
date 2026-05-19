namespace NPC.Library.Character;

/// <summary>
/// Built-in innate drives and bodily levels.
/// Most values are pressures (higher = stronger urge). <see cref="Satiety"/> is a reserve (higher = more fueled).
/// </summary>
public enum DriveType
{
    /// <summary>How satisfied/fueled the body is (from food/rest). Higher = more energy available to spend; lower = need sustenance.</summary>
    Satiety,

    /// <summary>Need for water. Higher = thirstier.</summary>
    Thirst,

    /// <summary>Wear from activity (running, labor, stress). Higher = more exhausted; distinct from low <see cref="Satiety"/>.</summary>
    Fatigue,

    /// <summary>Need for interaction. Higher = lonelier.</summary>
    Social,

    /// <summary>Need for security. Higher = more threatened.</summary>
    Safety,

    /// <summary>Need for pleasant conditions. Higher = more uncomfortable.</summary>
    Comfort,
}
