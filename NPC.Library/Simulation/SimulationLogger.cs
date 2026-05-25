namespace NPC.Library.Simulation;

using System;
using System.Collections.Generic;
using System.IO;
using NPC.Library.Character;
using NPC.Library.Messaging;

public class SimulationLogger : IDisposable
{
    private readonly string _logDir;
    private readonly MessageDispatcher _dispatcher;
    private readonly Dictionary<Character, StreamWriter> _writers = new();
    private long _currentTick = 0;

    public SimulationLogger(string logDir, MessageDispatcher dispatcher)
    {
        _logDir = logDir;
        _dispatcher = dispatcher;
        
        if (!Directory.Exists(_logDir))
        {
            Directory.CreateDirectory(_logDir);
        }

        // Subscribe to relevant messages
        _dispatcher.Subscribe<TargetDriveChangedMessage>(OnTargetDriveChanged);
        _dispatcher.Subscribe<CharacterDiedMessage>(OnCharacterDied);
        _dispatcher.Subscribe<ActuatorChangedMessage>(OnActuatorChanged);
        _dispatcher.Subscribe<LLMReasoningGeneratedMessage>(OnLLMReasoningGenerated);
        _dispatcher.Subscribe<DialogueGeneratedMessage>(OnDialogueGenerated);
    }

    public void UpdateTick(long tick)
    {
        _currentTick = tick;
    }

    private StreamWriter GetWriter(Character character)
    {
        if (!_writers.TryGetValue(character, out var writer))
        {
            string safeName = string.Join("_", character.Name.Split(Path.GetInvalidFileNameChars()));
            string path = Path.Combine(_logDir, $"{safeName}.log");
            writer = new StreamWriter(path, append: true) { AutoFlush = true };
            _writers[character] = writer;
        }
        return writer;
    }

    private void Log(Character character, string message)
    {
        var writer = GetWriter(character);
        writer.WriteLine($"[Tick {_currentTick:D5}] {message}");
    }

    private void OnTargetDriveChanged(TargetDriveChangedMessage msg)
    {
        Log(msg.Character, $"Drive changed to {msg.NewDrive}");
    }

    private void OnActuatorChanged(ActuatorChangedMessage msg)
    {
        Log(msg.Character, $"Action changed from {msg.OldActuatorName} to {msg.NewActuatorName}");
    }

    private void OnLLMReasoningGenerated(LLMReasoningGeneratedMessage msg)
    {
        Log(msg.Character, $"[LLM] Decided to '{msg.Action}' because: {msg.Reason}");
    }

    private void OnDialogueGenerated(DialogueGeneratedMessage msg)
    {
        Log(msg.Character, $"[Dialogue] \"{msg.Dialogue}\"");
    }

    private void OnCharacterDied(CharacterDiedMessage msg)
    {
        Log(msg.Character, $"Character DIED! Reason: {msg.Reason}");
    }

    public void Dispose()
    {
        _dispatcher.Unsubscribe<TargetDriveChangedMessage>(OnTargetDriveChanged);
        _dispatcher.Unsubscribe<CharacterDiedMessage>(OnCharacterDied);
        _dispatcher.Unsubscribe<ActuatorChangedMessage>(OnActuatorChanged);
        _dispatcher.Unsubscribe<LLMReasoningGeneratedMessage>(OnLLMReasoningGenerated);
        _dispatcher.Unsubscribe<DialogueGeneratedMessage>(OnDialogueGenerated);
        
        foreach (var writer in _writers.Values)
        {
            writer.Dispose();
        }
        _writers.Clear();
    }
}
