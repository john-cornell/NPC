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
using NPC.World.Map;

namespace NPC.UI.World
{
    public class WorldRenderer : IDisposable
    {
        private Dictionary<string, Texture2D> _textures = new();
        private string _globalTestStatus = "";
        private Camera2D _camera;
        private UIOverlay _uiOverlay = new();
        private int _currentZLayer = 0;
        private int _lastTrackedZ = 0;
        
        // Log Deletion State
        private int _logDeleteMode = 2; // 0=All(1), 1=Keep1, 2=Keep2, 3=Keep5
        private string _deleteStatus = "";
        
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
            Raylib.InitWindow(1280, 720, "NPC World Simulation");
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

        private void DrawIsoSprite(string spriteName, int gridX, int gridY, float yOffset = 0, float scale = 1.0f, bool flipH = false, Color? tint = null)
        {
            if (!_textures.TryGetValue(spriteName, out var tex)) return;

            Vector2 pos = GridToIso(gridX, gridY);
            
            // Adjust position so the bottom center of the sprite aligns with the iso coordinate
            float destX = pos.X - (tex.Width * scale) / 2.0f;
            float destY = pos.Y - (tex.Height * scale) + (TileHeight / 2.0f) + yOffset;

            Rectangle sourceRec = new Rectangle(0, 0, flipH ? -tex.Width : tex.Width, tex.Height);
            Rectangle destRec = new Rectangle(destX, destY, tex.Width * scale, tex.Height * scale);
            Raylib.DrawTexturePro(tex, sourceRec, destRec, new Vector2(0, 0), 0.0f, tint ?? Color.White);
        }

        private bool _cameraInitialized = false;

        public void Render(UIState state)
        {
            if (state.SpatialContext is not NPC.World.Map.WorldMap worldMap) return;

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
                bool movedCamera = false;
                if (Raylib.IsKeyDown(KeyboardKey.Up) || Raylib.IsKeyDown(KeyboardKey.Kp8)) { _camera.Target.Y -= 10.0f / _camera.Zoom; movedCamera = true; }
                if (Raylib.IsKeyDown(KeyboardKey.Down) || Raylib.IsKeyDown(KeyboardKey.Kp2)) { _camera.Target.Y += 10.0f / _camera.Zoom; movedCamera = true; }
                if (Raylib.IsKeyDown(KeyboardKey.Left) || Raylib.IsKeyDown(KeyboardKey.Kp4)) { _camera.Target.X -= 10.0f / _camera.Zoom; movedCamera = true; }
                if (Raylib.IsKeyDown(KeyboardKey.Right) || Raylib.IsKeyDown(KeyboardKey.Kp6)) { _camera.Target.X += 10.0f / _camera.Zoom; movedCamera = true; }
                
                if (Raylib.IsKeyPressed(KeyboardKey.PageUp)) _currentZLayer++;
                if (Raylib.IsKeyPressed(KeyboardKey.PageDown)) _currentZLayer--;
                
                if (Raylib.IsKeyDown(KeyboardKey.KpSubtract)) _camera.Zoom -= 0.02f;
                if (Raylib.IsKeyDown(KeyboardKey.KpAdd)) _camera.Zoom += 0.02f;

                float wheel = Raylib.GetMouseWheelMove();
                if (wheel != 0)
                {
                    _camera.Zoom += wheel * 0.1f;
                }

                if (Raylib.IsMouseButtonDown(MouseButton.Left))
                {
                    Vector2 delta = Raylib.GetMouseDelta();
                    if (delta.X != 0 || delta.Y != 0)
                    {
                        _camera.Target.X -= delta.X / _camera.Zoom;
                        _camera.Target.Y -= delta.Y / _camera.Zoom;
                        movedCamera = true;
                    }
                }

                if (movedCamera)
                {
                    state.LockCameraToSelectedCharacter = false;
                }

                if (Raylib.IsKeyPressed(KeyboardKey.C))
                {
                    state.SelectedCharacter = state.PlayerCharacter;
                    state.LockCameraToSelectedCharacter = true;
                }

                if (Raylib.IsKeyPressed(KeyboardKey.Kp5) && state.WellLocation.HasValue)
                {
                    _camera.Target = GridToIso(state.WellLocation.Value.X, state.WellLocation.Value.Y);
                    state.LockCameraToSelectedCharacter = false;
                }

                if (_camera.Zoom < 0.1f) _camera.Zoom = 0.1f;
                if (_camera.Zoom > 5.0f) _camera.Zoom = 5.0f;
            }

