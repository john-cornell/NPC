using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using NPC.Application;
using NPC.Library.Character;
using NPC.Library.Spatial.Grid;
using DriveType = NPC.Library.Character.DriveType;

namespace NPC.UI.World
{
    public class UIOverlay
    {
        private string _savePopulationName = "";
        private Character? _characterToSelect = null;
        private System.Collections.Generic.Dictionary<Character, string> _characterTestStatuses = new();

        public void SelectCharacter(Character character)
        {
            _characterToSelect = character;
        }

        public void Render(UIState state)
        {
            if (state.PlayerCharacter != null)
            {
                RenderPlayerHUD(state);
            }

            // Set next window size and position if it's the first time
            ImGui.SetNextWindowSize(new Vector2(600, 400), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(20, 150), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(400, 300), new Vector2(float.MaxValue, float.MaxValue));
            if (ImGui.Begin("Character Management", ImGuiWindowFlags.None))
            {
                state.SelectedCharacter = null; // Reset selection each frame. If a character tab is open, it will re-assign this.

                if (ImGui.BeginTabBar("CharacterTabs"))
                {
                    // OVERVIEW TAB
                    if (ImGui.BeginTabItem("Overview"))
                    {
                        RenderOverview(state);
                        ImGui.EndTabItem();
                    }

                    // TRAINING STATS TAB
                    if (state.IsTrainerMode)
                    {
                        if (ImGui.BeginTabItem("NPC Training"))
                        {
                            RenderTrainingStats(state, "NPC", state.AverageFitnessHistory, state.BestFitnessHistory, state.DeathCountHistory, state.DehydrationDeathHistory, state.StarvationDeathHistory, state.ExhaustionDeathHistory, state.SurvivedCountHistory, state.EatenDeathHistory);
                            ImGui.EndTabItem();
                        }
                        if (ImGui.BeginTabItem("Fox Training"))
                        {
                            RenderTrainingStats(state, "Fox", state.FoxAverageFitnessHistory, state.FoxBestFitnessHistory, state.FoxDeathCountHistory, state.FoxDehydrationDeathHistory, state.FoxStarvationDeathHistory, null, null, null);
                            ImGui.EndTabItem();
                        }
                        if (ImGui.BeginTabItem("Sheep Training"))
                        {
                            RenderTrainingStats(state, "Sheep", state.SheepAverageFitnessHistory, state.SheepBestFitnessHistory, state.SheepDeathCountHistory, state.SheepDehydrationDeathHistory, state.SheepStarvationDeathHistory, null, null, state.SheepEatenDeathHistory);
                            ImGui.EndTabItem();
                        }
                        if (ImGui.BeginTabItem("Save Models"))
                        {
                            RenderSaveModels(state);
                            ImGui.EndTabItem();
                        }

                        if (ImGui.BeginTabItem("Resource Stats"))
                        {
                            RenderResourceStats(state);
                            ImGui.EndTabItem();
                        }
                    }

                    // INDIVIDUAL CHARACTER TABS
                    int cid = 1;
                    foreach (var character in state.SpatialContext.GetCharacters())
                    {
                        string statusIcon = character.IsDead ? "(DEAD) " : "";
                        string tabName = $"{statusIcon}{character.Name ?? $"NPC {cid}"}###char_{cid}";

                        ImGuiTabItemFlags flags = ImGuiTabItemFlags.None;
                        if (_characterToSelect == character)
                        {
                            flags |= ImGuiTabItemFlags.SetSelected;
                        }

                        bool dummyOpen = true;
                        if (ImGui.BeginTabItem(tabName, ref dummyOpen, flags))
                        {
                            state.SelectedCharacter = character;

                            if (_characterToSelect == character)
                            {
                                _characterToSelect = null; // Consume selection
                            }

                            RenderCharacterDetails(state, character);
                            ImGui.EndTabItem();
                        }
                        cid++;
                    }

                    ImGui.EndTabBar();
                }
            }
            ImGui.End();
        }

