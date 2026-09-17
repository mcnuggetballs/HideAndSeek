using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static SimulationController;

// only create, fill and return grid
// grid factory that produces configured grid


// see it as a pure generator

public class ScenarioSystem : MonoBehaviour
{
    public enum ScenarioType
    {
        // UserPainted
        Fixed,
        Random,
        Empty,
        Saved
    }
    // for randomised scenario
    [SerializeField] public int obstacleCount = 8;
    [SerializeField] public int seekerCount = 1;
    [SerializeField] public int hiderCount = 1;
    [SerializeField] private string savedScenarioName = "painted_scenario";

    public string SavedScenarioName => savedScenarioName;

    public ScenarioGrid Generate(
    ScenarioType type,
    TextAsset mapFile,
    WorldConfig config)
    {
        try
        {
            ScenarioGrid grid = type switch
            {
                ScenarioType.Fixed => GenerateFixed(mapFile, config),
                ScenarioType.Random => GenerateRandom(config),
                ScenarioType.Empty => GenerateEmpty(config),
                ScenarioType.Saved => ScenarioStorage.Load(savedScenarioName, config.cellSize, config.origin),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown scenario type.")
            };

            return grid;
        }
        catch (Exception exception) when (exception is ArgumentException || exception is FormatException ||
                                          exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogError($"[ScenarioSystem] Could not generate {type} scenario: {exception.Message}");
            return null;
        }
    }

    public string SavePainted(ScenarioGrid grid) => ScenarioStorage.Save(savedScenarioName, grid);

    public ScenarioGrid LoadPainted(float cellSize, Vector3 origin) =>
        ScenarioStorage.Load(savedScenarioName, cellSize, origin);

    public static bool HasBothTeams(ScenarioGrid grid)
    {
        int seekers = 0;
        int hiders = 0;
        foreach (Vector2Int cell in grid.GetAllCells())
        {
            char value = grid.GetCell(cell);
            if (value == ScenarioGrid.SeekerCell) seekers++;
            if (value == ScenarioGrid.HiderCell) hiders++;
        }

        return seekers > 0 && hiders > 0;
    }

    private ScenarioGrid GenerateFixed(TextAsset file, WorldConfig config)
    {
        Debug.Log("Generating FIXED scenario...");
        if (file == null)
        {
            Debug.LogWarning("[ScenarioSystem] GenerateFixed called with file == null");
            return null;
        }

        ScenarioGrid grid = ScenarioTextFormat.Parse(file.text, config.cellSize, config.origin);
        Debug.Log($"[ScenarioSystem] Loaded {file.name}: {grid.Width}x{grid.Height}");
        return grid;
    }

    private ScenarioGrid GenerateRandom(WorldConfig config)
    {
        Debug.Log("Generating RANDOM scenario...");
        if (obstacleCount < 0 || seekerCount < 0 || hiderCount < 0)
            throw new ArgumentException("Random scenario counts cannot be negative.");
        if ((long)obstacleCount + seekerCount + hiderCount > (long)config.width * config.height)
            throw new ArgumentException("Random scenario has more objects than cells.");

        var grid = new ScenarioGrid();
        grid.Initialise(config.width, config.height, config.cellSize, config.origin);

        PlaceRandom(grid, ScenarioGrid.WallCell, obstacleCount);
        PlaceRandom(grid, ScenarioGrid.SeekerCell, seekerCount);
        PlaceRandom(grid, ScenarioGrid.HiderCell, hiderCount);

        return grid;
    }

    private ScenarioGrid GenerateEmpty(WorldConfig config)
    {
        Debug.Log("Generating EMPTY scenario...");
        var grid = new ScenarioGrid();
        grid.Initialise(config.width, config.height, config.cellSize, config.origin);

        return grid;
    }

    private void PlaceRandom(ScenarioGrid grid, char type, int count)
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
            int randomIndex = UnityEngine.Random.Range(i, emptyCells.Count);
            emptyCells[i] = emptyCells[randomIndex];
            emptyCells[randomIndex] = temp;
        }

        for (int i = 0; i < count && i < emptyCells.Count; i++)
        {
            grid.SetCell(emptyCells[i], type);
        }
    }

}
