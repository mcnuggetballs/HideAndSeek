using Grpc.Core;
using NUnit.Framework.Constraints;
using System.Collections.Generic;
using UnityEngine;

// grid origin system, anytime dealing with grid positions ONLY can take from ScenarioGrid
// changing to non monobehaviour as we are creating grids instead of having a grid

public class ScenarioGrid
{
    // symbols to describe the map
    public const char EmptyCell = ' ';
    public const char WallCell = 'X';
    public const char SeekerCell = 'S';
    public const char HiderCell = 'H';

    public bool isDirty { get; private set; } // will be referenced by simulation controller

    private char[,] cells; // 2d array 
    private int gridWidth;
    private int gridHeight;
    private float cellSize;
    private Vector3 origin;

    public int Width => gridWidth;
    public int Height => gridHeight;
    public float CellSize => cellSize;

    // decide this in SimulationController or EditorController
    public void Initialise(int width, int height, float size, Vector3 origin)
    {
        this.gridWidth = width;
        this.gridHeight = height;
        this.cellSize = size;
        this.origin = origin;

        cells = new char[width, height];

    }

    #region Conversion Helpers
    // converts a unity world position to grid cell 
    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        float offsetX = (gridWidth * cellSize) / 2f;
        float offsetZ = (gridHeight * cellSize) / 2f;
        int col = Mathf.FloorToInt((worldPosition.x - origin.x + offsetX) / cellSize);
        int row = Mathf.FloorToInt((worldPosition.z - origin.z + offsetZ) / cellSize);

        return new Vector2Int(col, row);
    }

    // converts cell position back to unity world position
    public Vector3 CellToWorld(Vector2Int cell)
    {
        float offsetX = (gridWidth * cellSize) / 2f;
        float offsetZ = (gridHeight * cellSize) / 2f;

        return origin + new Vector3(
            (cell.x * cellSize) - offsetX + (cellSize * 0.5f),
            0,
            (cell.y * cellSize) - offsetZ + (cellSize * 0.5f)
        );

    }
    // is clicked cell inside grid?
    public bool IsInsideGrid(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < gridWidth && // check if cell x position is 0 - 19
               cell.y >= 0 && cell.y < gridHeight;
    }

    #endregion


    #region Data Manipulation
    public void Resize(int width, int height)
    {
        gridWidth = width;
        gridHeight = height;

        cells = new char[gridHeight, gridWidth];
    }

    public void ClearGrid()
    {
        for (int row = 0; row < gridHeight; row++)
        {
            for (int col = 0; col < gridWidth; col++)
            {
                cells[row, col] = EmptyCell;
            }
        }
    }
    // what is currently in this cell?
    public char GetCell(Vector2Int cell) // but vector2int is x,y
    {
        if (!IsInsideGrid(cell))
        {
            return EmptyCell;
        }

        return cells[cell.y, cell.x]; // arrays usually stored as row,col
    }

    // change cell to either seeker/hider/wall
    public void SetCell(Vector2Int cell, char value)
    {
        if (!IsInsideGrid(cell))
        {
            Debug.LogError($"SetCell out of bounds: {cell}");
            return;
        }

        cells[cell.x, cell.y] = value;// left to right
        isDirty = true;
    }
    public IEnumerable<Vector2Int> GetAllCells()
    {
        for (int row = 0; row < gridHeight; row++)
        {
            for (int col = 0; col < gridWidth; col++)
            {
                yield return new Vector2Int(col, row);
            }
        }
    }

    public void MarkDirty()
    {
        isDirty = true;
    }
    public void ClearDirty()
    {
        isDirty = false;
    }
    #endregion
}