            Character trackChar = state.SelectedCharacter ?? state.PlayerCharacter;
            if (trackChar != null)
            {
                var loc = state.SpatialContext.GetCharacterLocation(trackChar);
                if (loc.HasValue)
                {
                    if (loc.Value.Z != _lastTrackedZ)
                    {
                        _currentZLayer = loc.Value.Z;
                        _lastTrackedZ = loc.Value.Z;
                        state.LockCameraToSelectedCharacter = true; // Auto-lock camera on Z-transition
                    }
                    
                    if (state.LockCameraToSelectedCharacter)
                    {
                        _camera.Target = GridToIso(loc.Value.X, loc.Value.Y);
                    }
                }
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(30, 30, 40, 255));

            Raylib.BeginMode2D(_camera);

            // Dynamically load chunks based on camera bounds
            Vector2 screenTL = Raylib.GetScreenToWorld2D(new Vector2(0, 0), _camera);
            Vector2 screenTR = Raylib.GetScreenToWorld2D(new Vector2(Raylib.GetScreenWidth(), 0), _camera);
            Vector2 screenBL = Raylib.GetScreenToWorld2D(new Vector2(0, Raylib.GetScreenHeight()), _camera);
            Vector2 screenBR = Raylib.GetScreenToWorld2D(new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight()), _camera);

            float GetGridX(Vector2 iso) => (iso.Y / (TileHeight / 2.0f) + iso.X / (TileWidth / 2.0f)) / 2.0f;
            float GetGridY(Vector2 iso) => (iso.Y / (TileHeight / 2.0f) - iso.X / (TileWidth / 2.0f)) / 2.0f;

            float minGridX = Math.Min(Math.Min(GetGridX(screenTL), GetGridX(screenTR)), Math.Min(GetGridX(screenBL), GetGridX(screenBR)));
            float maxGridX = Math.Max(Math.Max(GetGridX(screenTL), GetGridX(screenTR)), Math.Max(GetGridX(screenBL), GetGridX(screenBR)));
            float minGridY = Math.Min(Math.Min(GetGridY(screenTL), GetGridY(screenTR)), Math.Min(GetGridY(screenBL), GetGridY(screenBR)));
            float maxGridY = Math.Max(Math.Max(GetGridY(screenTL), GetGridY(screenTR)), Math.Max(GetGridY(screenBL), GetGridY(screenBR)));

            int margin = 5;
            int startX = (int)Math.Floor(minGridX) - margin;
            int endX = (int)Math.Ceiling(maxGridX) + margin;
            int startY = (int)Math.Floor(minGridY) - margin;
            int endY = (int)Math.Ceiling(maxGridY) + margin;

            int startChunkX = (int)Math.Floor(startX / 100.0);
            int endChunkX = (int)Math.Floor(endX / 100.0);
            int startChunkY = (int)Math.Floor(startY / 100.0);
            int endChunkY = (int)Math.Floor(endY / 100.0);

            var visibleChunks = new List<MapChunk>();
            for (int cx = startChunkX; cx <= endChunkX; cx++)
            {
                for (int cy = startChunkY; cy <= endChunkY; cy++)
                {
                    visibleChunks.Add(worldMap.ChunkManager.LoadChunk(cx, cy, _currentZLayer, DateTime.MinValue));
                }
            }

            // Calculate Lighting based on Time of Day
            Color ambientLight = Color.White;
            double hour = state.CurrentWorldTime.TimeOfDay.TotalHours;
            
            if (hour >= 20 || hour < 5) 
            {
                // Night (8 PM to 5 AM)
                ambientLight = new Color(50, 50, 100, 255);
            }
            else if (hour >= 5 && hour < 7)
            {
                // Dawn (5 AM to 7 AM)
                float t = (float)((hour - 5) / 2.0);
                ambientLight = new Color((int)(50 + (205 * t)), (int)(50 + (205 * t)), (int)(100 + (155 * t)), 255);
            }
            else if (hour >= 18 && hour < 20)
            {
                // Dusk (6 PM to 8 PM)
                float t = (float)((hour - 18) / 2.0);
                ambientLight = new Color((int)(255 - (205 * t)), (int)(255 - (205 * t)), (int)(255 - (155 * t)), 255);
            }

