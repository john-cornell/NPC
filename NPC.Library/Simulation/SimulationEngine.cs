namespace NPC.Library.Simulation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NPC.Library.Character;
using NPC.Library.State;
using NPC.Library.Spatial;
using NPC.Library.Messaging;

/// <summary>
/// The core background loop for updating character simulation sequentially or concurrently over time.
/// </summary>
public sealed class SimulationEngine : IDisposable
{
    private readonly StateMachine _stateMachine;
    private readonly ISpatialContext _spatialContext;
    private readonly MessageDispatcher? _dispatcher;
    private readonly List<Character> _characters = new();
    
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private long _tickCount;

    /// <summary>
    /// Fired whenever a full simulation tick has completed.
    /// </summary>
    public event EventHandler<SimulationTickedEventArgs>? OnTickComplete;

    /// <summary>
    /// For testing purposes, controls how much Satiety decays per second (tick).
    /// </summary>
    public decimal SatietyDecayPerTick { get; set; } = 0.025m;

    public SimulationEngine(StateMachine stateMachine, ISpatialContext spatialContext, MessageDispatcher? dispatcher = null)
    {
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _spatialContext = spatialContext ?? throw new ArgumentNullException(nameof(spatialContext));
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Adds a character to the simulation loop.
    /// </summary>
    public void AddCharacter(Character character)
    {
        lock (_characters)
        {
            _characters.Add(character);
        }
    }

    /// <summary>
    /// Removes a character from the simulation loop.
    /// </summary>
    public void RemoveCharacter(Character character)
    {
        lock (_characters)
        {
            _characters.Remove(character);
        }
    }

    public bool IsRunning => _loopTask != null && !_loopTask.IsCompleted;

    /// <summary>
    /// Starts the simulation loop using the specified interval (e.g. 1 second per tick).
    /// </summary>
    public void Start(TimeSpan tickInterval)
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunLoopAsync(tickInterval, _cts.Token), _cts.Token);
    }

    /// <summary>
    /// Stops the simulation loop.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
        }

        if (_loopTask != null)
        {
            try
            {
                await _loopTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }
    }

    private async Task RunLoopAsync(TimeSpan tickInterval, CancellationToken token)
    {
        using var timer = new PeriodicTimer(tickInterval);
        
        while (await timer.WaitForNextTickAsync(token))
        {
            _tickCount++;

            List<Character> activeRoster;
            lock (_characters)
            {
                activeRoster = _characters.ToList();
            }

            if (activeRoster.Count == 0)
            {
                OnTickComplete?.Invoke(this, new SimulationTickedEventArgs(tickInterval, _tickCount, 0));
                continue;
            }

            // 1) Apply passive simulation changes over time (dt)
            ApplyPassiveDecay(activeRoster);

            // 2) Concurrently tick each character's state machine
            var tickTasks = activeRoster.Select(c => SafeTickCharacterAsync(c)).ToArray();
            await Task.WhenAll(tickTasks);
            
            // Process any deferred messages enqueued during the tick
            _dispatcher?.ProcessQueue();

            // 3) Fire completed tick event for external listeners (e.g. UI)
            OnTickComplete?.Invoke(this, new SimulationTickedEventArgs(tickInterval, _tickCount, activeRoster.Count));
        }
    }

    /// <summary>
    /// Executes a single simulation tick synchronously or asynchronously, bypassing the periodic timer.
    /// Useful for fast-forwarding during genetic evolution training.
    /// </summary>
    public async Task TickOnceAsync()
    {
        _tickCount++;

        List<Character> activeRoster;
        lock (_characters)
        {
            activeRoster = _characters.ToList();
        }

        if (activeRoster.Count == 0)
        {
            OnTickComplete?.Invoke(this, new SimulationTickedEventArgs(TimeSpan.Zero, _tickCount, 0));
            return;
        }

        ApplyPassiveDecay(activeRoster);

        var tickTasks = activeRoster.Select(c => SafeTickCharacterAsync(c)).ToArray();
        await Task.WhenAll(tickTasks);
        
        _dispatcher?.ProcessQueue();

        OnTickComplete?.Invoke(this, new SimulationTickedEventArgs(TimeSpan.Zero, _tickCount, activeRoster.Count));
    }

    private void ApplyPassiveDecay(List<Character> characters)
    {
        foreach (var character in characters)
        {
            if (character.IsDead) continue;

            if (character.Drives.TryGetLevel(DriveType.Satiety, out var satiety) && satiety <= 0m)
            {
                character.IsDead = true;
                character.DeathReason = "Starvation";
                character.DeathTick = (int)_tickCount;
                _dispatcher?.DispatchImmediate(new CharacterDiedMessage(character, "Starvation"));
                continue;
            }

            if (character.Drives.TryGetLevel(DriveType.Thirst, out var thirst) && thirst <= 0m)
            {
                character.IsDead = true;
                character.DeathReason = "Dehydration";
                character.DeathTick = (int)_tickCount;
                _dispatcher?.DispatchImmediate(new CharacterDiedMessage(character, "Dehydration"));
                continue;
            }

            // Apply normal decay
            if (character.Drives.TryGetLevel(DriveType.Satiety, out var currentSatiety))
            {
                var newSatiety = Math.Max(0m, currentSatiety - SatietyDecayPerTick);
                character.Drives.SetLevel(DriveType.Satiety, newSatiety);
                if (newSatiety <= 0m)
                {
                    character.IsDead = true;
                    character.DeathReason = "Starvation";
                    character.DeathTick = (int)_tickCount;
                    _dispatcher?.DispatchImmediate(new CharacterDiedMessage(character, "Starvation"));
                }
            }
            
            // Thirst goes down over time (half as fast as Satiety)
            if (!character.IsDead && character.Drives.TryGetLevel(DriveType.Thirst, out var currentThirst))
            {
                var newThirst = Math.Max(0m, currentThirst - (SatietyDecayPerTick * 0.5m));
                character.Drives.SetLevel(DriveType.Thirst, newThirst);
                if (newThirst <= 0m) 
                {
                    character.IsDead = true;
                    character.DeathReason = "Dehydration";
                    character.DeathTick = (int)_tickCount;
                    _dispatcher?.DispatchImmediate(new CharacterDiedMessage(character, "Dehydration"));
                }
            }

            // Fatigue goes UP over time
            if (!character.IsDead && character.Drives.TryGetLevel(DriveType.Fatigue, out var currentFatigue))
            {
                var newFatigue = Math.Min(1.0m, currentFatigue + (SatietyDecayPerTick * 0.5m));
                character.Drives.SetLevel(DriveType.Fatigue, newFatigue);
                if (newFatigue >= 1.0m) 
                {
                    character.IsDead = true;
                    character.DeathReason = "Exhaustion";
                    character.DeathTick = (int)_tickCount;
                    _dispatcher?.DispatchImmediate(new CharacterDiedMessage(character, "Exhaustion"));
                }
            }

            // Social goes DOWN over time (slightly faster than thirst, slower than hunger)
            if (!character.IsDead && character.Drives.TryGetLevel(DriveType.Social, out var currentSocial))
            {
                var newSocial = Math.Max(0m, currentSocial - (SatietyDecayPerTick * 0.75m));
                character.Drives.SetLevel(DriveType.Social, newSocial);
            }
        }
        
        // Tick world resources
        _spatialContext.TickEnvironment();
    }

    private async Task SafeTickCharacterAsync(Character character)
    {
        if (character.IsDead) return;
        
        try
        {
            await _stateMachine.TickAsync(character);
        }
        catch (Exception)
        {
            // Log exception here depending on logging framework,
            // swallow to prevent one character crashing the entire loop.
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
