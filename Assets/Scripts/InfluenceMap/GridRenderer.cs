using UnityEngine;

// reads influenceMap output to visalise layers
public class GridRenderer : MonoBehaviour
{
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private ScenarioGrid grid;

    private Renderer[,] tiles;


    private int rows;
    private int cols;

    private MaterialPropertyBlock block;
    private static readonly int ColorID = Shader.PropertyToID("_BaseColor"); // convert string to int ID

    public void BuildFromScenarioGrid(ScenarioGrid grid)
    {
        float size = grid.CellSize;

        this.rows = grid.Width;
        this.cols = grid.Height;

        tiles = new Renderer[rows, cols];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                Vector3 worldPos = grid.CellToWorld(new Vector2Int(x, y));
                GameObject tile = Instantiate(tilePrefab, transform);

                // if you getting world space, use position**
                //tile.transform.position = worldPos;
                tile.transform.position = worldPos + Vector3.up * 0.05f;
                tile.transform.localScale = new Vector3(grid.CellSize, 1f, grid.CellSize);

                Renderer r = tile.GetComponent<Renderer>();
                if (r == null)
                {
                    Debug.LogError("Tile prefab missing Renderer!");
                    continue;
                }
                tiles[x, y] = r;
            }
        }
    }
    public void SetCellColor(int x, int y, Color color)
    {
        var tile = tiles[x, y];
        if (tile == null) return;

        block ??= new MaterialPropertyBlock(); // if no instance,
        block.Clear();

        tile.GetPropertyBlock(block);
        block.SetColor(ColorID, color);
        tile.SetPropertyBlock(block);
    }

    public void Render(float[,] data)
    {
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                float v = data[y, x];
                Color c = Color.Lerp(Color.white, Color.red, v);
                c.a = 0.3f;

                SetCellColor(x, y, c);

            }
        }
    }

    public float[,] Smooth(float[,] data)
    {
        int rows = data.GetLength(0);
        int cols = data.GetLength(1);

        float[,] result = new float[rows, cols];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                float sum = 0f;
                int count = 0;

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;

                        if (nx >= 0 && nx < cols && ny >= 0 && ny < rows)
                        {
                            sum += data[ny, nx];
                            count++;
                        }
                    }
                }

                result[y, x] = sum / count;
            }
        }

        return result;
    }
}