            // Draw Terrain First (Bottom Layer)
            foreach (var chunk in visibleChunks)
            {
                for (int localY = 0; localY < 100; localY++)
                {
                    for (int localX = 0; localX < 100; localX++)
                    {
                        int globalX = (chunk.X * 100) + localX;
                        int globalY = (chunk.Y * 100) + localY;
                        
                        string texName = "iso_grass_tile";
                        bool flipTerrain = false;

                        if (chunk.Tiles[localX, localY] == TileType.Wall || chunk.Tiles[localX, localY] == TileType.CaveWall)
                        {
                            bool hasNegX = (localX > 0 && (chunk.Tiles[localX - 1, localY] == TileType.Wall || chunk.Tiles[localX - 1, localY] == TileType.Door || chunk.Tiles[localX - 1, localY] == TileType.CaveWall));
                            bool hasPosX = (localX < 99 && (chunk.Tiles[localX + 1, localY] == TileType.Wall || chunk.Tiles[localX + 1, localY] == TileType.Door || chunk.Tiles[localX + 1, localY] == TileType.CaveWall));
                            bool hasNegY = (localY > 0 && (chunk.Tiles[localX, localY - 1] == TileType.Wall || chunk.Tiles[localX, localY - 1] == TileType.Door || chunk.Tiles[localX, localY - 1] == TileType.CaveWall));
                            bool hasPosY = (localY < 99 && (chunk.Tiles[localX, localY + 1] == TileType.Wall || chunk.Tiles[localX, localY + 1] == TileType.Door || chunk.Tiles[localX, localY + 1] == TileType.CaveWall));
                            
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
                                if (hasXNeighbor) flipTerrain = true; 
                            }
                        }
                        else
                        {
                            texName = chunk.Tiles[localX, localY] switch
                            {
                                TileType.Water => "iso_water_tile",
                                TileType.Road => "iso_road_tile",
                                TileType.Floor => "iso_floor_tile",
                                TileType.Bed => "iso_floor_tile",
                                TileType.Chest => "iso_floor_tile",
                                TileType.Door => "iso_floor_tile",
                                TileType.CaveFloor => "iso_floor_tile",
                                _ => "iso_grass_tile"
                            };
                        }
                        
                        DrawIsoSprite(texName, globalX, globalY, yOffset: 16f, scale: 0.5f, flipH: flipTerrain, tint: ambientLight);
                    }
                }
            }

            // Draw Objects (Middle Layer)
            foreach (var chunk in visibleChunks)
            {
                for (int localY = 0; localY < 100; localY++)
                {
                    for (int localX = 0; localX < 100; localX++)
                    {
                        int globalX = (chunk.X * 100) + localX;
                        int globalY = (chunk.Y * 100) + localY;

                        string objTex = chunk.Tiles[localX, localY] switch
                        {
                            TileType.AppleTree => chunk.TreeApples.TryGetValue((localX, localY), out int count) && count > 0 ? "iso_tree_apple" : "iso_tree_empty",
                            TileType.Door => "iso_door_tile",
                            TileType.Chest => "iso_chest_tile",
                            TileType.Bed => "iso_bed_tile",
                            TileType.Well => "iso_well_tile",
                            TileType.CaveEntrance => "iso_cave_entrance",
                            TileType.StairsUp => "iso_road_tile",
                            _ => null
                        };

                        if (objTex != null)
                        {
                            bool flipObj = false;
                            if (chunk.Tiles[localX, localY] == TileType.Door)
                            {
                                objTex = "iso_door_x";
                                if ((localX > 0 && chunk.Tiles[localX - 1, localY] == TileType.Wall) || 
                                    (localX < 99 && chunk.Tiles[localX + 1, localY] == TileType.Wall))
                                {
                                    flipObj = true;
                                }
                            }
                            DrawIsoSprite(objTex, globalX, globalY, yOffset: 16f, scale: 0.5f, flipH: flipObj, tint: ambientLight);
                        }

                        if (chunk.GroundItems.TryGetValue((localX, localY), out var groundItems) && groundItems.Count > 0)
                        {
                            var topItem = groundItems.Last();
                            string groundTex = topItem.Type switch
                            {
                                NPC.Library.Inventory.ItemType.Dagger => "iso_dagger",
                                NPC.Library.Inventory.ItemType.LightHealingPotion => "iso_light_healing_potion",
                                NPC.Library.Inventory.ItemType.Gold => "iso_gold",
                                _ => "iso_road_tile"
                            };
                            DrawIsoSprite(groundTex, globalX, globalY, yOffset: 8f, scale: 0.35f, flipH: false, tint: ambientLight);
                        }
                    }
                }
            }

            // Draw Characters (Top Layer)
            // Code-driven bouncy animation based on time
            float time = (float)Raylib.GetTime();
            
            bool clickedThisFrame = Raylib.IsMouseButtonPressed(MouseButton.Left) && !ImGui.GetIO().WantCaptureMouse;
            Vector2 mouseWorld = Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), _camera);

            int charIdx = 0;
            // Draw dead characters first, then NPCs, then the Player on top
            var sortedChars = state.SpatialContext.GetCharacters().OrderBy(c => c.IsDead ? 0 : (c == state.PlayerCharacter ? 2 : 1)).ToList();
            foreach (var character in sortedChars)
            {
                var pos = state.SpatialContext.GetCharacterLocation(character);
                if (pos.HasValue && pos.Value.Z == _currentZLayer)
                {
                    // If moving, make them bounce using a sine wave
                    float bounceOffset = 0f;
                    if (character.LastAction != null && (character.LastAction.Contains("Moving") || character.LastAction.Contains("Wandering") || character.LastAction.Contains("Walking") || character.LastAction.Contains("Fleeing")))
                    {
                        bounceOffset = (float)Math.Abs(Math.Sin(time * 15.0f)) * -10.0f; // Bounces up by 10 pixels
                    }
                    
                    string charTex = (Math.Abs(character.Name.GetHashCode()) % 2 == 0) ? "iso_villager_farmer" : "iso_villager_baker";
                    float scale = 0.5f;
                    float yOffset = bounceOffset; // Centered in the tile

                    if (character is NPC.Library.Character.Animal animal)
                    {
                        if (animal.AnimalType == NPC.Library.Character.AnimalType.Sheep)
                        {
                            charTex = "iso_sheep";
                            if (animal.IsDead) charTex = "iso_dead_sheep";
                            else if (character.LastAction != null && character.LastAction.Contains("Sleeping")) charTex = "iso_sheep_sleeping";
                        }
                        else if (animal.AnimalType == NPC.Library.Character.AnimalType.Fox)
                        {
                            charTex = "iso_fox";
                            if (animal.IsDead) charTex = "iso_gravestone"; // Foxes just turn into graves for now
                            else if (character.LastAction != null && character.LastAction.Contains("Sleeping")) charTex = "iso_fox_sleeping";
                        }
                    }
                    else if (character.Name.Contains("Goblin"))
                    {
                        charTex = "iso_goblin";
                        if (character.IsDead) charTex = "iso_dead_goblin";
                        else if (character.LastAction != null && character.LastAction.Contains("Sleeping")) 
                        {
                            charTex = "iso_goblin_sleeping";
                            yOffset = 0f;
                        }
                    }
                    else
                    {
                        if (character.IsDead)
                        {
                            charTex = "iso_gravestone";
                            scale = 0.5f;
                            yOffset = 0f;
                        }
                        else if (character.LastAction != null && character.LastAction.Contains("Sleeping"))
                        {
                            charTex = (Math.Abs(character.Name.GetHashCode()) % 2 == 0) ? "iso_villager_farmer_sleeping" : "iso_villager_baker_sleeping";
                            scale = 0.5f;
                            yOffset = 0f; // Centered
                        }
                    }
                    
                    if (!_textures.TryGetValue(charTex, out var tex)) continue;
                    
                    Vector2 isoPos = GridToIso(pos.Value.X, pos.Value.Y);
                    float destX = isoPos.X - (tex.Width * scale) / 2.0f;
                    float destY = isoPos.Y - (tex.Height * scale) + (TileHeight / 2.0f) + yOffset;

                    Color charTint = ambientLight;
                    if (character == state.PlayerCharacter)
                    {
                        // Distinct tint for the player character
                        charTint = new Color(
                            Math.Min(255, ambientLight.R + 50),
                            Math.Min(255, ambientLight.G + 50),
                            255, 
                            255);
                    }

                    Raylib.DrawTextureEx(tex, new Vector2(destX, destY), 0.0f, scale, charTint);

                    // Render combat flair removed as per user request

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
                    if (character == state.PlayerCharacter) displayName = "★ PLAYER ★";
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
                        ImGui.Text("Arrows/Numpad (8,4,2,6): Move Camera");
                        ImGui.Text("Left Click + Drag: Pan Camera");
                        ImGui.Text("Mouse Wheel or Numpad +/-: Zoom In/Out");
                        ImGui.Text("C: Center Camera on Player");
                        ImGui.Text("Numpad 5: Center Camera on Town");
                        ImGui.Text("ESC: Quit");
                        ImGui.Separator();
                        
                        bool lockCam = state.LockCameraToSelectedCharacter;
                        if (ImGui.Checkbox("Lock Camera to Selected Character", ref lockCam))
                        {
                            state.LockCameraToSelectedCharacter = lockCam;
                        }

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
                    if (ImGui.BeginTabItem("Data Management"))
                    {
                        ImGui.Text("Genetic Training Logs");
                        ImGui.Separator();

                        ImGui.Combo("Deletion Mode", ref _logDeleteMode, "All (Except Current)\0Keep Latest 1\0Keep Latest 2\0Keep Latest 5\0");
                        
                        if (ImGui.Button("Delete Logs"))
                        {
                            _deleteStatus = "Deleting...";
                            System.Threading.Tasks.Task.Run(() => {
                                try {
                                    string logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NPC", "GeneticLogs");
                                    if (Directory.Exists(logsDir))
                                    {
                                        var runDirs = Directory.GetDirectories(logsDir).OrderByDescending(d => d).ToList();
                                        
                                        // Determine how many of the newest runs to KEEP
                                        int skipCount = _logDeleteMode switch {
                                            0 => 1, // All (Except Current)
                                            1 => 1, // Keep Latest 1
                                            2 => 2, // Keep Latest 2
                                            3 => 5, // Keep Latest 5
                                            _ => 2
                                        };

                                        var toDelete = runDirs.Skip(skipCount).ToList();
                                        foreach (var dir in toDelete)
                                        {
                                            Directory.Delete(dir, true);
                                        }
                                        
                                        _deleteStatus = $"Deleted {toDelete.Count} old log folders.";
                                    }
                                    else
                                    {
                                        _deleteStatus = "Logs directory not found.";
                                    }
                                } catch (Exception ex) {
                                    _deleteStatus = $"Error: {ex.Message}";
                                }
                            });
                        }
                        
                        if (!string.IsNullOrEmpty(_deleteStatus))
                        {
                            ImGui.TextWrapped(_deleteStatus);
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
            
            // Time & Speed HUD
            int relativeDay = (state.CurrentWorldTime - state.StartWorldTime).Days + 1;
            string timeStr = state.IsTrainerMode 
                ? $"Time: {state.CurrentWorldTime:HH:mm} (Day {relativeDay}) [Gen {state.CurrentGeneration}]"
                : $"Time: {state.CurrentWorldTime:HH:mm} (Day {relativeDay})";
            string speedStr = $"Speed: {state.CurrentTimeScale:0.0}x (Press 1-6 to change)";
            
            int timeWidth = Raylib.MeasureText(timeStr, 20);
            int speedWidth = Raylib.MeasureText(speedStr, 20);
            int hudWidth = Math.Max(timeWidth, speedWidth) + 20;
            
            int screenWidth = Raylib.GetScreenWidth();
            
            Raylib.DrawRectangle(screenWidth - hudWidth - 10, 10, hudWidth, 60, new Color(0, 0, 0, 150));
            Raylib.DrawText(timeStr, screenWidth - timeWidth - 20, 20, 20, Color.White);
            Raylib.DrawText(speedStr, screenWidth - speedWidth - 20, 45, 20, Color.Gold);

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
