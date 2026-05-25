namespace NPC.UI.Console;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NPC.Application;
using NPC.Library.Character;
using NPC.Library.Spatial;
using NPC.Library.Spatial.Grid;
using Spectre.Console;
using Spectre.Console.Rendering;
using DriveType = NPC.Library.Character.DriveType;

public class SpectreRenderer : IRenderer
{
    public IRenderable Build(UIState state)
    {
        var rule = new Rule($"[green]NPC Simulation Tick: {state.TickCount}[/]");
        rule.Justification = Justify.Center;

        IRenderable content = state.ViewMode == 0 ? BuildTableView(state) : BuildMapView(state);
        
        var controls = new Markup("\n[grey]Controls: [white]V[/] Toggle View | [white]Left/Right[/] Select Character | [white]Up/Down[/] Select Tree | [white]Q[/] Quit[/]");

        return new Rows(rule, content, controls);
    }

    private IRenderable BuildTableView(UIState state)
    {
        var table = new Table();
        table.AddColumn("Character ID");
        table.AddColumn("Pos (X,Y)");
        table.AddColumn("Action");
        table.AddColumn("Satiety");
        table.AddColumn("Thirst");
        table.AddColumn("Inv");
        table.AddColumn("Other Drives");

        int id = 1;
        foreach (var character in state.SpatialContext.GetCharacters())
        {
            var pos = state.SpatialContext.GetCharacterLocation(character).GetValueOrDefault((0, 0));
            var drives = character.Drives.Levels;
            
            string satietyStr = character.Drives.TryGetLevel(DriveType.Satiety, out var s) ? $"{s:P0}" : "-";
            string thirstStr = character.Drives.TryGetLevel(DriveType.Thirst, out var t) ? $"{t:P0}" : "-";

            string otherDrives = string.Join(", ", character.Drives.Levels
                .Where(d => d.Key != DriveType.Satiety && d.Key != DriveType.Thirst)
                .Select(d => $"{d.Key}: {d.Value:P0}"));

            string inventoryCount = "0";
            if (character.TryGetComponent<NPC.Library.Inventory.IInventory>(out var inv))
            {
                inventoryCount = inv.GetItems().Count().ToString();
            }

            string nameStr = state.SelectedCharacter == character ? $"[bold yellow]NPC {id}[/]" : $"NPC {id}";
            if (character.IsDead) nameStr = "[bold red]NPC " + id + " (DEAD)[/]";

            string actionStr = character.IsDead ? "[red]Dead[/]" : character.LastAction;

            table.AddRow(
                nameStr, 
                $"{pos.X},{pos.Y}", 
                actionStr,
                satietyStr, thirstStr, inventoryCount, otherDrives);
            id++;
        }

        return table;
    }

