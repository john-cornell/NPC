namespace NPC.Library.Simulation;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NPC.Library.Character;
using NPC.Library.Messaging;

using DriveType = NPC.Library.Character.DriveType;


/// <summary>
/// Diagnostic logger for genetic training. Attach to a generation's dispatcher
/// to capture per-character action distributions, drive transitions, and death context.
/// Writes a detailed summary file when disposed.
/// </summary>
public class GeneticTrainingLogger : IDisposable
{
    private readonly MessageDispatcher _dispatcher;
    private readonly string _outputPath;
    private readonly int _generation;

    // Per-character tracking
    private readonly Dictionary<Character, List<string>> _actionLog = new();
    private readonly Dictionary<Character, Dictionary<string, int>> _actionCounts = new();
    private readonly Dictionary<Character, List<(long Tick, DriveType Drive)>> _driveChanges = new();
    private readonly Dictionary<Character, (string Reason, int Tick)> _deaths = new();
    private readonly Dictionary<Character, List<(long Tick, string Snapshot)>> _driveSnapshots = new();

    private long _currentTick = 0;
    private int _snapshotInterval;

    public GeneticTrainingLogger(MessageDispatcher dispatcher, string outputDir, int generation, int snapshotInterval = 50)
    {
        _dispatcher = dispatcher;
        _generation = generation;
        _snapshotInterval = snapshotInterval;

        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);
        _outputPath = Path.Combine(outputDir, $"gen_{generation:D5}_diagnostic.log");

