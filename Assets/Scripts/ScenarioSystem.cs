using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;

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
    
    [SerializeField] private ScenarioGrid grid;

    public ScenarioGrid Generate(ScenarioType type, TextAsset asset = null)
    {
        switch (type)
        {
            case ScenarioType.Fixed:
                return GenerateFixed(asset);

            case ScenarioType.Random:
                return GenerateRandom();

            default:
                Debug.LogError("Unknown ScenarioType");
                return null;
        }

    }
    private ScenarioGrid GenerateFixed(TextAsset file)
    {
        if (file == null)
        {
            Debug.LogWarning("No map file assigned.");
            return grid;
        }

        string[] lines = file.text
                .Replace("\r", "")
                .Split('\n');

        int height = lines.Length;
        int width = lines[0].Length;

        grid.Resize(width, height);
        grid.ClearGrid();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                char c = lines[y][x]; // assign char to grid
                Vector2Int cell = new Vector2Int(x, height - 1 - y);

                grid.SetCell(cell, c);
            }
        }
        return grid;
    }

    private ScenarioGrid GenerateRandom()
    {
        grid.ClearGrid();

        PlaceRandom(ScenarioGrid.WallCell, obstacleCount);
        PlaceRandom(ScenarioGrid.SeekerCell, seekerCount);
        PlaceRandom(ScenarioGrid.HiderCell, hiderCount);

        return grid;
    }

    private void PlaceRandom(char type, int count)
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
