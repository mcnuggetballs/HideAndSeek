using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Converts ScenarioGrid data into runtime GameObjects. It does not own runtime state.
public class WorldBuilder : MonoBehaviour
{
    public void BuildGeometry(ScenarioGrid grid, GameObject obstaclePrefab,
        Transform runtimeRoot, IDictionary<Vector2Int, GameObject> runtimeObjects)
    {
        ValidateBuildArguments(grid, obstaclePrefab, runtimeRoot, runtimeObjects);

        foreach (Vector2Int cell in grid.GetAllCells())
        {
            if (grid.GetCell(cell) != ScenarioGrid.WallCell)
                continue;

            SpawnRuntimeObject(grid, cell, obstaclePrefab, runtimeRoot, runtimeObjects);
        }
    }

    // Agents are built only after the NavMesh exists.
    public WorldBuildAgentsResult BuildAgents(ScenarioGrid grid, GameObject seekerPrefab,
        GameObject hiderPrefab, Transform runtimeRoot,
        IDictionary<Vector2Int, GameObject> runtimeObjects)
    {
        ValidateBuildArguments(grid, seekerPrefab, runtimeRoot, runtimeObjects);
        if (hiderPrefab == null) throw new ArgumentNullException(nameof(hiderPrefab));
        if (seekerPrefab.GetComponent<SeekerAgent>() == null ||
            seekerPrefab.GetComponent<NavMeshAgent>() == null)
            throw new InvalidOperationException("The seeker prefab needs SeekerAgent and NavMeshAgent components.");
        if (hiderPrefab.GetComponent<NavMeshAgent>() == null)
            throw new InvalidOperationException("The hider prefab needs a NavMeshAgent component.");

        var seekers = new List<SeekerAgent>();
        var hiders = new List<NavMeshAgent>();

        foreach (Vector2Int cell in grid.GetAllCells())
        {
            char value = grid.GetCell(cell);
            if (value == ScenarioGrid.SeekerCell)
            {
                GameObject obj = SpawnRuntimeObject(
                    grid, cell, seekerPrefab, runtimeRoot, runtimeObjects);
                SeekerAgent seeker = obj.GetComponent<SeekerAgent>();
                if (seeker == null)
                    throw new InvalidOperationException($"{obj.name} has no SeekerAgent.");
                seekers.Add(seeker);
            }
            else if (value == ScenarioGrid.HiderCell)
            {
                GameObject obj = SpawnRuntimeObject(
                    grid, cell, hiderPrefab, runtimeRoot, runtimeObjects);
                NavMeshAgent hider = obj.GetComponent<NavMeshAgent>();
                if (hider == null)
                    throw new InvalidOperationException($"{obj.name} has no NavMeshAgent.");
                hiders.Add(hider);
            }
        }

        return new WorldBuildAgentsResult(seekers, hiders);
    }

    private static GameObject SpawnRuntimeObject(ScenarioGrid grid, Vector2Int cell,
        GameObject prefab, Transform runtimeRoot,
        IDictionary<Vector2Int, GameObject> runtimeObjects)
    {
        if (runtimeObjects.ContainsKey(cell))
            throw new InvalidOperationException($"Cell {cell} already has a runtime object.");

        GameObject obj = Instantiate(prefab, grid.CellToWorld(cell), Quaternion.identity, runtimeRoot);
        runtimeObjects.Add(cell, obj);
        return obj;
    }

    private static void ValidateBuildArguments(ScenarioGrid grid, GameObject prefab,
        Transform runtimeRoot, IDictionary<Vector2Int, GameObject> runtimeObjects)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (prefab == null) throw new ArgumentNullException(nameof(prefab));
        if (runtimeRoot == null) throw new ArgumentNullException(nameof(runtimeRoot));
        if (runtimeObjects == null) throw new ArgumentNullException(nameof(runtimeObjects));
    }
}

public sealed class WorldBuildAgentsResult
{
    public IReadOnlyList<SeekerAgent> Seekers { get; }
    public IReadOnlyList<NavMeshAgent> Hiders { get; }

    public WorldBuildAgentsResult(IReadOnlyList<SeekerAgent> seekers,
        IReadOnlyList<NavMeshAgent> hiders)
    {
        Seekers = seekers ?? throw new ArgumentNullException(nameof(seekers));
        Hiders = hiders ?? throw new ArgumentNullException(nameof(hiders));
    }
}
