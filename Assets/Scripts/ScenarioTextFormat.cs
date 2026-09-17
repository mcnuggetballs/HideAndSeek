using System;
using System.Text;
using UnityEngine;

/// <summary>Plain-text scenario format: the first line is the top grid row.</summary>
public static class ScenarioTextFormat
{
    public static ScenarioGrid Parse(string text, float cellSize, Vector3 origin)
    {
        if (string.IsNullOrEmpty(text))
            throw new FormatException("Scenario text is empty.");

        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int height = lines.Length;
        if (lines[height - 1].Length == 0)
            height--; // A final newline is allowed, but it is not another row.

        if (height == 0 || lines[0].Length == 0)
            throw new FormatException("Scenario must have at least one nonempty row.");

        int width = lines[0].Length;
        var grid = new ScenarioGrid();
        grid.Initialise(width, height, cellSize, origin);

        for (int row = 0; row < height; row++)
        {
            if (lines[row].Length != width)
                throw new FormatException($"Scenario row {row + 1} has {lines[row].Length} cells; expected {width}. Preserve spaces at the end of each row.");

            for (int x = 0; x < width; x++)
            {
                char value = lines[row][x];
                if (value != ScenarioGrid.EmptyCell && value != ScenarioGrid.WallCell &&
                    value != ScenarioGrid.SeekerCell && value != ScenarioGrid.HiderCell)
                    throw new FormatException($"Unsupported character '{value}' at row {row + 1}, column {x + 1}.");

                grid.SetCell(new Vector2Int(x, height - 1 - row), value);
            }
        }

        grid.ClearDirty();
        return grid;
    }

    public static string Serialize(ScenarioGrid grid)
    {
        if (grid == null)
            throw new ArgumentNullException(nameof(grid));

        var text = new StringBuilder(grid.Height * (grid.Width + 1));
        for (int y = grid.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < grid.Width; x++)
                text.Append(grid.GetCell(new Vector2Int(x, y)));
            if (y > 0)
                text.Append('\n');
        }
        return text.ToString();
    }
}
