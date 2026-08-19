using UnityEngine;

public class GridRenderer : MonoBehaviour
{
    [SerializeField] private GameObject tilePrefab;


    private Renderer[,] tiles;


    private int rows;
    private int cols;

    private MaterialPropertyBlock block;
    private static readonly int ColorID = Shader.PropertyToID("_BaseColor"); // convert string to int ID

    public void BuildGrid(int rows, int cols)
    {
        this.rows = rows;
        this.cols = cols;

        tiles = new Renderer[rows, cols];

        for (int y = 0; y < rows; y++)
        {
            for(int x = 0; x < cols; x++)
            {
                GameObject tile = Instantiate(tilePrefab, transform);
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
}
