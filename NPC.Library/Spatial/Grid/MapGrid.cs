namespace NPC.Library.Spatial.Grid;

public class MapGrid
{
    public int Width { get; }
    public int Height { get; }
    public TileType[,] Tiles { get; }
    public System.Collections.Generic.Dictionary<(int X, int Y), int> TreeApples { get; }
    public System.Collections.Generic.Dictionary<(int X, int Y), NPC.Library.Inventory.IInventory> Chests { get; }
    public (int X, int Y) WellLocation { get; set; }

    public MapGrid(int width, int height)
    {
        Width = width;
        Height = height;
        Tiles = new TileType[width, height];
        TreeApples = new System.Collections.Generic.Dictionary<(int X, int Y), int>();
        Chests = new System.Collections.Generic.Dictionary<(int X, int Y), NPC.Library.Inventory.IInventory>();
    }
}