        private void RenderPlayerHUD(UIState state)
        {
            var p = state.PlayerCharacter!;
            
            // Draw a permanent overlay in the bottom left
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(new Vector2(10, viewport.WorkSize.Y - 200), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(250, 190), ImGuiCond.Always);
            
            ImGui.Begin("Player HUD", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoBackground);
            
            // Draw opaque background manually so it doesn't look like a standard window but is readable
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();
            drawList.AddRectFilled(pos, pos + size, ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.1f, 0.7f)), 10.0f);
            
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "PLAYER HUD");
            ImGui.Separator();
            
            ImGui.Text($"Action: {p.LastAction ?? "Idle"}");
            
            // Drives
            foreach (var drive in p.Drives.Levels)
            {
                float level = (float)drive.Value;
                Vector4 color = new Vector4(0.2f, 0.8f, 0.2f, 1.0f); // Green
                if (level < 0.3f) color = new Vector4(1.0f, 0.2f, 0.2f, 1.0f); // Red
                else if (level < 0.6f) color = new Vector4(1.0f, 0.8f, 0.0f, 1.0f); // Yellow

                if (drive.Key == DriveType.Fatigue)
                {
                    if (level > 0.8f) color = new Vector4(1.0f, 0.2f, 0.2f, 1.0f);
                    else if (level > 0.4f) color = new Vector4(1.0f, 0.8f, 0.0f, 1.0f);
                    else color = new Vector4(0.2f, 0.8f, 0.2f, 1.0f);
                }

                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
                ImGui.ProgressBar(level, new Vector2(230, 15), $"{drive.Key}: {level:P0}");
                ImGui.PopStyleColor();
            }

            // Inventory
            if (p.TryGetComponent<NPC.Library.Inventory.IInventory>(out var inv))
            {
                int apples = inv.GetItems().Count(i => i.Type == NPC.Library.Inventory.ItemType.Apple);
                var water = inv.GetItems().OfType<NPC.Library.Inventory.WaterBottleItem>().FirstOrDefault();
                int sips = water?.SipsRemaining ?? 0;
                
                ImGui.Spacing();
                ImGui.Text($"Inventory: Apples: {apples} | Water Sips: {sips}");
            }
            
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "WASD: Move  E: Interact  Q: Quaff  R: Eat");

            ImGui.End();
        }

        private void RenderOverview(UIState state)
        {
            if (ImGui.BeginTable("OverviewTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Location");
                ImGui.TableSetupColumn("Status");
                ImGui.TableSetupColumn("Current Action");
                ImGui.TableSetupColumn("Items");
                ImGui.TableHeadersRow();

                int cid = 1;
                foreach (var character in state.SpatialContext.GetCharacters())
                {
                    ImGui.TableNextRow();

                    // Name
                    ImGui.TableNextColumn();
                    ImGui.Text(character.Name ?? $"NPC {cid}");

                    // Location
                    ImGui.TableNextColumn();
                    var loc = state.SpatialContext.GetCharacterLocation(character);
                    if (loc.HasValue) ImGui.Text($"({loc.Value.X}, {loc.Value.Y})");
                    else ImGui.Text("Unknown");

                    // Status
                    ImGui.TableNextColumn();
                    if (character.IsDead) ImGui.TextColored(new Vector4(1, 0, 0, 1), $"Dead: {character.DeathReason}");
                    else ImGui.TextColored(new Vector4(0, 1, 0, 1), "Alive");

                    // Action
                    ImGui.TableNextColumn();
                    ImGui.Text(character.LastAction ?? "Idle");

                    // Items
                    ImGui.TableNextColumn();
                    if (character.TryGetComponent<NPC.Library.Inventory.IInventory>(out var inv))
                    {
                        ImGui.Text(inv.GetItems().Count().ToString());
                    }
                    else
                    {
                        ImGui.Text("0");
                    }

                    cid++;
                }

                ImGui.EndTable();
            }
        }

        private float[]? GetPlotData(System.Collections.Generic.List<float> history)
        {
            if (history == null || history.Count == 0) return null;
            if (history.Count == 1) return new float[] { history[0], history[0] };
            return history.ToArray();
        }

        private void SafePlotLines(string label, float[] data, float minScale, float maxScale, Vector2 size)
        {
            int count = data.Length;
            if (count < 2)
            {
                ImGui.PlotLines(label, ref data[0], count, 0, null, minScale, maxScale, size);
                return;
            }

            if (minScale == float.MaxValue || maxScale == float.MaxValue)
            {
                float min = data[0];
                float max = data[0];
                for (int i = 1; i < count; i++)
                {
                    if (data[i] < min) min = data[i];
                    if (data[i] > max) max = data[i];
                }

                if (Math.Abs(max - min) < 0.0001f)
                {
                    if (minScale == float.MaxValue) minScale = min - 1f;
                    if (maxScale == float.MaxValue) maxScale = max + 1f;
                }
            }
            
            if (minScale == 0 && maxScale == float.MaxValue)
            {
                float max = data[0];
                for (int i = 1; i < count; i++)
                {
                    if (data[i] > max) max = data[i];
                }
                if (max <= 0.0001f) maxScale = 1f;
            }

            ImGui.PlotLines(label, ref data[0], count, 0, null, minScale, maxScale, size);
        }

        private void RenderTrainingStats(UIState state, string speciesName, System.Collections.Generic.List<float> avgFit, System.Collections.Generic.List<float> bestFit, System.Collections.Generic.List<float> deathHist, System.Collections.Generic.List<float> dehyHist, System.Collections.Generic.List<float> starvHist, System.Collections.Generic.List<float>? exhHist, System.Collections.Generic.List<float>? survHist, System.Collections.Generic.List<float>? eatenHist)
        {
            ImGui.Text($"{speciesName} Genetic Evolution Progress — Generation {state.CurrentGeneration}");
            ImGui.Separator();

            bool hasData = avgFit.Count >= 1;
            if (!hasData)
            {
                ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.4f, 1.0f), "Graphs will appear after the first generation completes.");
                if (speciesName == "NPC") RenderLiveGenerationStats(state);
                return;
            }

            var avgArray = GetPlotData(avgFit);
            if (avgArray != null) SafePlotLines("Avg Fitness", avgArray, float.MaxValue, float.MaxValue, new Vector2(500, 150));

            var bestArray = GetPlotData(bestFit);
            if (bestArray != null) SafePlotLines("Best Fitness", bestArray, float.MaxValue, float.MaxValue, new Vector2(500, 150));

            var deathArray = GetPlotData(deathHist);
            if (deathArray != null) SafePlotLines("Total Deaths", deathArray, 0, 16, new Vector2(500, 100));

            var dehydrationArray = GetPlotData(dehyHist);
            var starvationArray = GetPlotData(starvHist);
            var exhaustionArray = exhHist != null ? GetPlotData(exhHist) : null;
            var survivedArray = survHist != null ? GetPlotData(survHist) : null;
            var eatenArray = eatenHist != null ? GetPlotData(eatenHist) : null;
            
            if (dehydrationArray != null && starvationArray != null)
            {
                float maxVal = 16f;
                var dataSeries = new System.Collections.Generic.List<float[]> { dehydrationArray, starvationArray };
                var colors = new System.Collections.Generic.List<uint> 
                { 
                    ImGui.GetColorU32(new Vector4(0.2f, 0.6f, 1.0f, 1.0f)),
                    ImGui.GetColorU32(new Vector4(1.0f, 0.4f, 0.2f, 1.0f))
                };
                
                ImGui.TextColored(new Vector4(0.2f, 0.6f, 1.0f, 1.0f), "■ Dehydration");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.2f, 1.0f), "■ Starvation");

                if (exhaustionArray != null)
                {
                    dataSeries.Add(exhaustionArray);
                    colors.Add(ImGui.GetColorU32(new Vector4(0.8f, 0.8f, 0.8f, 1.0f)));
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1.0f), "■ Exhaustion");
                }
                
                if (eatenArray != null)
                {
                    dataSeries.Add(eatenArray);
                    colors.Add(ImGui.GetColorU32(new Vector4(0.8f, 0.2f, 0.2f, 1.0f)));
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.8f, 0.2f, 0.2f, 1.0f), "■ Eaten");
                }
                if (survivedArray != null)
                {
                    dataSeries.Add(survivedArray);
                    colors.Add(ImGui.GetColorU32(new Vector4(0.2f, 1.0f, 0.2f, 1.0f)));
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.2f, 1.0f, 0.2f, 1.0f), "■ Survived");
                }

                RenderMultiLineGraph("Outcomes", dataSeries, colors, new Vector2(500, 150), maxVal);
            }

            if (speciesName == "NPC")
            {
                var earliestArray = GetPlotData(state.EarliestDeathHistory);
                if (earliestArray != null) SafePlotLines("Earliest Death", earliestArray, float.MaxValue, float.MaxValue, new Vector2(500, 100));

                var latestArray = GetPlotData(state.LatestDeathHistory);
                if (latestArray != null) SafePlotLines("Latest Death", latestArray, float.MaxValue, float.MaxValue, new Vector2(500, 100));
            }
        }

        private void RenderSaveModels(UIState state)
        {
            ImGui.Text("Save Trained Populations");
            ImGui.Separator();
            ImGui.Text("This will save the NPC, Fox, and Sheep neural networks.");
            ImGui.Spacing();
            
            if (ImGui.InputText("Base Name", ref _savePopulationName, 50)) { }
            if (ImGui.Button("Save to AppData"))
            {
                if (!string.IsNullOrWhiteSpace(_savePopulationName))
                {
                    try
                    {
                        var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NPC", "Populations");
                        System.IO.Directory.CreateDirectory(path);

                        if (state.CurrentPopulation.Count > 0)
                            System.IO.File.WriteAllText(System.IO.Path.Combine(path, _savePopulationName + "_npc.json"), System.Text.Json.JsonSerializer.Serialize(state.CurrentPopulation));
                        
                        if (state.FoxPopulation.Count > 0)
                            System.IO.File.WriteAllText(System.IO.Path.Combine(path, _savePopulationName + "_fox.json"), System.Text.Json.JsonSerializer.Serialize(state.FoxPopulation));
                            
                        if (state.SheepPopulation.Count > 0)
                            System.IO.File.WriteAllText(System.IO.Path.Combine(path, _savePopulationName + "_sheep.json"), System.Text.Json.JsonSerializer.Serialize(state.SheepPopulation));
                    }
                    catch { } // Ignore errors
                }
            }
        }

        private void RenderMultiLineGraph(string label, System.Collections.Generic.List<float[]> dataSeries, System.Collections.Generic.List<uint> colors, Vector2 size, float maxValY)
        {
            ImGui.Text(label);
            var p = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(p, p + size, ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.1f, 1.0f)));
            drawList.AddRect(p, p + size, ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1.0f)));

            if (maxValY <= 0) maxValY = 1;

            for (int i = 0; i < dataSeries.Count; i++)
            {
                var data = dataSeries[i];
                if (data.Length < 2) continue;

                uint col = colors[i];
                float stepX = size.X / (data.Length - 1);

                for (int n = 0; n < data.Length - 1; n++)
                {
                    var p1 = new Vector2(p.X + n * stepX, p.Y + size.Y - (data[n] / maxValY) * size.Y);
                    var p2 = new Vector2(p.X + (n + 1) * stepX, p.Y + size.Y - (data[n + 1] / maxValY) * size.Y);
                    
                    // Clamp to box
                    p1.Y = Math.Max(p.Y, Math.Min(p.Y + size.Y, p1.Y));
                    p2.Y = Math.Max(p.Y, Math.Min(p.Y + size.Y, p2.Y));

                    drawList.AddLine(p1, p2, col, 2.0f);
                }
            }

            ImGui.Dummy(size); // Move cursor down below the custom drawn rect
        }

        private void RenderResourceStats(UIState state)
        {
            if (state.AvgApplesCollectedHistory.Count < 1)
            {
                ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.4f, 1.0f), "Resource graphs will appear after the first generation completes.");
                RenderLiveResourceStats(state);
                return;
            }

            var avgApplesCol = GetPlotData(state.AvgApplesCollectedHistory);
            var maxApplesCol = GetPlotData(state.MaxApplesCollectedHistory);
            if (avgApplesCol != null) SafePlotLines("Avg Apples Collected", avgApplesCol, 0, float.MaxValue, new Vector2(500, 100));
            if (maxApplesCol != null) SafePlotLines("Max Apples Collected", maxApplesCol, 0, float.MaxValue, new Vector2(500, 100));

            var avgApplesEaten = GetPlotData(state.AvgApplesEatenHistory);
            var maxApplesEaten = GetPlotData(state.MaxApplesEatenHistory);
            if (avgApplesEaten != null) SafePlotLines("Avg Apples Eaten", avgApplesEaten, 0, float.MaxValue, new Vector2(500, 100));
            if (maxApplesEaten != null) SafePlotLines("Max Apples Eaten", maxApplesEaten, 0, float.MaxValue, new Vector2(500, 100));

            var avgWaterCol = GetPlotData(state.AvgWaterCollectedHistory);
            var maxWaterCol = GetPlotData(state.MaxWaterCollectedHistory);
            if (avgWaterCol != null) SafePlotLines("Avg Water Collected", avgWaterCol, 0, float.MaxValue, new Vector2(500, 100));
            if (maxWaterCol != null) SafePlotLines("Max Water Collected", maxWaterCol, 0, float.MaxValue, new Vector2(500, 100));

            var avgSips = GetPlotData(state.AvgSipsTakenHistory);
            var maxSips = GetPlotData(state.MaxSipsTakenHistory);
            if (avgSips != null) SafePlotLines("Avg Sips Taken", avgSips, 0, float.MaxValue, new Vector2(500, 100));
            if (maxSips != null) SafePlotLines("Max Sips Taken", maxSips, 0, float.MaxValue, new Vector2(500, 100));

            var avgItemsInChest = GetPlotData(state.AvgItemsInChestHistory);
            var maxItemsInChest = GetPlotData(state.MaxItemsInChestHistory);
            if (avgItemsInChest != null) SafePlotLines("Avg Items In Chest", avgItemsInChest, 0, float.MaxValue, new Vector2(500, 100));
            if (maxItemsInChest != null) SafePlotLines("Max Items In Chest", maxItemsInChest, 0, float.MaxValue, new Vector2(500, 100));
        }

        private void RenderLiveGenerationStats(UIState state)
        {
            ImGui.Spacing();
            ImGui.Text("Live Generation Stats:");
            ImGui.Separator();
            
            var characters = state.SpatialContext.GetCharacters().ToList();
            if (characters.Count == 0)
            {
                ImGui.Text("No characters in simulation.");
                return;
            }
            
            int alive = characters.Count(c => !c.IsDead);
            int dead = characters.Count(c => c.IsDead);
            int dehydration = characters.Count(c => c.IsDead && c.DeathReason == "Dehydration");
            int starvation = characters.Count(c => c.IsDead && c.DeathReason == "Starvation");
            int exhaustion = characters.Count(c => c.IsDead && c.DeathReason == "Exhaustion");
            
            ImGui.TextColored(new Vector4(0.2f, 1.0f, 0.2f, 1.0f), $"Alive: {alive}");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1.0f), $"Dead: {dead}");
            
            if (dead > 0)
            {
                ImGui.BulletText($"Dehydration: {dehydration}");
                ImGui.BulletText($"Starvation: {starvation}");
                ImGui.BulletText($"Exhaustion: {exhaustion}");
            }
            
            ImGui.Text($"Tick: {state.TickCount}");
        }

        private void RenderLiveResourceStats(UIState state)
        {
            ImGui.Spacing();
            ImGui.Text("Live Resource Stats:");
            ImGui.Separator();
            
            var characters = state.SpatialContext.GetCharacters().ToList();
            var metrics = characters
                .Select(c => c.GetComponent<NPC.Library.Character.Components.CharacterMetrics>())
                .Where(m => m != null)
                .ToList();
            
            if (metrics.Count == 0)
            {
                ImGui.Text("No metrics data available.");
                return;
            }
            
            int totalApplesCol = metrics.Sum(m => m!.ApplesCollected);
            int totalApplesEaten = metrics.Sum(m => m!.ApplesEaten);
            int totalWaterCol = metrics.Sum(m => m!.WaterCollected);
            int totalSips = metrics.Sum(m => m!.SipsTaken);
            int maxApplesCol = metrics.Max(m => m!.ApplesCollected);
            int maxApplesEaten = metrics.Max(m => m!.ApplesEaten);
            int maxWaterCol = metrics.Max(m => m!.WaterCollected);
            int maxSips = metrics.Max(m => m!.SipsTaken);
            
            if (ImGui.BeginTable("LiveResources", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Resource");
                ImGui.TableSetupColumn("Total");
                ImGui.TableSetupColumn("Best NPC");
                ImGui.TableHeadersRow();
                
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text("Apples Collected");
                ImGui.TableNextColumn(); ImGui.Text($"{totalApplesCol}");
                ImGui.TableNextColumn(); ImGui.Text($"{maxApplesCol}");
                
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text("Apples Eaten");
                ImGui.TableNextColumn(); ImGui.Text($"{totalApplesEaten}");
                ImGui.TableNextColumn(); ImGui.Text($"{maxApplesEaten}");
                
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text("Water Collected");
                ImGui.TableNextColumn(); ImGui.Text($"{totalWaterCol}");
                ImGui.TableNextColumn(); ImGui.Text($"{maxWaterCol}");
                
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text("Sips Taken");
                ImGui.TableNextColumn(); ImGui.Text($"{totalSips}");
                ImGui.TableNextColumn(); ImGui.Text($"{maxSips}");
                
                ImGui.EndTable();
            }
        }

        private void RenderCharacterDetails(UIState state, Character character)
        {
            string name = character.Name ?? "NPC";
            if (ImGui.InputText("Name", ref name, 50))
            {
                character.Name = name;
            }

            if (character.IsDead)
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), $"This character has died due to {character.DeathReason}.");
                
                if (character.TryGetComponent<NPC.Library.Character.Components.NarrativeComponent>(out var narrative))
                {
                    var lastWords = narrative.GetHistory().FirstOrDefault(h => h.Action == "DIED" || h.Action == "Last Words");
                    if (lastWords != default)
                    {
                        ImGui.Spacing();
                        ImGui.TextWrapped($"Last Words: \"{lastWords.Reason}\"");
                    }
                }
                return;
            }

            ImGui.Text($"Current Action: {character.LastAction}");
            ImGui.Separator();

            // DRIVES (Progress Bars)
            ImGui.Text("Drives");
            foreach (var drive in character.Drives.Levels)
            {
                float level = (float)drive.Value;
                Vector4 color = new Vector4(0.2f, 0.8f, 0.2f, 1.0f); // Green
                if (level < 0.3f) color = new Vector4(1.0f, 0.2f, 0.2f, 1.0f); // Red
                else if (level < 0.6f) color = new Vector4(1.0f, 0.8f, 0.0f, 1.0f); // Yellow

                // Fatigue is reversed (high is bad)
                if (drive.Key == DriveType.Fatigue)
                {
                    if (level > 0.8f) color = new Vector4(1.0f, 0.2f, 0.2f, 1.0f);
                    else if (level > 0.4f) color = new Vector4(1.0f, 0.8f, 0.0f, 1.0f);
                    else color = new Vector4(0.2f, 0.8f, 0.2f, 1.0f);
                }

                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
                ImGui.ProgressBar(level, new Vector2(200, 20), $"{drive.Key}: {level:P0}");
                ImGui.PopStyleColor();
            }

            ImGui.Spacing();
            ImGui.Separator();

            // INVENTORY
            if (ImGui.CollapsingHeader("Inventory", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (character.TryGetComponent<NPC.Library.Inventory.IInventory>(out var inv))
                {
                    var items = inv.GetItems().ToList();
                    if (!items.Any())
                    {
                        ImGui.Text("Inventory is empty.");
                    }
                    else
                    {
                        foreach (var item in items)
                        {
                            if (item is NPC.Library.Inventory.WaterBottleItem water)
                            {
                                ImGui.BulletText($"Water Bottle ({water.SipsRemaining}/3 sips)");
                            }
                            else
                            {
                                ImGui.BulletText(item.Type.ToString());
                            }
                        }
                    }
                }
            }

            ImGui.Spacing();
            ImGui.Separator();

            // CHEST INVENTORY
            if (ImGui.CollapsingHeader("Home Chest", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (character.TryGetComponent<NPC.Library.Memory.IMemory>(out var mem))
                {
                    var chestLocs = mem.Recall(TileType.Chest).ToList();
                    if (chestLocs.Any())
                    {
                        var chestLoc = chestLocs.First();
                        var chestInv = state.SpatialContext.GetChest(chestLoc);
                        if (chestInv != null)
                        {
                            var items = chestInv.GetItems().ToList();
                            if (!items.Any())
                            {
                                ImGui.Text("Chest is empty.");
                            }
                            else
                            {
                                foreach (var item in items)
                                {
                                    if (item is NPC.Library.Inventory.WaterBottleItem water)
                                    {
                                        ImGui.BulletText($"Water Bottle ({water.SipsRemaining}/3 sips)");
                                    }
                                    else
                                    {
                                        ImGui.BulletText(item.Type.ToString());
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        ImGui.Text("Does not remember a chest.");
                    }
                }
                else
                {
                    ImGui.Text("No memory component to recall chest location.");
                }
            }

            ImGui.Spacing();
            ImGui.Separator();

            // MEMORY
            if (ImGui.CollapsingHeader("Memory"))
            {
                if (character.TryGetComponent<NPC.Library.Memory.IMemory>(out var mem))
                {
                    ImGui.Text($"Memory Type: {mem.GetType().Name}");
                    int treeCount = mem.Recall(TileType.AppleTree).Count();
                    int waterCount = mem.Recall(TileType.Water).Count();
                    ImGui.BulletText($"Apple Trees known: {treeCount}");
                    ImGui.BulletText($"Water sources known: {waterCount}");
                }
                else
                {
                    ImGui.Text("This character has no memory component.");
                }
            }

            ImGui.Spacing();
            ImGui.Separator();

            // NARRATIVE
            if (ImGui.CollapsingHeader("Narrative (Inner Thoughts)", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (character.TryGetComponent<NPC.Library.Character.Components.NarrativeComponent>(out var narrative))
                {
                    var history = narrative.GetHistory().ToList();
                    if (!history.Any())
                    {
                        ImGui.Text("No thoughts yet...");
                    }
                    else
                    {
                        // Print from most recent to oldest
                        for (int i = history.Count - 1; i >= 0; i--)
                        {
                            var record = history[i];
                            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), $"[{record.Action}]");
                            ImGui.TextWrapped(record.Reason);
                            if (i > 0) ImGui.Spacing();
                        }
                    }
                }
                else
                {
                    ImGui.Text("No narrative component active.");
                }
            }

            ImGui.Spacing();
            ImGui.Separator();

            // BRAIN (LLM)
            if (ImGui.CollapsingHeader("Brain (LLM)"))
            {
                if (character.TryGetComponent<NPC.Library.Character.Components.LLMComponent>(out var llmComp))
                {
                    ImGui.Text("Personal LLM Override Configuration");
                    if (ImGui.Button("Remove Override (Use Global)"))
                    {
                        character.RemoveComponent<NPC.Library.Character.Components.LLMComponent>();
                    }
                    else
                    {
                        ImGui.Separator();

                        bool isEnabled = llmComp.Config.IsEnabled;
                        if (ImGui.Checkbox("Enable AI for this Character##LLM", ref isEnabled))
                        {
                            llmComp.Config.IsEnabled = isEnabled;
                        }

                        var provider = (int)llmComp.Config.Provider;
                        if (ImGui.Combo("Provider##LLM", ref provider, "None\0Ollama\0OpenAI\0Gemini\0Claude\0OpenRouter\0"))
                        {
                            llmComp.Config.Provider = (NPC.LLM.ProviderType)provider;
                        }

                        string defaultUrl = llmComp.Config.Provider switch {
                            NPC.LLM.ProviderType.Ollama => "http://localhost:11434/api/chat",
                            NPC.LLM.ProviderType.OpenAI => "https://api.openai.com/v1/chat/completions",
                            NPC.LLM.ProviderType.OpenRouter => "https://openrouter.ai/api/v1/chat/completions",
                            _ => ""
                        };
                        string defaultModel = llmComp.Config.Provider switch {
                            NPC.LLM.ProviderType.Ollama => "llama3",
                            NPC.LLM.ProviderType.OpenAI => "gpt-4o",
                            NPC.LLM.ProviderType.OpenRouter => "openai/gpt-4o",
                            _ => ""
                        };

                        string baseUrl = llmComp.Config.BaseUrl;
                        if (ImGui.InputTextWithHint("Base URL##LLM", defaultUrl, ref baseUrl, 256)) llmComp.Config.BaseUrl = baseUrl;

                        string apiKey = llmComp.Config.ApiKey;
                        if (ImGui.InputTextWithHint("API Key##LLM", llmComp.Config.Provider == NPC.LLM.ProviderType.Ollama ? "Not required for Ollama" : "sk-...", ref apiKey, 256, ImGuiInputTextFlags.Password)) llmComp.Config.ApiKey = apiKey;

                        string modelName = llmComp.Config.ModelName;
                        if (ImGui.InputTextWithHint("Model Name##LLM", defaultModel, ref modelName, 256)) llmComp.Config.ModelName = modelName;

                        float temp = llmComp.Config.Temperature;
                        if (ImGui.SliderFloat("Temperature##LLM", ref temp, 0.0f, 2.0f)) llmComp.Config.Temperature = temp;

                        int maxTokens = llmComp.Config.MaxTokens;
                        if (ImGui.InputInt("Max Tokens##LLM", ref maxTokens)) llmComp.Config.MaxTokens = maxTokens;

                        ImGui.Separator();
                        if (ImGui.Button("Test Connection##LLM"))
                        {
                            _characterTestStatuses[character] = "Testing...";
                            System.Threading.Tasks.Task.Run(async () => {
                                try {
                                    var p = NPC.LLM.LLMProviderFactory.Create(llmComp.Config);
                                    var req = new NPC.LLM.LLMRequest { Messages = new System.Collections.Generic.List<NPC.LLM.ChatMessage> { new NPC.LLM.ChatMessage { Role = NPC.LLM.ChatRole.User, Content = "Say hello!" } }, MaxTokens = 50 };
                                    var res = await p.GenerateResponseAsync(req);
                                    _characterTestStatuses[character] = $"Success: {res}";
                                } catch (System.Exception e) {
                                    _characterTestStatuses[character] = $"Error: {e.Message}";
                                }
                            });
                        }
                        if (_characterTestStatuses.TryGetValue(character, out var status) && !string.IsNullOrEmpty(status))
                        {
                            ImGui.TextWrapped(status);
                        }

                        ImGui.Spacing();
                        if (ImGui.Button("Save Settings As Default##LLM"))
                        {
                            var aiSettings = new NPC.Application.AISettings
                            {
                                GlobalConfig = state.GlobalLLMConfig
                            };
                            
                            foreach (var c in state.SpatialContext.GetCharacters())
                            {
                                if (c.TryGetComponent<NPC.Library.Character.Components.LLMComponent>(out var llm))
                                {
                                    aiSettings.IndividualOverrides[c.Name] = llm.Config;
                                }
                            }
                            
                            NPC.Application.AISettingsManager.SaveSettings(aiSettings);
                            _characterTestStatuses[character] = "Settings Saved Successfully!";
                        }
                    }
                }
                else
                {
                    ImGui.Text("Using Global LLM Configuration.");
                    if (ImGui.Button("Add Personal Override"))
                    {
                        character.AddComponent(new NPC.Library.Character.Components.LLMComponent());
                    }
                }
            }
        }
    }
}
