using System;
using System.Collections.Generic;
using UnityEngine;

// Plans a training batch. EnvironmentManager owns creation and runtime building.
[DisallowMultipleComponent]
public class EnvironmentSpawner : MonoBehaviour
{
    [Tooltip("Optional override; otherwise uses SimulationController's training environment prefab.")]
    public GameObject environmentPrefab;
    [Min(1)] public int environmentCount = 16;
    [Min(1)] public int maxCount = 50;
    [Tooltip("Empty world-space distance between environment floors.")]
    [Min(0f)] public float environmentGap = 100f;

    // Called explicitly after the manager and scenario configuration are ready.
    public IReadOnlyList<EnvironmentInstance> SpawnEnvironments(
        EnvironmentManager manager, GameObject fallbackPrefab, ScenarioGrid firstGrid,
        Func<Vector3, ScenarioGrid> generateGrid)
    {
        if (manager == null) throw new ArgumentNullException(nameof(manager));
        if (firstGrid == null) throw new ArgumentNullException(nameof(firstGrid));
        if (generateGrid == null) throw new ArgumentNullException(nameof(generateGrid));
        if (environmentCount < 1 || maxCount < 1 || environmentCount > maxCount)
            throw new ArgumentOutOfRangeException(nameof(environmentCount),
                $"Environment count must be between 1 and {maxCount}.");
        if (float.IsNaN(environmentGap) || float.IsInfinity(environmentGap) || environmentGap < 0f)
            throw new ArgumentOutOfRangeException(nameof(environmentGap));
        GameObject prefab = environmentPrefab != null ? environmentPrefab : fallbackPrefab;
        if (prefab == null) throw new ArgumentNullException(nameof(fallbackPrefab));
        if (manager.Environments.Count != 0)
            throw new InvalidOperationException("Remove the previous training batch before spawning another.");

        int columns = Mathf.CeilToInt(Mathf.Sqrt(environmentCount));
        float spacingX = firstGrid.Width * firstGrid.CellSize + environmentGap;
        float spacingZ = firstGrid.Height * firstGrid.CellSize + environmentGap;
        var grids = new List<ScenarioGrid>(environmentCount);
        var uniqueGrids = new HashSet<ScenarioGrid>();
        // Validate all scenario data before creating any runtime objects.
        for (int i = 0; i < environmentCount; i++)
        {
            Vector3 origin = firstGrid.Origin + new Vector3(
                (i % columns) * spacingX, 0f, (i / columns) * spacingZ);
            ScenarioGrid grid = i == 0 ? firstGrid : generateGrid(origin);
            if (grid == null || !ScenarioSystem.HasBothTeams(grid))
                throw new InvalidOperationException($"Training scenario {i} must contain both teams.");
            if (!uniqueGrids.Add(grid))
                throw new InvalidOperationException("Each environment requires its own ScenarioGrid.");
            if (grid.Width != firstGrid.Width || grid.Height != firstGrid.Height ||
                grid.CellSize != firstGrid.CellSize || grid.Origin != origin)
                throw new InvalidOperationException("Batch grids must match the first grid's dimensions and use their assigned origin.");
            grids.Add(grid);
        }

        var created = new List<EnvironmentInstance>(environmentCount);
        try
        {
            foreach (ScenarioGrid grid in grids)
            {
                EnvironmentInstance instance = manager.CreateEnvironment(grid, prefab);
                created.Add(instance);
                manager.BuildEnvironment(instance);
            }
        }
        catch
        {
            // Do not leave a partially built training batch running.
            for (int i = created.Count - 1; i >= 0; i--)
                manager.RemoveEnvironment(created[i]);
            throw;
        }
        return created.AsReadOnly();
    }
}
