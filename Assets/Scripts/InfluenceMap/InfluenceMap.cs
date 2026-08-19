using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;

// acts an an analysis + agent perception layer 
// enforces layer filtering? but all layers should be able to overlay one another tho

public class InfluenceMap : MonoBehaviour
{
    // influence map layers, ablet o coexist internally but not visually
    public enum LayerTag
    {
        WasSeen,
        TimeSinceLastSeen,
        DistanceToGoal,
        AgentPositions
    }
    // references
    [SerializeField] private ScenarioGrid grid; // truth layer
    [SerializeField] private GridRenderer gridRenderer;
    [SerializeField] private LayerTag debugLayer;

    private int rows;
    private int cols;

    private Dictionary<LayerTag, float[,]> layers;

    public void Initialise(ScenarioGrid gridRef)
    {
        grid = gridRef;
        rows = grid.Width;
        cols = grid.Height;

        InitialiseLayers();
    }

    // creating 4 separate grids 
    private void InitialiseLayers()
    {
        layers = new Dictionary<LayerTag, float[,]>
        {
            { LayerTag.WasSeen, new float[rows, cols] },
            { LayerTag.TimeSinceLastSeen, new float[rows, cols] },
            { LayerTag.DistanceToGoal, new float[rows, cols] },
            { LayerTag.AgentPositions, new float[rows, cols] }
        };
    }
    #region Core functions
    public float GetInfluenceAtPosition(Vector3 worldPosition, LayerTag layer)
    {
        Vector2Int cell = grid.WorldToCell(worldPosition);
        if (!grid.IsInsideGrid(cell)) return 0f;

        return layers[layer][cell.y,cell.x]; // query for specific single layer
    }

    // used by agent reward system
    public float GetCombinedInfluence(Vector3 worldPosition)
    {
        Vector2Int cell = grid.WorldToCell(worldPosition);
        if (!grid.IsInsideGrid(cell)) return 0f;

        float value = 0f;
        value += layers[LayerTag.WasSeen][cell.y, cell.x];
        value += layers[LayerTag.TimeSinceLastSeen][cell.y, cell.x];
        value += layers[LayerTag.DistanceToGoal][cell.y, cell.x];
        value += layers[LayerTag.AgentPositions][cell.y, cell.x];

        return value;
    }
    
    // seperate debug hook for visualisation instead of tied to influenceMap

    #endregion

    #region How agents influence the map
    // agent position update
    public void SetAgentInfluence(Vector2Int cell, float value)
    {
        if (!grid.IsInsideGrid(cell)) return;

        layers[LayerTag.AgentPositions][cell.y, cell.x] = value;
    }

    // distance field
    public void SetDistanceToGoal(float[,] data)
    {
        layers[LayerTag.DistanceToGoal] = data;
    }

    // for switching or inspecting layers
    public void RefreshDebugView()
    {
        for (int y = 0; y < rows; y++)
        {
            for(int x = 0; x < cols; x++)
            {

                float v = layers[LayerTag.AgentPositions][y, x];
                gridRenderer.SetCellColor(x, y, Color.Lerp(Color.white, Color.red, v));
            }
        }
    }

    #endregion

    #region runtime influence map
    // mark where agents are on the grid
    public void UpdateAgentPositions(List<Transform> agents)
    {
        var data = layers[LayerTag.AgentPositions];

        // clear grid
        for (int y = 0; y<rows;y++)
        {
            for(int x = 0; x < cols; x++)
            {
                data[y, x] = 0f;
            }
        }

        // mark agent positions
        foreach(var agent in agents)
        {
            Vector2Int cell = grid.WorldToCell(agent.position);
            Debug.Log($"{agent.name} world={agent.position} -> cell={cell}");

            if (!grid.IsInsideGrid(cell))
            {
                Debug.Log("GRID IS OUT OF BOUNDS!");
                continue;
            }
            data[cell.y, cell.x] = 1f;
        }
        Debug.Log($"Agents received: {agents.Count}");
    }
    #endregion
}
