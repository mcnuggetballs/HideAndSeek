using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class RandomScenarioGenerator : MonoBehaviour
{
    [SerializeField] public int obstacleCount = 8;
    [SerializeField] public int seekerCount = 1;
    [SerializeField] public int hiderCount = 1;

    private ScenarioGrid grid;
    private EnvironmentManager environmentManager;

    private void Awake()
    {
        grid = GetComponent<ScenarioGrid>();
        environmentManager = GetComponent<EnvironmentManager>();
    }

    public void Generate()
    {
        grid.ClearGrid();

        PlaceRandom(ScenarioGrid.WallCell, obstacleCount);
        PlaceRandom(ScenarioGrid.SeekerCell, seekerCount);
        PlaceRandom(ScenarioGrid.HiderCell, hiderCount);
    }

    //void PlaceRandom(char type, int count)
    //{
    //    int attempts = 0;
    //    while (count > 0 && attempts < 1000)
    //    {
    //        int x = Random.Range(0, grid.Width);
    //        int y = Random.Range(0, grid.Height);

    //        Vector2Int cell = new Vector2Int(x, y);

    //        if (grid.GetCell(cell) == ScenarioGrid.EmptyCell)
    //        {
    //            grid.SetCell(cell, type);
    //            count--;
    //        }

    //        attempts++;
    //    }
    //}
    void PlaceRandom(char type, int count)
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
