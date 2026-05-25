namespace NPC.Library.Character;

/// <summary>
/// Built-in innate drives and bodily levels.
/// Most values are pressures (higher = stronger urge). <see cref="Satiety"/> is a reserve (higher = more fueled).
/// </summary>
public enum DriveType
{
    /// <summary>How satisfied/fueled the body is (from food). Higher = more energy available.</summary>
    Satiety,

    /// <summary>Need for water. Higher = thirstier.</summary>
    Thirst,

    /// <summary>Need for rest. Higher = more tired.</summary>
    Fatigue,

    /// <summary>No pressing biological needs. Higher = more bored/restless.</summary>
    Idle,

    /// <summary>Need for social interaction. Lower = more lonely. Higher = more fulfilled.</summary>
    Social
}
