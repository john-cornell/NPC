using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using NPC.Application;
using NPC.Library.Character;
using NPC.Library.Spatial.Grid;
using DriveType = NPC.Library.Character.DriveType;

namespace NPC.UI.Isometric
{
    public class CharacterUIOverlay
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
            // Set next window size and position if it's the first time
            ImGui.SetNextWindowSize(new Vector2(600, 400), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(20, 150), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("Character Management", ImGuiWindowFlags.None))
            {
                if (ImGui.BeginTabBar("CharacterTabs"))
                {
                    // OVERVIEW TAB
                    if (ImGui.BeginTabItem("Overview"))
                    {
                        RenderOverview(state);
                        ImGui.EndTabItem();
                    }

                    // TRAINING STATS TAB
                    if (state.AverageFitnessHistory.Count > 0 || state.BestFitnessHistory.Count > 0)
                    {
                        if (ImGui.BeginTabItem("Training Stats"))
                        {
                            RenderTrainingStats(state);
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

        private void RenderTrainingStats(UIState state)
        {
            ImGui.Text("Genetic Evolution Progress");
            ImGui.Separator();

            if (state.AverageFitnessHistory.Count > 0)
            {
                var avgArray = state.AverageFitnessHistory.ToArray();
                ImGui.PlotLines("Avg Fitness", ref avgArray[0], avgArray.Length, 0, null, float.MaxValue, float.MaxValue, new Vector2(500, 150));
            }

            if (state.BestFitnessHistory.Count > 0)
            {
                var bestArray = state.BestFitnessHistory.ToArray();
                ImGui.PlotLines("Best Fitness", ref bestArray[0], bestArray.Length, 0, null, float.MaxValue, float.MaxValue, new Vector2(500, 150));
            }

            if (state.DeathCountHistory.Count > 0)
            {
                var deathArray = state.DeathCountHistory.ToArray();
                ImGui.PlotLines("Total Deaths", ref deathArray[0], deathArray.Length, 0, null, 0, 16, new Vector2(500, 100));
            }

            if (state.DeathCountHistory.Count > 0)
            {
                var dehydrationArray = state.DehydrationDeathHistory.ToArray();
                var starvationArray = state.StarvationDeathHistory.ToArray();
                var exhaustionArray = state.ExhaustionDeathHistory.ToArray();
                var survivedArray = state.SurvivedCountHistory.ToArray();
                
                float maxVal = 16f; // Max population
                
                var dataSeries = new System.Collections.Generic.List<float[]> { dehydrationArray, starvationArray, exhaustionArray, survivedArray };
                var colors = new System.Collections.Generic.List<uint> 
                { 
                    ImGui.GetColorU32(new Vector4(0.2f, 0.6f, 1.0f, 1.0f)), // Blue for Dehydration
                    ImGui.GetColorU32(new Vector4(1.0f, 0.4f, 0.2f, 1.0f)), // Orange for Starvation
                    ImGui.GetColorU32(new Vector4(0.8f, 0.8f, 0.8f, 1.0f)), // Gray for Exhaustion
                    ImGui.GetColorU32(new Vector4(0.2f, 1.0f, 0.2f, 1.0f))  // Green for Survived
                };
                
                ImGui.TextColored(new Vector4(0.2f, 0.6f, 1.0f, 1.0f), "■ Dehydration");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.2f, 1.0f), "■ Starvation");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1.0f), "■ Exhaustion");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.2f, 1.0f, 0.2f, 1.0f), "■ Survived");

                RenderMultiLineGraph("Outcomes", dataSeries, colors, new Vector2(500, 150), maxVal);
            }

            if (state.EarliestDeathHistory.Count > 0)
            {
                var earliestArray = state.EarliestDeathHistory.ToArray();
                ImGui.PlotLines("Earliest Death", ref earliestArray[0], earliestArray.Length, 0, null, float.MaxValue, float.MaxValue, new Vector2(500, 100));
            }

            if (state.LatestDeathHistory.Count > 0)
            {
                var latestArray = state.LatestDeathHistory.ToArray();
                ImGui.PlotLines("Latest Death", ref latestArray[0], latestArray.Length, 0, null, float.MaxValue, float.MaxValue, new Vector2(500, 100));
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Save Population");
            if (ImGui.InputText("Name", ref _savePopulationName, 50)) { }
            if (ImGui.Button("Save to AppData"))
            {
                if (!string.IsNullOrWhiteSpace(_savePopulationName) && state.CurrentPopulation.Count > 0)
                {
                    try
                    {
                        var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NPC", "Populations");
                        System.IO.Directory.CreateDirectory(path);
                        var json = System.Text.Json.JsonSerializer.Serialize(state.CurrentPopulation);
                        System.IO.File.WriteAllText(System.IO.Path.Combine(path, _savePopulationName + ".json"), json);
                    }
                    catch { } // Ignore errors for now
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
            if (state.AvgApplesCollectedHistory.Count > 0)
            {
                var avgApplesCol = state.AvgApplesCollectedHistory.ToArray();
                var maxApplesCol = state.MaxApplesCollectedHistory.ToArray();
                ImGui.PlotLines("Avg Apples Collected", ref avgApplesCol[0], avgApplesCol.Length, 0, null, 0, float.MaxValue, new Vector2(500, 100));
                ImGui.PlotLines("Max Apples Collected", ref maxApplesCol[0], maxApplesCol.Length, 0, null, 0, float.MaxValue, new Vector2(500, 100));
            }

            if (state.AvgApplesEatenHistory.Count > 0)
            {
                var avgApplesEaten = state.AvgApplesEatenHistory.ToArray();
                var maxApplesEaten = state.MaxApplesEatenHistory.ToArray();
                ImGui.PlotLines("Avg Apples Eaten", ref avgApplesEaten[0], avgApplesEaten.Length, 0, null, 0, float.MaxValue, new Vector2(500, 100));
                ImGui.PlotLines("Max Apples Eaten", ref maxApplesEaten[0], maxApplesEaten.Length, 0, null, 0, float.MaxValue, new Vector2(500, 100));
            }

            if (state.AvgWaterCollectedHistory.Count > 0)
            {
                var avgWaterCol = state.AvgWaterCollectedHistory.ToArray();
                var maxWaterCol = state.MaxWaterCollectedHistory.ToArray();
                ImGui.PlotLines("Avg Water Collected", ref avgWaterCol[0], avgWaterCol.Length, 0, null, 0, float.MaxValue, new Vector2(500, 100));
                ImGui.PlotLines("Max Water Collected", ref maxWaterCol[0], maxWaterCol.Length, 0, null, 0, float.MaxValue, new Vector2(500, 100));
            }

            if (state.AvgSipsTakenHistory.Count > 0)
            {
                var avgSips = state.AvgSipsTakenHistory.ToArray();
                var maxSips = state.MaxSipsTakenHistory.ToArray();
                ImGui.PlotLines("Avg Sips Taken", ref avgSips[0], avgSips.Length, 0, null, 0, float.MaxValue, new Vector2(500, 100));
                ImGui.PlotLines("Max Sips Taken", ref maxSips[0], maxSips.Length, 0, null, 0, float.MaxValue, new Vector2(500, 100));
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
                        if (state.SpatialContext is GridSpatialContext gridCtx && gridCtx.Map.Chests.TryGetValue(chestLoc, out var chestInv))
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