        _dispatcher.Subscribe<ActuatorChangedMessage>(OnActuatorChanged);
        _dispatcher.Subscribe<TargetDriveChangedMessage>(OnDriveChanged);
        _dispatcher.Subscribe<CharacterDiedMessage>(OnCharacterDied);
        _dispatcher.Subscribe<CharacterSocializingMessage>(OnSocializing);
    }

    public void UpdateTick(long tick)
    {
        _currentTick = tick;
    }

    /// <summary>
    /// Call each tick to snapshot drive levels at regular intervals.
    /// </summary>
    public void SnapshotDrives(IEnumerable<Character> characters)
    {
        if (_currentTick % _snapshotInterval != 0) return;

        foreach (var c in characters)
        {
            if (c.IsDead) continue;
            if (!_driveSnapshots.ContainsKey(c))
                _driveSnapshots[c] = new();

            var snap = FormatDrives(c);
            _driveSnapshots[c].Add((_currentTick, snap));
        }
    }

    private void OnActuatorChanged(ActuatorChangedMessage msg)
    {
        var c = msg.Character;
        EnsureCharacter(c);

        _actionLog[c].Add($"[T{_currentTick:D4}] {msg.OldActuatorName} -> {msg.NewActuatorName} (Drive={c.TargetDrive})");

        if (!_actionCounts[c].ContainsKey(msg.NewActuatorName))
            _actionCounts[c][msg.NewActuatorName] = 0;
        _actionCounts[c][msg.NewActuatorName]++;
    }

    private void OnDriveChanged(TargetDriveChangedMessage msg)
    {
        var c = msg.Character;
        EnsureCharacter(c);
        _driveChanges[c].Add((_currentTick, msg.NewDrive));
    }

    private void OnCharacterDied(CharacterDiedMessage msg)
    {
        var c = msg.Character;
        EnsureCharacter(c);
        _deaths[c] = (msg.Reason, (int)_currentTick);

        // Record final drive snapshot at death
        if (!_driveSnapshots.ContainsKey(c))
            _driveSnapshots[c] = new();
        _driveSnapshots[c].Add((_currentTick, $"DEATH({msg.Reason}) {FormatDrives(c)}"));
    }

    private void OnSocializing(CharacterSocializingMessage msg)
    {
        var c = msg.Character;
        EnsureCharacter(c);

        if (!_actionCounts[c].ContainsKey("Socializing"))
            _actionCounts[c]["Socializing"] = 0;
        _actionCounts[c]["Socializing"]++;
    }

    private void EnsureCharacter(Character c)
    {
        if (!_actionLog.ContainsKey(c)) _actionLog[c] = new();
        if (!_actionCounts.ContainsKey(c)) _actionCounts[c] = new();
        if (!_driveChanges.ContainsKey(c)) _driveChanges[c] = new();
    }

    private static string FormatDrives(Character c)
    {
        var parts = new List<string>();
        if (c.Drives.TryGetLevel(DriveType.Satiety, out var sat)) parts.Add($"Sat={sat:F3}");
        if (c.Drives.TryGetLevel(DriveType.Thirst, out var thi)) parts.Add($"Thi={thi:F3}");
        if (c.Drives.TryGetLevel(DriveType.Fatigue, out var fat)) parts.Add($"Fat={fat:F3}");
        if (c.Drives.TryGetLevel(DriveType.Social, out var soc)) parts.Add($"Soc={soc:F3}");
        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Write the full diagnostic report to disk.
    /// </summary>
    public void WriteSummary(IReadOnlyList<Character> characters, IReadOnlyList<(NPC.Library.Decision.NeuralNetwork Brain, float Fitness)> evaluated)
    {
        using var writer = new StreamWriter(_outputPath);

        writer.WriteLine($"=== GENETIC TRAINING DIAGNOSTIC — Generation {_generation} ===");
        writer.WriteLine($"Population: {characters.Count} | Final Tick: {_currentTick}");
        writer.WriteLine();

        // Overall summary
        var dead = characters.Where(c => c.IsDead).ToList();
        var alive = characters.Where(c => !c.IsDead).ToList();
        writer.WriteLine($"Dead: {dead.Count} | Survived: {alive.Count}");
        if (dead.Count > 0)
        {
            var byReason = dead.GroupBy(c => c.DeathReason).OrderByDescending(g => g.Count());
            foreach (var g in byReason)
                writer.WriteLine($"  {g.Key}: {g.Count()} (ticks: {g.Min(c => c.DeathTick)}-{g.Max(c => c.DeathTick)}, avg={g.Average(c => c.DeathTick):F0})");
        }
        writer.WriteLine();

        // Fitness distribution
        var fitnesses = evaluated.Select(e => e.Fitness).OrderByDescending(f => f).ToList();
        writer.WriteLine($"Fitness — Min: {fitnesses.Min():F1} | Max: {fitnesses.Max():F1} | Avg: {fitnesses.Average():F1} | StdDev: {StdDev(fitnesses):F1}");
        writer.WriteLine($"  Spread (Max-Min): {fitnesses.Max() - fitnesses.Min():F1}");
        writer.WriteLine();

        // Per-character detail
        writer.WriteLine("=== PER-CHARACTER DETAIL ===");
        for (int i = 0; i < characters.Count; i++)
        {
            var c = characters[i];
            var fitness = evaluated.FirstOrDefault(e => ReferenceEquals(e.Brain, c.GetComponent<NPC.Library.Decision.NeuralNetwork>()));

            writer.WriteLine($"\n--- {c.Name} (Fitness: {fitness.Fitness:F1}) ---");
            writer.WriteLine($"  Status: {(c.IsDead ? $"DEAD at tick {c.DeathTick} ({c.DeathReason})" : "ALIVE")}");
            writer.WriteLine($"  Final Drives: {FormatDrives(c)}");
            writer.WriteLine($"  Last Action: {c.LastAction}");
            writer.WriteLine($"  Target Drive: {c.TargetDrive}");

            // Action distribution
            if (_actionCounts.TryGetValue(c, out var counts) && counts.Count > 0)
            {
                writer.WriteLine($"  Action Distribution:");
                int totalActions = counts.Values.Sum();
                foreach (var kv in counts.OrderByDescending(x => x.Value))
                    writer.WriteLine($"    {kv.Key}: {kv.Value} ({100.0 * kv.Value / totalActions:F0}%)");
            }
            else
            {
                writer.WriteLine($"  Action Distribution: NO ACTIONS RECORDED");
            }

            // Drive change timeline (first 20 and last 10)
            if (_driveChanges.TryGetValue(c, out var changes) && changes.Count > 0)
            {
                writer.WriteLine($"  Drive Changes ({changes.Count} total):");
                var toShow = changes.Take(20).ToList();
                foreach (var (tick, drive) in toShow)
                    writer.WriteLine($"    T{tick:D4}: -> {drive}");
                if (changes.Count > 30)
                    writer.WriteLine($"    ... ({changes.Count - 30} more) ...");
                if (changes.Count > 20)
                {
                    foreach (var (tick, drive) in changes.Skip(Math.Max(20, changes.Count - 10)))
                        writer.WriteLine($"    T{tick:D4}: -> {drive}");
                }
            }

            // Drive snapshots
            if (_driveSnapshots.TryGetValue(c, out var snapshots) && snapshots.Count > 0)
            {
                writer.WriteLine($"  Drive Snapshots:");
                foreach (var (tick, snap) in snapshots)
                    writer.WriteLine($"    T{tick:D4}: {snap}");
            }
        }

        // NN output analysis — check if all NNs produce similar outputs for the same input
        writer.WriteLine("\n=== NEURAL NETWORK DIVERGENCE CHECK ===");
        writer.WriteLine("Testing with uniform input [0.5, 0.5, 0.5, 0.5, 1.0, 1.0, 1.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]:");
        var testInput = new float[] { 0.5f, 0.5f, 0.5f, 0.5f, 1.0f, 1.0f, 1.0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
        var outputs = new List<float[]>();
        foreach (var (brain, _) in evaluated)
        {
            var output = brain.FeedForward(testInput);
            outputs.Add(output);
        }

        // Show first 3 and variance
        for (int i = 0; i < Math.Min(3, outputs.Count); i++)
        {
            writer.WriteLine($"  NN[{i}]: [{string.Join(", ", outputs[i].Select(v => v.ToString("F3")))}]");
        }

        // Per-output variance
        if (outputs.Count > 1 && outputs[0].Length > 0)
        {
            writer.WriteLine($"  Per-output variance across population:");
            string[] actuatorNames = { "Wander", "SearchFood", "GatherFood", "Eat", "Rest/Sleep", "GatherWater", "Drink", "Socialize", "StoreItem", "RetrieveItem", "Fallback" };
            for (int j = 0; j < outputs[0].Length && j < actuatorNames.Length; j++)
            {
                var vals = outputs.Select(o => o[j]).ToList();
                writer.WriteLine($"    [{j}] {actuatorNames[j],-14}: mean={vals.Average():F3} var={Variance(vals):F5} range=[{vals.Min():F3}, {vals.Max():F3}]");
            }
        }

        writer.Flush();
        Console.WriteLine($"[GeneticLog] Diagnostic written to: {_outputPath}");
    }

    private static float StdDev(List<float> values)
    {
        float avg = values.Average();
        return (float)Math.Sqrt(values.Average(v => (v - avg) * (v - avg)));
    }

    private static float Variance(List<float> values)
    {
        float avg = values.Average();
        return values.Average(v => (v - avg) * (v - avg));
    }

    public void Dispose()
    {
        _dispatcher.Unsubscribe<ActuatorChangedMessage>(OnActuatorChanged);
        _dispatcher.Unsubscribe<TargetDriveChangedMessage>(OnDriveChanged);
        _dispatcher.Unsubscribe<CharacterDiedMessage>(OnCharacterDied);
        _dispatcher.Unsubscribe<CharacterSocializingMessage>(OnSocializing);
    }
}
