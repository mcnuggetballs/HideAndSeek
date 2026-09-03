using System.Collections.Generic;
using UnityEngine;

// view this as a reward + perception abstraction layer for RL agents

// owns and runs influence logic
// reads world state (runtime/agent) state and computes layers

public class InfluenceMap : MonoBehaviour
{
    // influence map layers, ablet o coexist internally but not visually
    public enum LayerTag
    {
        WasSeen, // 0=never seen, 1=seen before
        TimeSinceLastSeen, // 0=just seen, 1=old, 2=very old
        AgentPositions // occupancy grid, current position of SEEKER/HIDER
    }
    // references
    [SerializeField] private ScenarioGrid grid; // truth layer

    private int rows;
    private int cols;

    private Dictionary<LayerTag, float[,]> layers; // core storage

    #region SETUP 
    // system set up
    public void Initialise(ScenarioGrid gridRef)
    {
        if (gridRef == null)
        {
            Debug.LogError("InfluenceMap: Grid reference missing!");
            return;
        }
        grid = gridRef;
        rows = grid.Width;
        cols = grid.Height;

        InitialiseLayers();
    }

    // creating memory for each influence layer
    private void InitialiseLayers()
    {
        layers = new Dictionary<LayerTag, float[,]>
        {
            { LayerTag.WasSeen, new float[rows, cols] },
            { LayerTag.TimeSinceLastSeen, new float[rows, cols] },
            { LayerTag.AgentPositions, new float[rows, cols] }
        };
    }
    #endregion

    #region READ functions
    public float GetValue(Vector3 worldPosition, LayerTag layer)
    {
        Vector2Int cell = grid.WorldToCell(worldPosition);

        if (!grid.IsInsideGrid(cell)) return 0f;

        if (!layers.TryGetValue(layer, out var data)) return 0f;

        return data[cell.y, cell.x];
    }

    public bool TryGetLayer(LayerTag layer, out float[,] data)
    {
        data = null;

        if (layers == null)
            return false;

        if (!layers.TryGetValue(layer, out data))
            return false;

        return data != null;
    }
    #endregion

    #region WRITE functions
    // currently unused, for future optimisation
    public void WriteAgentLayer(Vector2Int cell, float value)
    {
        if (!grid.IsInsideGrid(cell)) return;

        if (!layers.TryGetValue(LayerTag.AgentPositions, out var data))
            return;

        data[cell.y, cell.x] = value;
    }

    // event based update
    public bool TryMarkWasSeen(Vector3 worldPosition, ref Vector2Int prevCell)
    {
        Vector2Int currCell = grid.WorldToCell(worldPosition);

        if (currCell == prevCell) return false;

        if (!grid.IsInsideGrid(currCell)) return false;

        layers[LayerTag.WasSeen][currCell.y, currCell.x] = 1f;
        prevCell = currCell;

        return true;
    }

    public void MarkWasSeen(Vector3 worldPos)
    {
        Vector2Int cell = grid.WorldToCell(worldPos);

        if (!grid.IsInsideGrid(cell)) return;

        layers[LayerTag.WasSeen][cell.y, cell.x] = 1f;

    }
    #endregion

    #region UPDATE functions, per frame update
    // occupancy grid
    public void UpdateAgentPositions(List<Transform> agents)
    {
        if(layers == null)
        {
            Debug.LogWarning("InfluenceMap is not initialised. Skipping. ");
            return;
        }

        if(!layers.TryGetValue(LayerTag.AgentPositions, out var data))
        {
            Debug.LogWarning("AgentPositions Layer missing");
            return;
        }

        ClearLayer(data);
        // mark agent positions
        foreach (var agent in agents)
        {
            Vector2Int cell = grid.WorldToCell(agent.position);

            if (!grid.IsInsideGrid(cell)) continue;

            data[cell.x, cell.y] = 1f; // cols, rows
        }
    }

    public void UpdateTimeLastSeen()
    {
        var data = layers[LayerTag.TimeSinceLastSeen];

        ClearLayer(data);

        // agent 
    }

    #endregion

    #region Utilities

    private void ClearLayer(float[,] layer)
    {
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                layer[y, x] = 0f;
    }

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        return grid.WorldToCell(worldPosition);
    }
    #endregion
}
