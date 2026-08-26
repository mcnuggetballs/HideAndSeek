using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;

// view this as a reward + perception abstraction layer for RL agents

// owns and runs influence logic
// reads world state (runtime/agent) state and computes layers

public class InfluenceMap : MonoBehaviour
{
    // influence map layers, ablet o coexist internally but not visually
    public enum LayerTag
    {
        WasSeen, // decay over time
        TimeSinceLastSeen, // ticking timer
        DistanceToGoal, // bfs / dijkstra, not sure if i actually need this
        AgentPositions // occupancy grid, where agents ARE
    }
    // references
    [SerializeField] private ScenarioGrid grid; // truth layer

    private int rows;
    private int cols;

    private Dictionary<LayerTag, float[,]> layers; // core storage

    // system set up
    public void Initialise(ScenarioGrid gridRef)
    {
        if(gridRef == null)
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
            { LayerTag.DistanceToGoal, new float[rows, cols] },
            { LayerTag.AgentPositions, new float[rows, cols] }
        };
    }

    #region Core queries
    public float GetValue(Vector3 worldPosition, LayerTag layer)
    {
        Vector2Int cell = grid.WorldToCell(worldPosition);

        if (!grid.IsInsideGrid(cell)) return 0f;

        if (!layers.TryGetValue(layer, out var data)) return 0f;

        return data[cell.y, cell.x];
    }

    public bool TryGetLayer(LayerTag layer, out float[,] data)
    {
        return layers.TryGetValue(layer, out data);
    }
    #endregion

    #region How agents influence the map
    // agent position update
    public void WriteAgentLayer(Vector2Int cell, float value)
    {
        if (!grid.IsInsideGrid(cell)) return;

        layers[LayerTag.AgentPositions][cell.y, cell.x] = value;
    }

    // distance field
    public void WriteDistanceToGoalLayer(float[,] data)
    {
        layers[LayerTag.DistanceToGoal] = data;
    }

    #endregion

    #region layer update functions
    // occupancy grid
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

    public void UpdateDistanceToGoal()
    {

    }

    public void UpdateWasSeen()
    {

    }


    #endregion
}
