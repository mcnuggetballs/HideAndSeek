using System.Collections.Generic;
using UnityEngine;

// this file records the grid map similar to a truth table
// eventually can have settings for users


// try read and load csv files, fall back default??? switch case

public class ScenarioGrid : MonoBehaviour
{
    // symbols to describe the map
    public const char EmptyCell = ' ';
    public const char WallCell = 'X';
    public const char SeekerCell = 'S';
    public const char HiderCell = 'H';

    // grid settings
    [SerializeField] private int gridWidth = 50; // cols
    [SerializeField] private int gridHeight = 50; // rows 
    [SerializeField] private float cellSize = 1f; // how big
    [SerializeField] private Vector3 gridOrigin; // where cell 0,0 begins

    private char[,] cells; // 2d array 

    public int Width => gridWidth;
    public int Height => gridHeight;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        cells = new char[gridHeight, gridWidth];
        ClearGrid();
    }

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

    // converts a unity world position to grid cell 
    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        // snapping
        int col = Mathf.FloorToInt((worldPosition.x - gridOrigin.x) / cellSize); // left right
        int row = Mathf.FloorToInt((worldPosition.z - gridOrigin.z) / cellSize); // foward back

        return new Vector2Int(col, row);
    }

    // converts cell position back to unity world position
    public Vector3 CellToWorld(Vector2Int cell)
    {
        // need 0.5f to spawn at center of cell
        //return gridOrigin + new Vector3(
        //    cellPosition.x * cell + (cell * 0.5f),
        //    0,
        //    cellPosition.y * cell + (cell * 0.5f)
        //);
        float offsetX = (gridWidth - 1) * 0.5f;
        float offsetZ = (gridHeight - 1) * 0.5f;

        return transform.position + new Vector3(
            (cell.x - offsetX)* cellSize, 
            0,
            (cell.y - offsetZ) * cellSize);
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

    // is clicked cell inside grid?
    public bool IsInsideGrid(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < gridWidth && // check if cell x position is 0 - 19
               cell.y >= 0 && cell.y < gridHeight;
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

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        Vector3 origin = gridOrigin;

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
}
