namespace NPC.Library.Messaging;

using NPC.Library.Character;

/// <summary>
/// Fired when a character's target drive changes, useful for logging or UI.
/// </summary>
public class TargetDriveChangedMessage : IMessage
{
    public Character Character { get; }
    public DriveType NewDrive { get; }

    public TargetDriveChangedMessage(Character character, DriveType newDrive)
    {
        Character = character;
        NewDrive = newDrive;
    }
}

/// <summary>
/// Fired when a character dies.
/// </summary>
public class CharacterDiedMessage : IMessage
{
    public Character Character { get; }
    public string Reason { get; }

    public CharacterDiedMessage(Character character, string reason)
    {
        Character = character;
        Reason = reason;
    }
}

/// <summary>
/// Fired when the state machine switches to a new actuator type.
/// </summary>
public class ActuatorChangedMessage : IMessage
{
    public Character Character { get; }
    public string OldActuatorName { get; }
    public string NewActuatorName { get; }

    public ActuatorChangedMessage(Character character, string oldActuatorName, string newActuatorName)
    {
        Character = character;
        OldActuatorName = oldActuatorName;
        NewActuatorName = newActuatorName;
    }
}

/// <summary>
/// Fired when the LLM generates a justification for an action change.
/// </summary>
public class LLMReasoningGeneratedMessage : IMessage
{
    public Character Character { get; }
    public string Action { get; }
    public string Reason { get; }

    public LLMReasoningGeneratedMessage(Character character, string action, string reason)
    {
        Character = character;
        Action = action;
        Reason = reason;
    }
}

/// <summary>
/// Fired when a character approaches another character and initiates a conversation.
/// </summary>
public class CharacterSocializingMessage : IMessage
{
    public Character Character { get; }
    public Character Target { get; }

    public CharacterSocializingMessage(Character character, Character target)
    {
        Character = character;
        Target = target;
    }
}

/// <summary>
/// Fired when the LLM generates dialogue for a character to speak.
/// </summary>
public class DialogueGeneratedMessage : IMessage
{
    public Character Character { get; }
    public string Dialogue { get; }

    public DialogueGeneratedMessage(Character character, string dialogue)
    {
        Character = character;
        Dialogue = dialogue;
    }
}
