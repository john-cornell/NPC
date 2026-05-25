namespace NPC.ConsoleUI.Map;

public class MapGrid
{
    public int Width { get; }
    public int Height { get; }
    public TileType[,] Tiles { get; }

    public MapGrid(int width, int height)
    {
        Width = width;
        Height = height;
        Tiles = new TileType[width, height];
    }
}
