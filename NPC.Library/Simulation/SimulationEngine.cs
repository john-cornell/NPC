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
    /// Multiplier for simulation time. 1.0 = real-time.
    /// </summary>
    public decimal TimeScaleMultiplier { get; set; } = 1.0m;

    /// <summary>
    /// Duration of a single tick in real-world time. Used to calculate decay accurately.
    /// </summary>
    public TimeSpan TickDuration { get; set; } = TimeSpan.FromSeconds(1);

    // Decay rates per simulation second (1.0m / total seconds)
    private const decimal SatietyDecayPerSecond = 1.0m / (5m * 24m * 60m * 60m); // 5 days
    private const decimal ThirstDecayPerSecond = 1.0m / (2m * 24m * 60m * 60m);  // 2 days
    private const decimal FatigueDecayPerSecond = 1.0m / (16m * 60m * 60m); // 16 hours to max fatigue
    private const decimal FatigueRecoveryPerSecond = 1.0m / (8m * 60m * 60m); // 8 hours to fully recover

    private const decimal SocialDecayPerSecond = 1.0m / (4m * 24m * 60m * 60m);  // 4 days

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
        // Initialize character fatigue based on time of day (awake since 06:00)
        var timeOfDay = _spatialContext.CurrentTime.TimeOfDay;
        double hoursAwake = timeOfDay.TotalHours - 6.0;
        
        // If it's before 6 AM, calculate as if they stayed up all night, or just highly fatigued
        if (hoursAwake < 0) hoursAwake += 24.0;
        
        decimal initialFatigue = Math.Clamp((decimal)(hoursAwake / 16.0), 0.0m, 1.0m);
        character.Drives.SetLevel(NPC.Library.Character.DriveType.Fatigue, initialFatigue);

        lock (_characters)
        {
            if (!_characters.Contains(character))
            {
                _characters.Add(character);
            }
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
        if (tickInterval <= TimeSpan.Zero)
        {
            while (!token.IsCancellationRequested)
            {
                await TickOnceAsync();
                await Task.Yield();
            }
            return;
        }

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
        decimal tickSeconds = (decimal)TickDuration.TotalSeconds * TimeScaleMultiplier;
        decimal satietyDecay = SatietyDecayPerSecond * tickSeconds;
        decimal thirstDecay = ThirstDecayPerSecond * tickSeconds;
        
        foreach (var character in characters)
        {
            if (character.IsDead) continue;

            if (character.Drives.TryGetLevel(DriveType.Satiety, out var satiety) && satiety <= 0m)
            {
                Die(character, "Starvation");
                continue;
            }

            if (character.Drives.TryGetLevel(DriveType.Thirst, out var thirst) && thirst <= 0m)
            {
                Die(character, "Dehydration");
                continue;
            }

            // Apply normal decay
            if (character.Drives.TryGetLevel(DriveType.Satiety, out var currentSatiety))
            {
                var newSatiety = Math.Max(0m, currentSatiety - satietyDecay);
                character.Drives.SetLevel(DriveType.Satiety, newSatiety);
                if (newSatiety <= 0m) Die(character, "Starvation");
            }
            
            // Thirst goes down over time
            if (!character.IsDead && character.Drives.TryGetLevel(DriveType.Thirst, out var currentThirst))
            {
                var newThirst = Math.Max(0m, currentThirst - thirstDecay);
                character.Drives.SetLevel(DriveType.Thirst, newThirst);
                if (newThirst <= 0m) Die(character, "Dehydration");
            }

            // Fatigue calculation (up if awake, down if asleep)
            if (!character.IsDead && character.Drives.TryGetLevel(DriveType.Fatigue, out var currentFatigue))
            {
                bool isSleeping = character.LastAction != null && (character.LastAction.Contains("Sleep") || character.LastAction.Contains("Rest"));

                if (isSleeping)
                {
                    decimal fatigueRecovery = FatigueRecoveryPerSecond * tickSeconds;
                    var newFatigue = Math.Max(0.0m, currentFatigue - fatigueRecovery);
                    character.Drives.SetLevel(DriveType.Fatigue, newFatigue);
                    
                    if (newFatigue <= 0) character.HasBadSleepModifier = false;
                }
                else
                {
                    // Modifier if they had a bad sleep
                    decimal fatigueModifier = character.HasBadSleepModifier ? 2.0m : 1.0m;
                    decimal fatigueDecay = FatigueDecayPerSecond * tickSeconds * fatigueModifier;

                    var newFatigue = Math.Min(1.0m, currentFatigue + fatigueDecay);
                    character.Drives.SetLevel(DriveType.Fatigue, newFatigue);
                    
                    if (newFatigue >= 1.0m) 
                    {
                        if (Random.Shared.NextDouble() < 0.05)
                        {
                            character.HasBadSleepModifier = true;
                        }
                    }
                }
            }

            // Social goes DOWN over time
            if (!character.IsDead && character.Drives.TryGetLevel(DriveType.Social, out var currentSocial))
            {
                var newSocial = Math.Max(0m, currentSocial - (SocialDecayPerSecond * tickSeconds));
                character.Drives.SetLevel(DriveType.Social, newSocial);
            }

            if (character is NPC.Library.Character.Animal animal)
            {
                animal.UpdateDecay(tickSeconds);
            }
        }
        
        // Tick world resources
        _spatialContext.TickEnvironment();
    }

    private void Die(Character character, string reason)
    {
        character.IsDead = true;
        character.DeathReason = reason;
        character.DeathTick = (int)_tickCount;
        _dispatcher?.DispatchImmediate(new CharacterDiedMessage(character, reason));
    }

    private async Task SafeTickCharacterAsync(Character character)
    {
        if (character.IsDead) return;
        
        try
        {
            if (character.TryGetComponent<NPC.Library.Behaviors.Player.IPlayerController>(out var playerController))
            {
                await playerController.TickAsync(character);
                return;
            }

            if (character.TryGetComponent<NPC.Library.Behaviors.AI.IAIController>(out var aiController))
            {
                await aiController.TickAsync(character);
                return;
            }

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
