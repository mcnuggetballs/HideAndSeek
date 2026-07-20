using UnityEngine;

public class RandomScenarioGenerator : MonoBehaviour
{
    [SerializeField] public int obstacleCount = 8;
    [SerializeField] public int seekerCount = 1;
    [SerializeField] public int hiderCount = 1;

    private ScenarioGrid grid;

    private void Awake()
    {
        grid = GetComponent<ScenarioGrid>();
    }

    public void Generate()
    {
        grid.ClearGrid();

        PlaceRandom(ScenarioGrid.WallCell, obstacleCount);
        PlaceRandom(ScenarioGrid.SeekerCell, seekerCount);
        PlaceRandom(ScenarioGrid.HiderCell, hiderCount);
    }

    void PlaceRandom(char type, int count)
    {
        int attempts = 0;
        while (count > 0 && attempts < 1000)
        {
            int x = Random.Range(0, grid.Width);
            int y = Random.Range(0, grid.Height);

            Vector2Int cell = new Vector2Int(x, y);

            if(grid.GetCell(cell) == ScenarioGrid.EmptyCell)
            {
                grid.SetCell(cell, type);
                count--;
            }

            attempts++;
        }
    }
}
