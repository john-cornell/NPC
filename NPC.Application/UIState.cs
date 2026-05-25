namespace NPC.Application;

using System.Collections.Generic;
using NPC.Library.Character;
using NPC.Library.Spatial;

public class UIState
{
    public ISpatialContext SpatialContext { get; set; } = null!;
    public long TickCount { get; set; }
    
    // View modes: 0 = Table View, 1 = Map View
    public int ViewMode { get; set; } = 0;
    
    // Map View Camera
    public int CameraX { get; set; } = 0;
    public int CameraY { get; set; } = 0;
    public int CameraWidth { get; set; } = 80;
    public int CameraHeight { get; set; } = 40;

    // Track max UI sizes to prevent jitter
    public int MaxInfoHeight { get; set; } = 0;
    public int MaxInfoWidth { get; set; } = 0;
    public int MaxCharsHeight { get; set; } = 0;
    public int MaxCharsWidth { get; set; } = 0;
    
    // For Map View: track selected character
    public Character? SelectedCharacter { get; set; }
    public (int X, int Y)? SelectedTree { get; set; }

    // Global LLM configuration for characters without a specific override
    public NPC.LLM.LLMConfig GlobalLLMConfig { get; set; } = new NPC.LLM.LLMConfig();

    // Map features
    public (int X, int Y)? WellLocation { get; set; }

    // Training History
    public List<float> AverageFitnessHistory { get; set; } = new();
    public List<float> BestFitnessHistory { get; set; } = new();
    public List<float> DeathCountHistory { get; set; } = new();
    public List<float> DehydrationDeathHistory { get; set; } = new();
    public List<float> StarvationDeathHistory { get; set; } = new();
    public List<float> ExhaustionDeathHistory { get; set; } = new();
    public List<float> SurvivedCountHistory { get; set; } = new();
    public List<float> EarliestDeathHistory { get; set; } = new();
    public List<float> LatestDeathHistory { get; set; } = new();

    // Resource Tracking History
    public List<float> MaxApplesCollectedHistory { get; set; } = new();
    public List<float> AvgApplesCollectedHistory { get; set; } = new();
    public List<float> MaxApplesEatenHistory { get; set; } = new();
    public List<float> AvgApplesEatenHistory { get; set; } = new();
    public List<float> MaxWaterCollectedHistory { get; set; } = new();
    public List<float> AvgWaterCollectedHistory { get; set; } = new();
    public List<float> MaxSipsTakenHistory { get; set; } = new();
    public List<float> AvgSipsTakenHistory { get; set; } = new();

    public List<NPC.Library.Decision.NeuralNetwork> CurrentPopulation { get; set; } = new();

    // Speed Control
    public bool SlowMode { get; set; } = false;
    public bool SlowModeChanged { get; set; } = false;
}
