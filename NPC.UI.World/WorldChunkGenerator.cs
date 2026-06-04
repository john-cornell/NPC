namespace NPC.UI.World;

using System;
using System.Collections.Generic;
using NPC.World.Map;
using NPC.Library.Spatial.Grid;
using NPC.Village.Map;

public class WorldChunkGenerator : IChunkGenerator
{
    private readonly MapGrid _villageMap;
    private readonly int _villageWidth;
    private readonly int _villageHeight;
    private readonly double _noiseOffsetX;
    private readonly double _noiseOffsetY;

    public WorldChunkGenerator(MapGrid villageMap, double noiseOffsetX = 0, double noiseOffsetY = 0)
    {
        _villageMap = villageMap;
        _villageWidth = villageMap.Width;
        _villageHeight = villageMap.Height;
        _noiseOffsetX = noiseOffsetX;
        _noiseOffsetY = noiseOffsetY;
    }

    public void GenerateChunk(MapChunk chunk)
    {
        int chunkStartX = chunk.X * 100;
        int chunkStartY = chunk.Y * 100;

        if (chunk.Z == -1)
        {
            // Procedurally generate dungeon/cave level
            GenerateDungeonLevel(chunk);
            return;
        }

        // Z == 0 (Surface)
        for (int localY = 0; localY < 100; localY++)
        {
            for (int localX = 0; localX < 100; localX++)
            {
                int globalX = chunkStartX + localX;
                int globalY = chunkStartY + localY;

                if (globalX >= 0 && globalX < _villageWidth && globalY >= 0 && globalY < _villageHeight)
                {
                    // Inside village
                    chunk.Tiles[localX, localY] = _villageMap.Tiles[globalX, globalY];
                    
                    if (_villageMap.Chests.TryGetValue((globalX, globalY), out var chest))
                    {
                        if (chest is NPC.Library.Inventory.StandardInventory stdChest)
                        {
                            chunk.Chests[(localX, localY)] = stdChest;
                        }
                    }
                    if (_villageMap.TreeApples.TryGetValue((globalX, globalY), out var apples))
                    {
                        chunk.TreeApples[(localX, localY)] = apples;
                    }
                }
                else
                {
                    // Procedural wilderness
                    double scale = 0.12;
                    double noise = PerlinNoise.Noise((globalX + _noiseOffsetX) * scale, (globalY + _noiseOffsetY) * scale);
                    
                    if (noise < -0.2)
                    {
                        chunk.Tiles[localX, localY] = TileType.Water;
                    }
                    else
                    {
                        chunk.Tiles[localX, localY] = TileType.Grass;
                        // 2% chance for a tree
                        if (Random.Shared.NextDouble() < 0.02)
                        {
                            chunk.Tiles[localX, localY] = TileType.AppleTree;
                            chunk.TreeApples[(localX, localY)] = Random.Shared.Next(3, 11);
                        }
                    }
                }
            }
        }

        // Force a cave entrance on the surface near the village (global 110, 50 -> chunk 1, 0, local 10, 50)
        // If this chunk contains global (110, 50)
        if (chunkStartX <= 110 && chunkStartX + 100 > 110 && chunkStartY <= 50 && chunkStartY + 100 > 50)
        {
            chunk.Tiles[110 - chunkStartX, 50 - chunkStartY] = TileType.CaveEntrance;
        }
    }

    private void GenerateDungeonLevel(MapChunk chunk)
    {
        // NetHack style room generation (simple BSP or random rooms)
        // Default to walls
        for (int y = 0; y < 100; y++)
        {
            for (int x = 0; x < 100; x++)
            {
                chunk.Tiles[x, y] = TileType.CaveWall;
            }
        }

        // Only generate a single set of rooms in the chunk containing the entrance (Chunk 1, 0)
        if (chunk.X != 1 || chunk.Y != 0)
        {
            return;
        }

        var random = new Random((chunk.X * 73856093) ^ (chunk.Y * 191919) ^ (chunk.Z * 88888));
        
        var rooms = new List<System.Drawing.Rectangle>();
        int maxRooms = 15;

        for (int i = 0; i < maxRooms; i++)
        {
            int w = random.Next(5, 15);
            int h = random.Next(5, 15);
            int x = random.Next(1, 100 - w - 1);
            int y = random.Next(1, 100 - h - 1);

            var newRoom = new System.Drawing.Rectangle(x, y, w, h);
            
            bool intersects = false;
            foreach (var r in rooms)
            {
                if (newRoom.IntersectsWith(r))
                {
                    intersects = true;
                    break;
                }
            }

            if (!intersects)
            {
                // Carve room
                for (int cy = newRoom.Y; cy < newRoom.Bottom; cy++)
                {
                    for (int cx = newRoom.X; cx < newRoom.Right; cx++)
                    {
                        chunk.Tiles[cx, cy] = TileType.CaveFloor;
                    }
                }

                if (rooms.Count > 0)
                {
                    // Carve corridor to previous room
                    var prev = rooms[^1];
                    int startX = newRoom.X + newRoom.Width / 2;
                    int startY = newRoom.Y + newRoom.Height / 2;
                    int endX = prev.X + prev.Width / 2;
                    int endY = prev.Y + prev.Height / 2;

                    // L-shaped corridor
                    int currentX = startX;
                    int currentY = startY;

                    while (currentX != endX)
                    {
                        chunk.Tiles[currentX, currentY] = TileType.CaveFloor;
                        currentX += Math.Sign(endX - currentX);
                    }
                    while (currentY != endY)
                    {
                        chunk.Tiles[currentX, currentY] = TileType.CaveFloor;
                        currentY += Math.Sign(endY - currentY);
                    }
                }

                rooms.Add(newRoom);
            }
        }

        // Place stairs up directly beneath the surface cave entrance
        // Surface cave entrance is at global (110, 50) -> chunk (1, 0, -1) -> local (10, 50)
        // Wait, chunk.X and chunk.Y could be anything.
        int chunkStartX = chunk.X * 100;
        int chunkStartY = chunk.Y * 100;

        if (chunkStartX <= 110 && chunkStartX + 100 > 110 && chunkStartY <= 50 && chunkStartY + 100 > 50)
        {
            int localStairsX = 110 - chunkStartX;
            int localStairsY = 50 - chunkStartY;
            chunk.Tiles[localStairsX, localStairsY] = TileType.StairsUp;
            
            // Clear area around stairs so player doesn't spawn in a wall
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (localStairsX + dx >= 0 && localStairsX + dx < 100 && localStairsY + dy >= 0 && localStairsY + dy < 100)
                    {
                        if (dx != 0 || dy != 0)
                        {
                            chunk.Tiles[localStairsX + dx, localStairsY + dy] = TileType.CaveFloor;
                        }
                    }
                }
            }
        }
    }
}
