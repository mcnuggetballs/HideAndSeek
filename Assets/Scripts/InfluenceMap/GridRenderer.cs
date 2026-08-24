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
        this.rows = grid.Width;
        this.cols = grid.Height;

        tiles = new Renderer[rows, cols];


        for (int y = 0; y < rows; y++)
        {
            for(int x = 0; x < cols; x++)
            {
                Vector3 worldPos = grid.CellToWorld(new Vector2Int(x,y));
                GameObject tile = Instantiate(tilePrefab, transform);

                // if you getting world space, use position**
                tile.transform.position = worldPos;
                tiles[x,y] = tile.GetComponent<Renderer>();
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
        for (int y = 0;  y < rows; y++)
        {
            for(int x = 0; x < cols; x++)
            {
                float v = data[y, x];
                SetCellColor(x, y, Color.Lerp(Color.white, Color.red, v));
            }
        }
    }
}
