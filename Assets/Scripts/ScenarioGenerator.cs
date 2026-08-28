using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// now handles both random + fixed environment
// only job is to fill grid with data
public class ScenarioGenerator : MonoBehaviour
{
    [SerializeField] public int obstacleCount = 8;
    [SerializeField] public int seekerCount = 1;
    [SerializeField] public int hiderCount = 1;

    private ScenarioGrid grid;

    public void Initialize(ScenarioGrid grid)
    {
        this.grid = grid;
    }

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

    public void GenerateFixed(TextAsset file)
    {
        if (file == null)
        {
            Debug.LogWarning("No map file assigned.");
            return;
        }

        grid.ClearGrid();
        string[] lines = file.text
            .Replace("\r", "")
            .Split('\n'); // line by line

        for (int y = 0; y < lines.Length; y++)
        {
            string line = lines[y];

            //int invertedY = lines.Length - 1 - y;
            for (int x = 0; x < line.Length; x++)
            {
                char c = line[x];

                Vector2Int cell = new Vector2Int(x, y);
                //Vector2Int cell = new Vector2Int(x, invertedY);


                switch (c)
                {
                    case ScenarioGrid.WallCell:
                        grid.SetCell(cell, ScenarioGrid.WallCell);
                        break;
                    case ScenarioGrid.SeekerCell:
                        grid.SetCell(cell, ScenarioGrid.SeekerCell);
                        break;
                    case ScenarioGrid.HiderCell:
                        grid.SetCell(cell, ScenarioGrid.HiderCell);
                        break;
                    default:
                        break;

                }
            }
        }
    }

    public void GenerateRandom()
    {
        grid.ClearGrid();

        PlaceRandom(ScenarioGrid.WallCell, obstacleCount);
        PlaceRandom(ScenarioGrid.SeekerCell, seekerCount);
        PlaceRandom(ScenarioGrid.HiderCell, hiderCount);
    }

}
