using System.Collections.Generic;
using UnityEngine;
using static InfluenceMap;

// reads influenceMap output to visalise layers
public class GridRenderer : MonoBehaviour
{
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private ScenarioGrid grid;
   
    [SerializeField] Color seenColor;
    [SerializeField] Color unseenColor;

    private Renderer[,] tiles;


    private int width;
    private int height;

    private MaterialPropertyBlock block;
    private static readonly int ColorID = Shader.PropertyToID("_BaseColor"); // convert string to int ID
    private List<GameObject> spawnedTiles = new();

    public void BuildVisualGrid(ScenarioGrid grid)

    {
        ClearTiles();
        this.width = grid.Width;
        this.height = grid.Height;

        float size = grid.CellSize;
        tiles = new Renderer[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 worldPos = grid.CellToWorld(new Vector2Int(x, y));

                GameObject tile = Instantiate(tilePrefab, transform);
                spawnedTiles.Add(tile);

                tile.transform.position = worldPos + Vector3.up * 0.05f;
                tile.transform.localScale = new Vector3(grid.CellSize, 1f, grid.CellSize);

                var r = tile.GetComponent<Renderer>();
                if (r == null)
                {
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

    // convert data to colors
    public void Render(float[,] data, LayerTag layer)
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                float v = data[x,y];

                Color col;

                switch (layer)
                {
                    case LayerTag.WasSeen:
                        col = (v > 0f) ? Color.red : Color.black;
                        break;

                    case LayerTag.AgentPositions:
                        col = (v > 0f) ? Color.blue : Color.black;
                        break;

                    default:
                        col = Color.Lerp(Color.black, Color.white, v);
                        break;
                }

                SetCellColor(x, y, col);
            }
    }

    private void ClearTiles()
    {
        foreach(var t in spawnedTiles)
        {
            if (t != null) Destroy(t);
        }
        spawnedTiles.Clear();
    }
}
