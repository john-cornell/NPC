using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NPC.Application;
using NPC.Library.Character;
using NPC.Library.Spatial.Grid;
using Raylib_cs;
using rlImGui_cs;
using System.Numerics;
using ImGuiNET;

namespace NPC.UI.Isometric
{
    public class IsometricRenderer : IDisposable
    {
        private Dictionary<string, Texture2D> _textures = new();
        private string _globalTestStatus = "";
        private Camera2D _camera;
        private CharacterUIOverlay _uiOverlay = new();
        
        // Isometric parameters
        private const int TileWidth = 64; // Since assets are 128x128, the "floor" diamond is roughly 128x64 or 64x32
        private const int TileHeight = 32;

        private class SpeechBubble
        {
            public string Text { get; set; } = "";
            public float ExpirationTime { get; set; }
        }
        private Dictionary<Character, SpeechBubble> _activeBubbles = new();
        private NPC.Library.Messaging.MessageDispatcher _dispatcher = null!;

        public void Initialize(NPC.Library.Messaging.MessageDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _dispatcher.Subscribe<NPC.Library.Messaging.DialogueGeneratedMessage>(OnDialogueGenerated);

            Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
            Raylib.InitWindow(1280, 720, "NPC Village - Isometric POC");
            Raylib.SetTargetFPS(60);

            rlImGui.Setup(true, true);

            // Load all assets
            string assetDir = "Assets";
            if (Directory.Exists(assetDir))
            {
                foreach (var file in Directory.GetFiles(assetDir, "*.png"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    _textures[name] = Raylib.LoadTexture(file);
                }
            }

            _camera = new Camera2D
            {
                Offset = new Vector2(1280 / 2.0f, 720 / 2.0f),
                Target = new Vector2(0, 0),
                Rotation = 0.0f,
                Zoom = 1.0f
            };
        }

        public void UpdateDispatcher(NPC.Library.Messaging.MessageDispatcher newDispatcher)
        {
            _dispatcher?.Unsubscribe<NPC.Library.Messaging.DialogueGeneratedMessage>(OnDialogueGenerated);
            _dispatcher = newDispatcher;
            _dispatcher.Subscribe<NPC.Library.Messaging.DialogueGeneratedMessage>(OnDialogueGenerated);
            
            // Clear bubbles since they belong to old characters
            _activeBubbles.Clear();
            _cameraInitialized = false; // re-center camera on new map well
        }

        private void OnDialogueGenerated(NPC.Library.Messaging.DialogueGeneratedMessage msg)
        {
            // Set expiration to 5 seconds from now
            _activeBubbles[msg.Character] = new SpeechBubble
            {
                Text = msg.Dialogue,
                ExpirationTime = (float)Raylib.GetTime() + 5.0f
            };
        }

        private Vector2 GridToIso(int gridX, int gridY)
        {
            // Standard isometric projection:
            // x = (gridX - gridY) * (TileWidth / 2)
            // y = (gridX + gridY) * (TileHeight / 2)
            float isoX = (gridX - gridY) * (TileWidth / 2.0f);
            float isoY = (gridX + gridY) * (TileHeight / 2.0f);
            return new Vector2(isoX, isoY);
        }

        private void DrawIsoSprite(string spriteName, int gridX, int gridY, float yOffset = 0, float scale = 1.0f, bool flipH = false)
        {
            if (!_textures.TryGetValue(spriteName, out var tex)) return;

            Vector2 pos = GridToIso(gridX, gridY);
            
            // Adjust position so the bottom center of the sprite aligns with the iso coordinate
            float destX = pos.X - (tex.Width * scale) / 2.0f;
            float destY = pos.Y - (tex.Height * scale) + (TileHeight / 2.0f) + yOffset;

            Rectangle sourceRec = new Rectangle(0, 0, flipH ? -tex.Width : tex.Width, tex.Height);
            Rectangle destRec = new Rectangle(destX, destY, tex.Width * scale, tex.Height * scale);
            Raylib.DrawTexturePro(tex, sourceRec, destRec, new Vector2(0, 0), 0.0f, Color.White);
        }

        private bool _cameraInitialized = false;

        public void Render(UIState state)
        {
            if (state.SpatialContext is not GridSpatialContext gridCtx) return;

            if (!_cameraInitialized)
            {
                if (state.WellLocation.HasValue)
                {
                    _camera.Target = GridToIso(state.WellLocation.Value.X, state.WellLocation.Value.Y);
                }
                _cameraInitialized = true;
            }

            // Camera Controls
            bool isMouseOverUI = ImGui.GetIO().WantCaptureMouse;
            if (!isMouseOverUI)
            {
                if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) _camera.Target.Y -= 10.0f / _camera.Zoom;
                if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) _camera.Target.Y += 10.0f / _camera.Zoom;
                if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left)) _camera.Target.X -= 10.0f / _camera.Zoom;
                if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right)) _camera.Target.X += 10.0f / _camera.Zoom;
                
                if (Raylib.IsKeyDown(KeyboardKey.Q)) _camera.Zoom -= 0.02f;
                if (Raylib.IsKeyDown(KeyboardKey.E)) _camera.Zoom += 0.02f;

                float wheel = Raylib.GetMouseWheelMove();
                if (wheel != 0)
                {
                    _camera.Zoom += wheel * 0.1f;
                }

                if (Raylib.IsMouseButtonDown(MouseButton.Left))
                {
                    Vector2 delta = Raylib.GetMouseDelta();
                    _camera.Target.X -= delta.X / _camera.Zoom;
                    _camera.Target.Y -= delta.Y / _camera.Zoom;
                }

                if (_camera.Zoom < 0.1f) _camera.Zoom = 0.1f;
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(30, 30, 40, 255));

            Raylib.BeginMode2D(_camera);

            var map = gridCtx.Map;

            // Draw Terrain First (Bottom Layer)
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    string texName = "iso_grass_tile";
                    bool flipTerrain = false;

                    if (map.Tiles[x, y] == TileType.Wall)
                    {
                        bool hasNegX = (x > 0 && (map.Tiles[x - 1, y] == TileType.Wall || map.Tiles[x - 1, y] == TileType.Door));
                        bool hasPosX = (x < map.Width - 1 && (map.Tiles[x + 1, y] == TileType.Wall || map.Tiles[x + 1, y] == TileType.Door));
                        bool hasNegY = (y > 0 && (map.Tiles[x, y - 1] == TileType.Wall || map.Tiles[x, y - 1] == TileType.Door));
                        bool hasPosY = (y < map.Height - 1 && (map.Tiles[x, y + 1] == TileType.Wall || map.Tiles[x, y + 1] == TileType.Door));
                        
                        bool hasXNeighbor = hasNegX || hasPosX;
                        bool hasYNeighbor = hasNegY || hasPosY;
                        
                        if (hasXNeighbor && hasYNeighbor) 
                        {
                            if (hasPosX && hasPosY) texName = "iso_wall_corner_top";
                            else if (hasNegX && hasNegY) texName = "iso_wall_corner_bottom";
                            else if (hasPosX && hasNegY) texName = "iso_wall_corner_left";
                            else if (hasNegX && hasPosY) texName = "iso_wall_corner_right";
                            else texName = "iso_wall_corner_top";
                        }
                        else 
                        {
                            texName = "iso_wall_x";
                            if (hasXNeighbor) flipTerrain = true; // Varying X runs top-left to bottom-right, needs flipped sprite
                        }
                    }
                    else
                    {
                        texName = map.Tiles[x, y] switch
                        {
                            TileType.Water => "iso_water_tile",
                            TileType.Road => "iso_road_tile",
                            TileType.Floor => "iso_floor_tile",
                            _ => "iso_grass_tile"
                        };
                    }
                    
                    // We draw terrain slightly smaller so the tiles fit together depending on how they were cropped
                    DrawIsoSprite(texName, x, y, yOffset: 16f, scale: 0.5f, flipH: flipTerrain);
                }
            }

            // Draw Objects (Middle Layer)
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    string objTex = map.Tiles[x, y] switch
                    {
                        TileType.AppleTree => gridCtx.GetAppleCount((x, y)) > 0 ? "iso_tree_apple" : "iso_tree_empty",
                        TileType.Door => "iso_door_tile",
                        TileType.Chest => "iso_chest_tile",
                        TileType.Bed => "iso_bed_tile",
                        TileType.Well => "iso_well_tile",
                        _ => null
                    };

                    if (objTex != null)
                    {
                        bool flipObj = false;
                        if (map.Tiles[x, y] == TileType.Door)
                        {
                            objTex = "iso_door_x";
                            // If there's a wall on the X axis, it's an X-door (so we flip it just like the X walls)
                            if ((x > 0 && map.Tiles[x - 1, y] == TileType.Wall) || 
                                (x < map.Width - 1 && map.Tiles[x + 1, y] == TileType.Wall))
                            {
                                flipObj = true;
                            }
                        }
                        DrawIsoSprite(objTex, x, y, yOffset: 16f, scale: 0.5f, flipH: flipObj);
                    }
                }
            }

            // Draw Characters (Top Layer)
            // Code-driven bouncy animation based on time
            float time = (float)Raylib.GetTime();
            
            bool clickedThisFrame = Raylib.IsMouseButtonPressed(MouseButton.Left) && !ImGui.GetIO().WantCaptureMouse;
            Vector2 mouseWorld = Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), _camera);

            int charIdx = 0;
            foreach (var character in state.SpatialContext.GetCharacters())
            {
                var pos = state.SpatialContext.GetCharacterLocation(character);
                if (pos.HasValue)
                {
                    // If moving, make them bounce using a sine wave
                    float bounceOffset = 0f;
                    if (character.LastAction.Contains("Moving") || character.LastAction.Contains("Wandering"))
                    {
                        bounceOffset = (float)Math.Abs(Math.Sin(time * 15.0f)) * -10.0f; // Bounces up by 10 pixels
                    }
                    
                    string charTex = (charIdx % 2 == 0) ? "iso_villager_farmer" : "iso_villager_baker";
                    float scale = 0.5f;
                    float yOffset = bounceOffset + 16f;

                    if (character.IsDead)
                    {
                        charTex = "iso_gravestone";
                        scale = 0.5f;
                        yOffset = 0f;
                    }
                    else if (character.LastAction.Contains("Sleeping"))
                    {
                        charTex = (charIdx % 2 == 0) ? "iso_villager_farmer_sleeping" : "iso_villager_baker_sleeping";
                        scale = 0.5f;
                        yOffset = 16f;
                    }
                    
                    if (!_textures.TryGetValue(charTex, out var tex)) continue;
                    
                    Vector2 isoPos = GridToIso(pos.Value.X, pos.Value.Y);
                    float destX = isoPos.X - (tex.Width * scale) / 2.0f;
                    float destY = isoPos.Y - (tex.Height * scale) + (TileHeight / 2.0f) + yOffset;

                    Raylib.DrawTextureEx(tex, new Vector2(destX, destY), 0.0f, scale, Color.White);

                    // Check for click
                    if (clickedThisFrame)
                    {
                        Rectangle charRect = new Rectangle(destX, destY, tex.Width * scale, tex.Height * scale);
                        if (Raylib.CheckCollisionPointRec(mouseWorld, charRect))
                        {
                            _uiOverlay.SelectCharacter(character);
                            if (character.IsDead)
                            {
                                _activeBubbles[character] = new SpeechBubble
                                {
                                    Text = $"Died from {character.DeathReason}",
                                    ExpirationTime = (float)Raylib.GetTime() + 5.0f
                                };
                            }
                        }
                    }

                    // Draw Name
                    string displayName = character.Name ?? $"NPC {charIdx + 1}";
                    int textWidth = Raylib.MeasureText(displayName, 20);
                    
                    // Add a small dark background behind the text so it's readable
                    Raylib.DrawRectangle((int)(isoPos.X - textWidth / 2.0f) - 2, (int)(destY - 25) - 2, textWidth + 4, 24, new Color(0, 0, 0, 150));
                    Raylib.DrawText(displayName, (int)(isoPos.X - textWidth / 2.0f), (int)(destY - 25), 20, Color.White);

                    // Draw Speech Bubble
                    if (_activeBubbles.TryGetValue(character, out var bubble))
                    {
                        if (time > bubble.ExpirationTime)
                        {
                            _activeBubbles.Remove(character);
                        }
                        else
                        {
                            // A simple white box with black text above the character
                            int bubbleWidth = 200; // Fixed width for word wrapping
                            int padding = 10;
                            // Measure height needed for wrapped text
                            // We can use DrawTextEx or just guess it based on length for a quick POC
                            int lines = (bubble.Text.Length / 25) + 1;
                            int bubbleHeight = lines * 20 + (padding * 2);

                            float bubbleX = isoPos.X - (bubbleWidth / 2.0f);
                            float bubbleY = destY - 40 - bubbleHeight;

                            Raylib.DrawRectangleRounded(new Rectangle(bubbleX, bubbleY, bubbleWidth, bubbleHeight), 0.2f, 10, Color.White);
                            Raylib.DrawRectangleRoundedLinesEx(new Rectangle(bubbleX, bubbleY, bubbleWidth, bubbleHeight), 0.2f, 10, 2.0f, Color.DarkGray);
                            
                            // Draw a small tail for the speech bubble
                            Raylib.DrawTriangle(
                                new Vector2(isoPos.X - 10, bubbleY + bubbleHeight),
                                new Vector2(isoPos.X + 10, bubbleY + bubbleHeight),
                                new Vector2(isoPos.X, bubbleY + bubbleHeight + 10),
                                Color.White
                            );
                            
                            // To draw wrapped text properly in Raylib, we can use DrawTextRec (but Raylib_cs might not expose it easily)
                            // A simple hack is to split the text
                            string[] words = bubble.Text.Split(' ');
                            string currentLine = "";
                            int lineY = (int)bubbleY + padding;
                            foreach (var word in words)
                            {
                                if (Raylib.MeasureText(currentLine + word + " ", 15) > bubbleWidth - (padding * 2))
                                {
                                    Raylib.DrawText(currentLine, (int)bubbleX + padding, lineY, 15, Color.Black);
                                    currentLine = word + " ";
                                    lineY += 18;
                                }
                                else
                                {
                                    currentLine += word + " ";
                                }
                            }
                            if (!string.IsNullOrEmpty(currentLine))
                            {
                                Raylib.DrawText(currentLine, (int)bubbleX + padding, lineY, 15, Color.Black);
                            }
                        }
                    }
                }
                charIdx++;
            }

            Raylib.EndMode2D();

            rlImGui.Begin();
            _uiOverlay.Render(state);

            // Options Window
            if (ImGui.Begin("Options"))
            {
                if (ImGui.BeginTabBar("OptionsTabs"))
                {
                    if (ImGui.BeginTabItem("Video"))
                    {
                        bool isFullScreen = Raylib.IsWindowFullscreen();
                        if (ImGui.Checkbox("Fullscreen", ref isFullScreen))
                        {
                            Raylib.ToggleFullscreen();
                        }
                        
                        bool slowMode = state.SlowMode;
                        if (ImGui.Checkbox("Slow Mode (Real-Time)", ref slowMode))
                        {
                            state.SlowMode = slowMode;
                            state.SlowModeChanged = true;
                        }
                        
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Controls"))
                    {
                        ImGui.Text("WASD/Arrows: Move Camera");
                        ImGui.Text("Left Click + Drag: Pan Camera");
                        ImGui.Text("Mouse Wheel or Q/E: Zoom In/Out");
                        ImGui.Text("ESC: Quit");
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("AI Settings"))
                    {
                        ImGui.Text("Global LLM Configuration");
                        ImGui.Separator();

                        bool isEnabled = state.GlobalLLMConfig.IsEnabled;
                        if (ImGui.Checkbox("Enable Global AI", ref isEnabled))
                        {
                            state.GlobalLLMConfig.IsEnabled = isEnabled;
                        }

                        var provider = (int)state.GlobalLLMConfig.Provider;
                        if (ImGui.Combo("Provider", ref provider, "None\0Ollama\0OpenAI\0Gemini\0Claude\0OpenRouter\0"))
                        {
                            state.GlobalLLMConfig.Provider = (NPC.LLM.ProviderType)provider;
                        }

                        string defaultUrl = state.GlobalLLMConfig.Provider switch {
                            NPC.LLM.ProviderType.Ollama => "http://localhost:11434/api/chat",
                            NPC.LLM.ProviderType.OpenAI => "https://api.openai.com/v1/chat/completions",
                            NPC.LLM.ProviderType.OpenRouter => "https://openrouter.ai/api/v1/chat/completions",
                            _ => ""
                        };
                        string defaultModel = state.GlobalLLMConfig.Provider switch {
                            NPC.LLM.ProviderType.Ollama => "llama3",
                            NPC.LLM.ProviderType.OpenAI => "gpt-4o",
                            NPC.LLM.ProviderType.OpenRouter => "openai/gpt-4o",
                            _ => ""
                        };

                        string baseUrl = state.GlobalLLMConfig.BaseUrl;
                        if (ImGui.InputTextWithHint("Base URL", defaultUrl, ref baseUrl, 256)) state.GlobalLLMConfig.BaseUrl = baseUrl;

                        string apiKey = state.GlobalLLMConfig.ApiKey;
                        if (ImGui.InputTextWithHint("API Key", state.GlobalLLMConfig.Provider == NPC.LLM.ProviderType.Ollama ? "Not required for Ollama" : "sk-...", ref apiKey, 256, ImGuiInputTextFlags.Password)) state.GlobalLLMConfig.ApiKey = apiKey;

                        string modelName = state.GlobalLLMConfig.ModelName;
                        if (ImGui.InputTextWithHint("Model Name", defaultModel, ref modelName, 256)) state.GlobalLLMConfig.ModelName = modelName;

                        float temp = state.GlobalLLMConfig.Temperature;
                        if (ImGui.SliderFloat("Temperature", ref temp, 0.0f, 2.0f)) state.GlobalLLMConfig.Temperature = temp;

                        int maxTokens = state.GlobalLLMConfig.MaxTokens;
                        if (ImGui.InputInt("Max Tokens", ref maxTokens)) state.GlobalLLMConfig.MaxTokens = maxTokens;

                        ImGui.Separator();
                        if (ImGui.Button("Test Connection"))
                        {
                            _globalTestStatus = "Testing...";
                            System.Threading.Tasks.Task.Run(async () => {
                                try {
                                    var p = NPC.LLM.LLMProviderFactory.Create(state.GlobalLLMConfig);
                                    var req = new NPC.LLM.LLMRequest { Messages = new System.Collections.Generic.List<NPC.LLM.ChatMessage> { new NPC.LLM.ChatMessage { Role = NPC.LLM.ChatRole.User, Content = "Say hello!" } }, MaxTokens = 50 };
                                    var res = await p.GenerateResponseAsync(req);
                                    _globalTestStatus = $"Success: {res}";
                                } catch (System.Exception e) {
                                    _globalTestStatus = $"Error: {e.Message}";
                                }
                            });
                        }
                        if (!string.IsNullOrEmpty(_globalTestStatus))
                        {
                            ImGui.TextWrapped(_globalTestStatus);
                        }

                        ImGui.Spacing();
                        if (ImGui.Button("Save Settings As Default"))
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
                            _globalTestStatus = "Settings Saved Successfully!";
                        }

                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
            }
            ImGui.End();

            rlImGui.End();

            // Native UI Overlay
            Raylib.DrawText($"Tick: {state.TickCount}", 10, 10, 20, Color.White);

            Raylib.EndDrawing();
        }

        public void Dispose()
        {
            _dispatcher?.Unsubscribe<NPC.Library.Messaging.DialogueGeneratedMessage>(OnDialogueGenerated);
            rlImGui.Shutdown();
            foreach (var tex in _textures.Values)
            {
                Raylib.UnloadTexture(tex);
            }
            Raylib.CloseWindow();
        }
    }
}
