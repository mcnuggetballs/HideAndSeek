using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.UI.Image;

// only create, fill and return grid
// factory that produces configured grid

public class ScenarioSystem : MonoBehaviour
{
    public enum ScenarioType
    {
        // UserPainted
        Fixed,
        Random
    }
    // for randomised scenario
    [SerializeField] public int obstacleCount = 8;
    [SerializeField] public int seekerCount = 1;
    [SerializeField] public int hiderCount = 1;

    public ScenarioGrid Generate(ScenarioType type, TextAsset asset = null, float cellSize = 1f, Vector3 origin = default)
    {
        switch (type)
        {
            case ScenarioType.Fixed:
                return GenerateFixed(asset, cellSize, origin);

            case ScenarioType.Random:
                return GenerateRandom(cellSize,origin);

            default:
                Debug.LogError("Unknown ScenarioType");
                return null;
        }

    }
    private ScenarioGrid GenerateFixed(TextAsset file, float cellSize, Vector3 origin)
    {
        if (file == null)
        {
            Debug.LogWarning("No map file assigned.");
            return null;
        }

        string[] lines = file.text
            .Replace("\r", "")
            .Split('\n');

        int height = lines.Length;
        int width = lines[0].Length;

        ScenarioGrid grid = new ScenarioGrid();
        grid.Initialise(width, height, cellSize, origin);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                char c = lines[y][x];
                Vector2Int cell = new Vector2Int(x, height - 1 - y);

                grid.SetCell(cell, c);
            }
        }

        return new ScenarioGrid(); // or better: empty valid grid
    }

    private ScenarioGrid GenerateRandom(float cellSize, Vector3 origin)
    {
        int width = 20;
        int height = 20;
        ScenarioGrid grid = new ScenarioGrid();
        grid.Initialise(width, height, cellSize, origin);

        PlaceRandom(grid,ScenarioGrid.WallCell, obstacleCount);
        PlaceRandom(grid, ScenarioGrid.SeekerCell, seekerCount);
        PlaceRandom(grid, ScenarioGrid.HiderCell, hiderCount);

        return grid;
    }

    private void PlaceRandom(ScenarioGrid grid,char type, int count)
    {
        List<Vector2Int> emptyCells = new List<Vector2Int>();

        foreach (var cell in grid.GetAllCells())
        {
            if (grid.GetCell(cell) == ScenarioGrid.EmptyCell)
            {
                emptyCells.Add(cell);
            }
        }

        // shuffle
        for (int i = 0; i < emptyCells.Count; i++)
        {
            Vector2Int temp = emptyCells[i];
            int randomIndex = Random.Range(i, emptyCells.Count);
            emptyCells[i] = emptyCells[randomIndex];
            emptyCells[randomIndex] = temp;
        }

        for (int i = 0; i < count && i < emptyCells.Count; i++)
        {
            grid.SetCell(emptyCells[i], type);
        }
    }

}
