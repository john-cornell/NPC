using System;
using System.Linq;
using System.Threading.Tasks;
using NPC.Application;
using NPC.Library.Simulation;
using NPC.Library.Spatial.Grid;
using Spectre.Console;

namespace NPC.UI.Console
{
    class Program
    {
        static async Task Main(string[] args)
        {
            System.Console.CursorVisible = false;
            System.Console.Clear();
            
            int selectedIndex = 0;
            string[] options = new[] {
                "1. Test Scenario (Survival of the Fittest - 800x800 open world)",
                "2. Village Scenario (NPC village with houses and roads - 200x100)",
                "3. Run Genetic Trainer (Headless Evolution Loop)"
            };

            while (true)
            {
                System.Console.SetCursorPosition(0, 0);
                AnsiConsole.MarkupLine("[bold]Which simulation would you like to run?[/]\n");
                for (int i = 0; i < options.Length; i++)
                {
                    if (i == selectedIndex)
                    {
                        AnsiConsole.MarkupLine($"[cyan]> {options[i]}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"  {options[i]}");
                    }
                }

                var key = System.Console.ReadKey(true);
                if (key.Key == ConsoleKey.UpArrow)
                {
                    selectedIndex--;
                    if (selectedIndex < 0) selectedIndex = options.Length - 1;
                }
                else if (key.Key == ConsoleKey.DownArrow)
                {
                    selectedIndex++;
                    if (selectedIndex >= options.Length) selectedIndex = 0;
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    break;
                }
                else if (key.Key == ConsoleKey.D1 || key.Key == ConsoleKey.NumPad1)
                {
                    selectedIndex = 0;
                    break;
                }
                else if (key.Key == ConsoleKey.D2 || key.Key == ConsoleKey.NumPad2)
                {
                    selectedIndex = 1;
                    break;
                }
                else if (key.Key == ConsoleKey.D3 || key.Key == ConsoleKey.NumPad3)
                {
                    selectedIndex = 2;
                    break;
                }
            }
            System.Console.Clear();

            ScenarioContext context;
            if (selectedIndex == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Generating Test World Map...[/]");
                context = ScenarioRunner.SetupTestScenario();
                var renderer = new SpectreRenderer();
                await RunSimulationLoop(context.Engine, context.InitialState, renderer);
            }
            else if (selectedIndex == 1)
            {
                AnsiConsole.MarkupLine("[yellow]Generating Village Map...[/]");
                context = ScenarioRunner.SetupVillageScenario();
                var renderer = new SpectreRenderer();
                await RunSimulationLoop(context.Engine, context.InitialState, renderer);
            }
            else
            {
                System.Console.CursorVisible = true;
                AnsiConsole.MarkupLine("[yellow]Starting Genetic Trainer...[/]");
                await GeneticTrainer.RunTrainingAsync(generations: 50, ticksPerGeneration: 1000, populationSize: 16);
                
                System.Console.WriteLine("Press any key to exit...");
                System.Console.ReadKey();
                return;
            }
        }

        static async Task RunSimulationLoop(SimulationEngine engine, UIState state, SpectreRenderer renderer)
        {
            System.Console.Clear();

            await AnsiConsole.Live(renderer.Build(state))
                .StartAsync(async ctx => 
                {
                    engine.OnTickComplete += (sender, e) =>
                    {
                        state.TickCount = e.TickCount;

                        if (state.SelectedCharacter != null && state.SpatialContext is GridSpatialContext gridCtx)
                        {
                            var pos = gridCtx.GetCharacterLocation(state.SelectedCharacter);
                            if (pos.HasValue)
                            {
                                int cx = pos.Value.X;
                                int cy = pos.Value.Y;
                                if (cx < state.CameraX || cx >= state.CameraX + state.CameraWidth ||
                                    cy < state.CameraY || cy >= state.CameraY + state.CameraHeight)
                                {
                                    state.CameraX = Math.Max(-1, Math.Min(cx - state.CameraWidth / 2, gridCtx.Map.Width - state.CameraWidth + 1));
                                    state.CameraY = Math.Max(-1, Math.Min(cy - state.CameraHeight / 2, gridCtx.Map.Height - state.CameraHeight + 1));
                                }
                            }
                        }

                        ctx.UpdateTarget(renderer.Build(state));
                        ctx.Refresh();
                    };

                    engine.Start(TimeSpan.FromSeconds(1));
                    
                    bool running = true;
                    while (running)
                    {
                        if (System.Console.KeyAvailable)
                        {
                            var key = System.Console.ReadKey(intercept: true);
                            
                            if (key.Key == ConsoleKey.Escape)
                            {
                                running = false;
                            }
                            else if (key.Key == ConsoleKey.V)
                            {
                                state.ViewMode = state.ViewMode == 0 ? 1 : 0;
                                ctx.UpdateTarget(renderer.Build(state));
                                ctx.Refresh();
                            }
                            else if (key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.RightArrow)
                            {
                                var chars = state.SpatialContext.GetCharacters().ToList();
                                int idx = state.SelectedCharacter != null ? chars.IndexOf(state.SelectedCharacter) : 0;
                                
                                if (key.Key == ConsoleKey.LeftArrow)
                                    idx = (idx - 1 + chars.Count) % chars.Count;
                                else
                                    idx = (idx + 1) % chars.Count;
                                    
                                state.SelectedCharacter = chars[idx];

                                if (state.SpatialContext is GridSpatialContext gridCtx)
                                {
                                    var pos = gridCtx.GetCharacterLocation(state.SelectedCharacter);
                                    if (pos.HasValue)
                                    {
                                        int cx = pos.Value.X;
                                        int cy = pos.Value.Y;
                                        if (cx < state.CameraX || cx >= state.CameraX + state.CameraWidth ||
                                            cy < state.CameraY || cy >= state.CameraY + state.CameraHeight)
                                        {
                                            state.CameraX = Math.Max(0, Math.Min(cx - state.CameraWidth / 2, gridCtx.Map.Width - state.CameraWidth));
                                            state.CameraY = Math.Max(0, Math.Min(cy - state.CameraHeight / 2, gridCtx.Map.Height - state.CameraHeight));
                                        }
                                    }
                                }

                                ctx.UpdateTarget(renderer.Build(state));
                                ctx.Refresh();
                            }
                            else if (key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.DownArrow)
                            {
                                if (state.SpatialContext is GridSpatialContext gridCtx)
                                {
                                    var trees = gridCtx.Map.TreeApples.Keys.ToList();
                                    if (trees.Count > 0)
                                    {
                                        int idx = state.SelectedTree != null ? trees.IndexOf(state.SelectedTree.Value) : 0;
                                        if (idx == -1) idx = 0;

                                        if (key.Key == ConsoleKey.UpArrow)
                                            idx = (idx - 1 + trees.Count) % trees.Count;
                                        else
                                            idx = (idx + 1) % trees.Count;

                                        state.SelectedTree = trees[idx];
                                        ctx.UpdateTarget(renderer.Build(state));
                                        ctx.Refresh();
                                    }
                                }
                            }
                            else
                            {
                                // Camera panning using QWE, A D, ZXC
                                char c = key.KeyChar;
                                int step = char.IsUpper(c) ? 10 : 1;
                                char lower = char.ToLower(c);

                                if ("qweadzxc".Contains(lower))
                                {
                                    if (lower == 'w' || lower == 'q' || lower == 'e') state.CameraY -= step;
                                    if (lower == 'x' || lower == 'z' || lower == 'c') state.CameraY += step;
                                    if (lower == 'a' || lower == 'q' || lower == 'z') state.CameraX -= step;
                                    if (lower == 'd' || lower == 'e' || lower == 'c') state.CameraX += step;
                                    
                                    // Clamp to map boundaries!
                                    if (state.SpatialContext is GridSpatialContext gridCtx)
                                    {
                                        state.CameraX = Math.Max(-1, Math.Min(state.CameraX, gridCtx.Map.Width - state.CameraWidth + 1));
                                        state.CameraY = Math.Max(-1, Math.Min(state.CameraY, gridCtx.Map.Height - state.CameraHeight + 1));
                                    }

                                    ctx.UpdateTarget(renderer.Build(state));
                                    ctx.Refresh();
                                }
                            }
                        }
                        await Task.Delay(50);
                    }

                    await engine.StopAsync();
                });

            engine.Dispose();
            System.Console.CursorVisible = true;
            AnsiConsole.MarkupLine("\n[yellow]Simulation stopped.[/]");
        }
    }
}
