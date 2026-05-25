namespace NPC.Village.Map;

using System;
using System.Collections.Generic;
using System.Linq;
using NPC.Library.Spatial.Grid;

/// <summary>
/// Generates a compact village map with houses clustered near the centre and
/// NetHack-inspired L-shaped road corridors connecting them.
/// </summary>
public static class VillageMapGenerator
{
    /// <summary>
    /// Generates a grid, places the village with a well and houses, and returns a list of house locations so 
    /// callers can assign a BedComponent to each NPC.
    /// </summary>
    public static MapGrid Generate(int width, int height, int npcCount, out List<(int X, int Y)> bedLocations, out List<(int X, int Y)> doorLocations, out List<(int X, int Y)> chestLocations, out (int X, int Y) wellLocation, Random? rng = null)
    {
        rng ??= new Random(1337);
        var map = new MapGrid(width, height);

        // 1. Fill with grass
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                map.Tiles[x, y] = TileType.Grass;

        // 2. Scatter trees lightly across map (village has cleared land in center)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // More trees near the edges, sparse in the village center
                double distFromCenterX = Math.Abs(x - width / 2.0) / (width / 2.0);
                double distFromCenterY = Math.Abs(y - height / 2.0) / (height / 2.0);
                double treeChance = 0.01 + 0.08 * Math.Max(distFromCenterX, distFromCenterY);

                if (rng.NextDouble() < treeChance)
                {
                    map.Tiles[x, y] = TileType.AppleTree;
                    map.TreeApples[(x, y)] = rng.Next(3, 11);
                }
            }
        }

        // 3. Add some water bodies using Perlin noise, biased to the edges
        double scale = 0.12;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double distFromCenter = Math.Max(
                    Math.Abs(x - width / 2.0) / (width / 2.0),
                    Math.Abs(y - height / 2.0) / (height / 2.0));

                if (distFromCenter > 0.55)
                {
                    double noise = PerlinNoise.Noise(x * scale, y * scale);
                    if (noise < -0.1)
                        map.Tiles[x, y] = TileType.Water;
                }
            }
        }

        // 4. Place a well in the center of the village
        bedLocations = new List<(int X, int Y)>();
        doorLocations = new List<(int X, int Y)>();
        chestLocations = new List<(int X, int Y)>();
        int clusterW = Math.Min(width - 10, 60);
        int clusterH = Math.Min(height - 10, 40);
        int clusterX = (width - clusterW) / 2;
        int clusterY = (height - clusterH) / 2;

        wellLocation = (clusterX + clusterW / 2, clusterY + clusterH / 2);
        map.Tiles[wellLocation.X, wellLocation.Y] = TileType.Well;
        map.WellLocation = wellLocation;

        // 5. Place houses in a cluster around the centre
        int attempts = 0;
        while (bedLocations.Count < npcCount && attempts < 10000)
        {
            attempts++;
            int hw = rng.Next(5, 9);
            int hh = rng.Next(5, 9);
            int hx = clusterX + rng.Next(0, Math.Max(1, clusterW - hw));
            int hy = clusterY + rng.Next(0, Math.Max(1, clusterH - hh));

            bool clear = true;
            for(int y = hy - 2; y < hy + hh + 2; y++)
            {
                for(int x = hx - 2; x < hx + hw + 2; x++)
                {
                    if (x < 0 || x >= width || y < 0 || y >= height) { clear = false; break; }
                    if (map.Tiles[x,y] == TileType.Wall || map.Tiles[x,y] == TileType.Door || map.Tiles[x,y] == TileType.Water || map.Tiles[x,y] == TileType.Well) { clear = false; break; }
                }
                if (!clear) break;
            }
            if (!clear) continue;

            for(int y = hy; y < hy + hh; y++)
            {
                for(int x = hx; x < hx + hw; x++)
                {
                    if (x == hx || x == hx + hw - 1 || y == hy || y == hy + hh - 1)
                        map.Tiles[x,y] = TileType.Wall;
                    else
                        map.Tiles[x,y] = TileType.Floor;
                }
            }



            int bx = hx + 1;
            int by = hy + 1;
            map.Tiles[bx, by] = TileType.Bed;
            bedLocations.Add((bx, by));

            int cx = hx + hw - 2;
            int cy = hy + 1;
            map.Tiles[cx, cy] = TileType.Chest;
            chestLocations.Add((cx, cy));
            map.Chests[(cx, cy)] = new NPC.Library.Inventory.StandardInventory(5);
        }

        // 6. Connect houses and the well with NetHack-style L-shaped road corridors
        //    Build a Minimum Spanning Tree (Prim's) so everything is connected
        // 6. Connect houses and the well with roads
        //    Build a Minimum Spanning Tree (Prim's) so everything is connected
        var allNodes = new List<(int X, int Y)>(bedLocations);
        allNodes.Add(wellLocation);

        if (allNodes.Count > 1)
        {
            var mst = BuildMST(allNodes);
            foreach (var (a, b) in mst)
                DigCorridorAStar(map, a, b, doorLocations);
        }

        return map;
    }

    /// <summary>
    /// Prim's algorithm to find a Minimum Spanning Tree of the house nodes
    /// using Manhattan distance as edge weight.
    /// </summary>
    private static List<((int X, int Y) A, (int X, int Y) B)> BuildMST(List<(int X, int Y)> nodes)
    {
        var inMST = new HashSet<(int X, int Y)> { nodes[0] };
        var edges = new List<((int X, int Y) A, (int X, int Y) B)>();

        while (inMST.Count < nodes.Count)
        {
            int bestDist = int.MaxValue;
            (int X, int Y) bestFrom = default, bestTo = default;

            foreach (var a in inMST)
            {
                foreach (var b in nodes)
                {
                    if (inMST.Contains(b)) continue;
                    int dist = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestFrom = a;
                        bestTo = b;
                    }
                }
            }

            inMST.Add(bestTo);
            edges.Add((bestFrom, bestTo));
        }

        return edges;
    }

    private static void DigCorridorAStar(MapGrid map, (int X, int Y) start, (int X, int Y) end, List<(int X, int Y)> doorLocations)
    {
        var openSet = new PriorityQueue<(int X, int Y), int>();
        var cameFrom = new Dictionary<(int X, int Y), (int X, int Y)>();
        var gScore = new Dictionary<(int X, int Y), int>();

        openSet.Enqueue(start, 0);
        gScore[start] = 0;

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current == end)
            {
                var path = new List<(int X, int Y)>();
                while (cameFrom.ContainsKey(current))
                {
                    path.Add(current);
                    current = cameFrom[current];
                }
                
                foreach (var p in path)
                {
                    if (map.Tiles[p.X, p.Y] == TileType.Grass)
                    {
                        map.Tiles[p.X, p.Y] = TileType.Road;
                    }
                    else if (map.Tiles[p.X, p.Y] == TileType.Wall)
                    {
                        map.Tiles[p.X, p.Y] = TileType.Door;
                        if (!doorLocations.Contains(p)) doorLocations.Add(p);
                    }
                }
                return;
            }

            var candidates = new[] { 
                (current.X, current.Y - 1), 
                (current.X, current.Y + 1), 
                (current.X - 1, current.Y), 
                (current.X + 1, current.Y) 
            };

            foreach (var neighbor in candidates)
            {
                if (neighbor.Item1 >= 0 && neighbor.Item1 < map.Width && neighbor.Item2 >= 0 && neighbor.Item2 < map.Height)
                {
                    int cost = GetTerrainCost(map.Tiles[neighbor.Item1, neighbor.Item2]);
                    int tentativeGScore = gScore[current] + cost;

                    if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        int fScore = tentativeGScore + Math.Abs(neighbor.Item1 - end.X) + Math.Abs(neighbor.Item2 - end.Y);
                        openSet.Enqueue(neighbor, fScore);
                    }
                }
            }
        }
    }

    private static int GetTerrainCost(TileType type)
    {
        return type switch
        {
            TileType.Road => 1,
            TileType.Grass => 5,
            TileType.Floor => 10,
            TileType.Door => 10,
            TileType.House => 10,
            TileType.Wall => 500, // Expensive, but allows breaking exactly one wall to exit a house
            TileType.Bed => 1000,
            TileType.Chest => 1000,
            TileType.AppleTree => 1000,
            TileType.Well => 1000,
            TileType.Water => 5000, // Almost impossible
            _ => 10
        };
    }
}
