using UnityEngine;

public class GridRenderer : MonoBehaviour
{
    private Renderer[,] tiles;
    private MaterialPropertyBlock block;

    private static readonly int ColorID = Shader.PropertyToID("_BaseColor"); // convert string to int ID

    public void SetCellColor(int x, int y, Color color)
    {
        Renderer tile = tiles[x, y];
        if (tile == null) return;

        block ??= new MaterialPropertyBlock(); // if no instance,
        block.Clear();

        tile.GetPropertyBlock(block);
        block.SetColor(ColorID, color);
        tile.SetPropertyBlock(block);
    }
}
