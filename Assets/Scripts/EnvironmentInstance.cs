using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// All references and runtime state for one independently built environment.
public sealed class EnvironmentInstance
{
    public GameObject Root { get; }
    public Transform RuntimeRoot { get; }
    public WorldBuilder WorldBuilder { get; }
    public RuntimeNavMeshBuilder NavMeshBuilder { get; }
    public InfluenceMap InfluenceMap { get; }
    public GridRenderer GridRenderer { get; }
    public bool OwnsRoot { get; }
    public ScenarioGrid Grid { get; private set; }
    public bool IsBuilt { get; internal set; }
    public bool NavMeshReady { get; internal set; }
    public IReadOnlyList<SeekerAgent> Seekers => WorldBuilder.GetSeekers();
    public IReadOnlyList<NavMeshAgent> Hiders => WorldBuilder.GetHiders();

    public EnvironmentInstance(GameObject root, Transform runtimeRoot, ScenarioGrid grid,
        WorldBuilder worldBuilder, RuntimeNavMeshBuilder navMeshBuilder,
        InfluenceMap influenceMap, GridRenderer gridRenderer, bool ownsRoot)
    {
        Root = root;
        RuntimeRoot = runtimeRoot;
        Grid = grid;
        WorldBuilder = worldBuilder;
        NavMeshBuilder = navMeshBuilder;
        InfluenceMap = influenceMap;
        GridRenderer = gridRenderer;
        OwnsRoot = ownsRoot;
    }

    public void SetGrid(ScenarioGrid grid)
    {
        Grid = grid;
        IsBuilt = false;
        NavMeshReady = false;
    }
}
