using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scenario layout. Cell coordinates are (x, y), where x runs along world X
/// and y runs along world Z. Origin is the center of the complete grid.
/// The cell array is always indexed [x, y].
/// </summary>
public class ScenarioGrid
{
    public const char EmptyCell = ' ';
    public const char WallCell = 'X';
    public const char SeekerCell = 'S';
    public const char HiderCell = 'H';

    private char[,] cells;
    private int gridWidth;
    private int gridHeight;
    private float cellSize;
    private Vector3 origin;
    private long layoutRevision;
    private long savedRevision;

    public int Width => gridWidth;
    public int Height => gridHeight;
    public float CellSize => cellSize;
    public Vector3 Origin => origin;
    public long LayoutRevision => layoutRevision;
    public long SavedRevision => savedRevision;
    public bool HasUnsavedChanges => layoutRevision != savedRevision;

    public void Initialise(int width, int height, float size, Vector3 origin)
    {
        ValidateDimensions(width, height);
        if (float.IsNaN(size) || float.IsInfinity(size) || size <= 0f)
            throw new ArgumentOutOfRangeException(nameof(size), "Cell size must be finite and greater than zero.");
        if (!IsFinite(origin.x) || !IsFinite(origin.y) || !IsFinite(origin.z))
            throw new ArgumentException("Origin coordinates must be finite.", nameof(origin));

        gridWidth = width;
        gridHeight = height;
        cellSize = size;
        this.origin = origin;
        cells = CreateEmptyCells(width, height);
        layoutRevision = 0;
        savedRevision = 0;
    }
    public bool IsInsideGrid(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < gridWidth &&
               cell.y >= 0 && cell.y < gridHeight;
    }

    /// <summary>Returns the cell containing a world position. The upper bounds are exclusive.</summary>
    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        EnsureInitialised();
        float left = origin.x - gridWidth * cellSize * 0.5f;
        float bottom = origin.z - gridHeight * cellSize * 0.5f;
        int x = Mathf.FloorToInt((worldPosition.x - left) / cellSize);
        int y = Mathf.FloorToInt((worldPosition.z - bottom) / cellSize);
        return new Vector2Int(x, y);
    }

    /// <summary>Returns the center of a cell on the grid's origin Y plane.</summary>
    public Vector3 CellToWorld(Vector2Int cell)
    {
        EnsureInitialised();
        if (!IsInsideGrid(cell))
            throw new ArgumentOutOfRangeException(nameof(cell), "Cell is outside the grid.");

        float left = origin.x - gridWidth * cellSize * 0.5f;
        float bottom = origin.z - gridHeight * cellSize * 0.5f;
        return new Vector3(
            left + (cell.x + 0.5f) * cellSize,
            origin.y,
            bottom + (cell.y + 0.5f) * cellSize);
    }

    /// <summary>Changes dimensions while preserving cells in the overlapping area.</summary>
    public void Resize(int width, int height)
    {
        EnsureInitialised();
        ValidateDimensions(width, height);
        if (width == gridWidth && height == gridHeight)
            return;

        char[,] resized = CreateEmptyCells(width, height);
        for (int x = 0; x < Math.Min(width, gridWidth); x++)
            for (int y = 0; y < Math.Min(height, gridHeight); y++)
                resized[x, y] = cells[x, y];

        cells = resized;
        gridWidth = width;
        gridHeight = height;
        layoutRevision++;
    }

    public void ClearGrid()
    {
        EnsureInitialised();
        bool changed = false;
        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
            {
                if (cells[x, y] == EmptyCell)
                    continue;

                cells[x, y] = EmptyCell;
                changed = true;
            }

        if (changed)
            layoutRevision++;
    }

    /// <summary>Out-of-bounds reads return EmptyCell; writes return false.</summary>
    public char GetCell(Vector2Int cell)
    {
        EnsureInitialised();
        return IsInsideGrid(cell) ? cells[cell.x, cell.y] : EmptyCell;
    }

    public bool SetCell(Vector2Int cell, char value)
    {
        EnsureInitialised();
        if (!IsInsideGrid(cell))
            return false;
        if (value != EmptyCell && value != WallCell && value != SeekerCell && value != HiderCell)
            throw new ArgumentException($"Unsupported scenario cell value: '{value}'.", nameof(value));

        if (cells[cell.x, cell.y] != value)
        {
            cells[cell.x, cell.y] = value;
            layoutRevision++;
        }
        return true;
    }

    public IEnumerable<Vector2Int> GetAllCells()
    {
        EnsureInitialised();
        for (int y = 0; y < gridHeight; y++)
            for (int x = 0; x < gridWidth; x++)
                yield return new Vector2Int(x, y);
    }

    /// <summary>Records that the current layout is the version persisted to storage.</summary>
    public void MarkSaved() => savedRevision = layoutRevision;

    private void EnsureInitialised()
    {
        if (cells == null)
            throw new InvalidOperationException("Initialise the scenario grid before using it.");
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Grid width must be greater than zero.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Grid height must be greater than zero.");
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static char[,] CreateEmptyCells(int width, int height)
    {
        var result = new char[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                result[x, y] = EmptyCell;
        return result;
    }
}
