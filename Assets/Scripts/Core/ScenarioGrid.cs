using System.Collections.Generic;
using UnityEngine;

// grid origin system, anytime dealing with grid positions ONLY can take from ScenarioGrid

public class ScenarioGrid : MonoBehaviour
{
    // symbols to describe the map
    public const char EmptyCell = ' ';
    public const char WallCell = 'X';
    public const char SeekerCell = 'S';
    public const char HiderCell = 'H';

    // grid settings
    [SerializeField] private int gridWidth; // cols
    [SerializeField] private int gridHeight; // rows 
    [SerializeField] private float cellSize; // how big
    private bool isDirty = false;

    private char[,] cells; // 2d array 

    // Getter, grid.Width, grid.Height
    public int Width => gridWidth;
    public int Height => gridHeight;

    public float CellSize => cellSize;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        cells = new char[gridHeight, gridWidth];
        ClearGrid();
    }


    #region Utilities
    // converts a unity world position to grid cell 
    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        Vector3 origin = transform.position; // was gridOrigin

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

        return transform.position + new Vector3(
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
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        float offsetX = (gridWidth * cellSize) / 2f;
        float offsetZ = (gridHeight * cellSize) / 2f;

        Vector3 origin = transform.position - new Vector3(offsetX, offsetX, offsetZ);

        for (int x = 0; x <= gridWidth; x++)
        {
            Vector3 start = origin + new Vector3(x * cellSize, 0, 0);
            Vector3 end = origin + new Vector3(x * cellSize, 0, gridHeight * cellSize);
            Gizmos.DrawLine(start, end);
        }

        for (int y = 0; y <= gridHeight; y++)
        {
            Vector3 start = origin + new Vector3(0, 0, y * cellSize);
            Vector3 end = origin + new Vector3(gridWidth * cellSize, 0, y * cellSize);
            Gizmos.DrawLine(start, end);
        }
    }
    #endregion


    #region Grid Update
    public void ClearGrid()
    {
        for (int row = 0; row < Height; row++)
        {
            for (int col = 0; col < Width; col++)
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
            return;
        }

        cells[cell.y, cell.x] = value;
    }
    public IEnumerable<Vector2Int> GetAllCells()
    {
        for (int row = 0; row < Height; row++)
        {
            for (int col = 0; col < Width; col++)
            {
                yield return new Vector2Int(col, row);
            }
        }
    }

    public bool IsDirty()
    {
        return isDirty;
    }

    public void ClearDirty()
    {
        isDirty = false;
    }
    #endregion
}
