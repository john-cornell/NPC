namespace NPC.Library.Spatial.Grid;

using System;

public static class MapGenerator
{
    public static MapGrid Generate(int width, int height, double scale = 0.1, double waterThreshold = -0.1, double treeChance = 0.05)
    {
        var map = new MapGrid(width, height);
        var random = new Random(42);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Perlin output is loosely -1 to 1 (usually closer to -0.5 to 0.5)
                double noiseVal = PerlinNoise.Noise(x * scale, y * scale);
                
                if (noiseVal < waterThreshold)
                {
                    map.Tiles[x, y] = TileType.Water;
                }
                else
                {
                    if (random.NextDouble() < treeChance)
                    {
                        map.Tiles[x, y] = TileType.AppleTree;
                        map.TreeApples[(x, y)] = random.Next(3, 11); // 3 to 10 apples
                    }
                    else
                    {
                        map.Tiles[x, y] = TileType.Grass;
                    }
                }
            }
        }

        return map;
    }
}
