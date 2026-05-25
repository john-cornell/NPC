namespace NPC.Library.Character;

/// <summary>
/// Runtime state for one NPC. Create via <see cref="CharacterFactory"/>.
/// </summary>
public sealed class Character
{
    private readonly NPC.Library.Messaging.MessageDispatcher? _dispatcher;

    internal Character(Drives drives, NPC.Library.Messaging.MessageDispatcher? dispatcher = null) 
    {
        Drives = drives;
        _dispatcher = dispatcher;
    }

    private readonly System.Collections.Generic.Dictionary<System.Type, object> _components = new();

    public Drives Drives { get; }
    
    public string Name { get; set; } = "NPC";
    public bool IsDead { get; set; } = false;
    public string DeathReason { get; set; } = string.Empty;
    public int DeathTick { get; set; } = 0;
    public string LastAction { get; set; } = "None";
    public (int X, int Y)? CurrentDestination { get; set; }
    
    private DriveType _targetDrive = DriveType.Idle;
    public DriveType TargetDrive 
    { 
        get => _targetDrive; 
        set
        {
            if (_targetDrive != value)
            {
                _targetDrive = value;
                _dispatcher?.DispatchImmediate(new NPC.Library.Messaging.TargetDriveChangedMessage(this, _targetDrive));
            }
        }
    }

    public NPC.Library.State.IActuator? ActiveActuator { get; set; }

    public void AddComponent<T>(T component) where T : class
    {
        _components[typeof(T)] = component;
    }

    public T? GetComponent<T>() where T : class
    {
        if (_components.TryGetValue(typeof(T), out var component))
        {
            return component as T;
        }
        return null;
    }

    public bool TryGetComponent<T>(out T component) where T : class
    {
        component = GetComponent<T>()!;
        return component != null;
    }

    public void RemoveComponent<T>() where T : class
    {
        _components.Remove(typeof(T));
    }
}