    private IRenderable BuildMapView(UIState state)
    {
        var gridContext = state.SpatialContext as GridSpatialContext;
        if (gridContext == null) return new Markup("[red]Map view only supports GridSpatialContext[/]");

        var map = gridContext.Map;
        var gridStr = new StringBuilder();

        // Create character lookup by position
        var charsByPos = new Dictionary<(int X, int Y), Character>();
        foreach (var character in state.SpatialContext.GetCharacters())
        {
            var pos = state.SpatialContext.GetCharacterLocation(character);
            if (pos != null) charsByPos[pos.Value] = character;
        }

        int startX = Math.Max(-1, Math.Min(state.CameraX, map.Width - state.CameraWidth + 1));
        int startY = Math.Max(-1, Math.Min(state.CameraY, map.Height - state.CameraHeight + 1));
        int endX = startX + state.CameraWidth;
        int endY = startY + state.CameraHeight;

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                if (x < 0 || x >= map.Width || y < 0 || y >= map.Height)
                {
                    gridStr.Append("[grey]#[/]");
                    continue;
                }

                if (charsByPos.TryGetValue((x, y), out var character))
                {
                    if (character.LastAction == "Sleeping in Bed")
                        gridStr.Append("[bold magenta]@[/]");
                    else if (character == state.SelectedCharacter)
                        gridStr.Append("[bold yellow]@[/]");
                    else
                        gridStr.Append("[bold white]@[/]");
                    continue;
                }

                var tile = map.Tiles[x, y];
                switch (tile)
                {
                    case TileType.Water:
                        gridStr.Append("[blue]~[/]");
                        break;
                    case TileType.Grass:
                        gridStr.Append("[green].[/]");
                        break;
                    case TileType.AppleTree:
                        if (state.SelectedTree.HasValue && state.SelectedTree.Value == (x, y))
                        {
                            gridStr.Append("[bold cyan]T[/]");
                        }
                        else
                        {
                            int apples = gridContext.GetAppleCount((x, y));
                            if (apples > 0)
                                gridStr.Append("[red]T[/]");
                            else
                                gridStr.Append("[grey]t[/]");
                        }
                        break;
                    case TileType.House:
                        gridStr.Append("[bold magenta]H[/]");
                        break;
                    case TileType.Road:
                        gridStr.Append("[bold yellow]+[/]");
                        break;
                    case TileType.Well:
                        gridStr.Append("[bold blue]O[/]");
                        break;
                    case TileType.Wall:
                        gridStr.Append("[grey]#[/]");
                        break;
                    case TileType.Floor:
                        gridStr.Append("[grey].[/]");
                        break;
                    case TileType.Door:
                        gridStr.Append("[bold yellow]D[/]");
                        break;
                    case TileType.Chest:
                        gridStr.Append("[bold gold3]C[/]");
                        break;
                    case TileType.Bed:
                        gridStr.Append("[bold magenta]B[/]");
                        break;
                }
            }
            gridStr.AppendLine();
        }

        var mapPanel = new Panel(gridStr.ToString())
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader("World Map")
        };

        var sidebarItems = new List<IRenderable>();

        if (state.SelectedCharacter != null)
        {
            var charsList = state.SpatialContext.GetCharacters().ToList();
            var charId = charsList.IndexOf(state.SelectedCharacter) + 1;
            var pos = state.SpatialContext.GetCharacterLocation(state.SelectedCharacter).GetValueOrDefault((0, 0));
            var drives = state.SelectedCharacter.Drives.Levels;
            
            var statsStr = new StringBuilder();
            
            string statusStr = state.SelectedCharacter.IsDead ? $"[bold red]DEAD ({state.SelectedCharacter.DeathReason} @ Tick {state.SelectedCharacter.DeathTick})[/]" : "ALIVE";
            statsStr.AppendLine($"[bold yellow]NPC {charId}[/] ({statusStr}) at ({pos.X}, {pos.Y})");
            
            string memoryType = "None";
            if (state.SelectedCharacter.TryGetComponent<NPC.Library.Memory.IMemory>(out var memory))
            {
                memoryType = memory.GetType().Name;
            }
            statsStr.AppendLine($"Memory Type: [cyan]{memoryType}[/]");

            string sightStr = "3 (Default)";
            if (state.SelectedCharacter.TryGetComponent<NPC.Library.Simulation.VisionComponent>(out var vision))
            {
                sightStr = vision.SightLength.ToString();
            }
            statsStr.AppendLine($"Sight Length: [cyan]{sightStr}[/]");

            string destStr = state.SelectedCharacter.CurrentDestination.HasValue ? 
                $"({state.SelectedCharacter.CurrentDestination.Value.X}, {state.SelectedCharacter.CurrentDestination.Value.Y})" : "None";
            statsStr.AppendLine($"Destination: [cyan]{destStr}[/]");
            statsStr.AppendLine($"Current Action: [cyan]{state.SelectedCharacter.LastAction}[/]");
            
            foreach (var drive in drives)
            {
                if (drive.Key == NPC.Library.Character.DriveType.Fatigue)
                {
                    var color = drive.Value > 0.8m ? "red" : (drive.Value > 0.4m ? "yellow" : "green");
                    statsStr.AppendLine($"{drive.Key}: [{color}]{drive.Value:P0}[/]");
                }
                else
                {
                    var color = drive.Value > 0.6m ? "green" : (drive.Value > 0.3m ? "yellow" : "red");
                    statsStr.AppendLine($"{drive.Key}: [{color}]{drive.Value:P0}[/]");
                }
            }

            statsStr.AppendLine();
            statsStr.AppendLine("[bold]Inventory[/]");
            statsStr.AppendLine();
            if (state.SelectedCharacter.TryGetComponent<NPC.Library.Inventory.IInventory>(out var inv))
            {
                var items = inv.GetItems().ToList();
                if (!items.Any()) statsStr.AppendLine("[grey]Empty[/]");
                
                var apples = items.Count(i => i.Type == NPC.Library.Inventory.ItemType.Apple);
                if (apples > 0) statsStr.AppendLine($"Apple: {apples}");
                
                var waterBottles = items.OfType<NPC.Library.Inventory.WaterBottleItem>().ToList();
                foreach (var bottle in waterBottles)
                {
                    statsStr.AppendLine($"Water Bottle: {bottle.SipsRemaining}/3 sips");
                }
            }
            else statsStr.AppendLine("[grey]No Inventory Component[/]");

            statsStr.AppendLine();
            statsStr.AppendLine("[bold]Memory[/]");
            statsStr.AppendLine();
            if (state.SelectedCharacter.TryGetComponent<NPC.Library.Memory.IMemory>(out var mem))
            {
                var treeCount = mem.Recall(TileType.AppleTree).Count();
                var waterCount = mem.Recall(TileType.Water).Count();
                statsStr.AppendLine($"Apple Trees: {treeCount}");
                statsStr.AppendLine($"Water: {waterCount}");

                if (mem is NPC.Village.Memory.VillageMemory)
                {
                    statsStr.AppendLine();
                    statsStr.AppendLine("[bold]Home Chest[/]");
                    statsStr.AppendLine();
                    var chestLocs = mem.Recall(TileType.Chest).ToList();
                    if (chestLocs.Any() && gridContext != null && gridContext.Map.Chests.TryGetValue(chestLocs[0], out var chestInv))
                    {
                        var chestItems = chestInv.GetItems().ToList();
                        if (!chestItems.Any()) statsStr.AppendLine("[grey]Empty[/]");
                        
                        var cApples = chestItems.Count(i => i.Type == NPC.Library.Inventory.ItemType.Apple);
                        if (cApples > 0) statsStr.AppendLine($"Apple: {cApples}");
                        
                        var cWaterBottles = chestItems.OfType<NPC.Library.Inventory.WaterBottleItem>().ToList();
                        foreach (var bottle in cWaterBottles)
                        {
                            statsStr.AppendLine($"Water Bottle: {bottle.SipsRemaining}/3 sips");
                        }
                    }
                    else
                    {
                        statsStr.AppendLine("[grey]Unknown/Missing Chest[/]");
                    }
                }
            }
            else statsStr.AppendLine("[grey]No Memory Component[/]");

            // Anti-jitter: pad vertical height
            var infoLines = statsStr.ToString().Replace("\r", "").TrimEnd().Split('\n').ToList();
            if (infoLines.Count > state.MaxInfoHeight) state.MaxInfoHeight = infoLines.Count;
            while (infoLines.Count < state.MaxInfoHeight) infoLines.Add("");
            
            // Anti-jitter: track max width
            int maxW = infoLines.Max(l => Markup.Remove(l).Length);
            if (maxW > state.MaxInfoWidth) state.MaxInfoWidth = maxW;
            
            // Pad longest line so the panel holds its width
            for (int i=0; i<infoLines.Count; i++)
            {
                int len = Markup.Remove(infoLines[i]).Length;
                if (len < state.MaxInfoWidth) infoLines[i] += new string(' ', state.MaxInfoWidth - len);
            }

            var statsPanel = new Panel(string.Join("\n", infoLines))
            {
                Border = BoxBorder.Rounded,
                Header = new PanelHeader("Selected Character")
            };
            sidebarItems.Add(statsPanel);
        }

        if (state.SelectedTree.HasValue)
        {
            var treePos = state.SelectedTree.Value;
            var apples = gridContext.GetAppleCount(treePos);
            
            var treeStr = new StringBuilder();
            treeStr.AppendLine($"[bold cyan]Apple Tree[/] at ({treePos.X}, {treePos.Y})");
            treeStr.AppendLine($"Apples Remaining: [red]{apples}[/]/10");
            
            if (apples == 0) treeStr.AppendLine("\n[grey](Depleted - Slowly Regrowing)[/]");
            
            var treePanel = new Panel(treeStr.ToString())
            {
                Border = BoxBorder.Rounded,
                Header = new PanelHeader("Selected Tree")
            };
            sidebarItems.Add(treePanel);
        }

        var charsStr = new StringBuilder();
        int cid = 1;
        foreach (var character in state.SpatialContext.GetCharacters())
        {
            string stateStr = character.IsDead ? $"[red]DEAD ({character.DeathReason} @ Tick {character.DeathTick})[/]" : $"[green]{character.LastAction}[/]";
            string nameStr = state.SelectedCharacter == character ? $"[bold yellow]> NPC {cid}[/]" : $"  NPC {cid}";
            charsStr.AppendLine($"{nameStr} - {stateStr}");
            cid++;
        }

        var charLines = charsStr.ToString().Replace("\r", "").TrimEnd().Split('\n').ToList();
        if (charLines.Count > state.MaxCharsHeight) state.MaxCharsHeight = charLines.Count;
        while (charLines.Count < state.MaxCharsHeight) charLines.Add("");

        int maxCW = charLines.Max(l => Markup.Remove(l).Length);
        if (maxCW > state.MaxCharsWidth) state.MaxCharsWidth = maxCW;

        for (int i=0; i<charLines.Count; i++)
        {
            int len = Markup.Remove(charLines[i]).Length;
            if (len < state.MaxCharsWidth) charLines[i] += new string(' ', state.MaxCharsWidth - len);
        }

        var charsPanel = new Panel(string.Join("\n", charLines))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader("Characters")
        };

        if (sidebarItems.Count > 0)
        {
            var grid = new Grid();
            grid.AddColumn(new GridColumn());
            grid.AddColumn(new GridColumn().PadLeft(2));
            grid.AddColumn(new GridColumn().PadLeft(2));
            grid.AddRow(mapPanel, new Rows(sidebarItems), charsPanel);
            
            return grid;
        }
        
        var fallbackGrid = new Grid();
        fallbackGrid.AddColumn(new GridColumn());
        fallbackGrid.AddColumn(new GridColumn().PadLeft(2));
        fallbackGrid.AddRow(mapPanel, charsPanel);
        return fallbackGrid;
    }
}
