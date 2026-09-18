using System;
using System.Collections.Generic;
using UnityEngine;

// Owns the build order and lifetime of every environment in this simulation.
public class EnvironmentManager : MonoBehaviour
{
    private readonly List<EnvironmentInstance> environments = new();
    private GameObject seekerPrefab;
    private GameObject hiderPrefab;
    private GameObject obstaclePrefab;

    public IReadOnlyList<EnvironmentInstance> Environments => environments;

    public void Configure(GameObject seeker, GameObject hider, GameObject obstacle)
    {
        seekerPrefab = seeker;
        hiderPrefab = hider;
        obstaclePrefab = obstacle;
    }

    public EnvironmentInstance CreateEnvironment(ScenarioGrid grid, GameObject environmentPrefab)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (environmentPrefab == null) throw new ArgumentNullException(nameof(environmentPrefab));
        GameObject root = Instantiate(environmentPrefab, grid.Origin, Quaternion.identity, transform);
        root.name = $"Environment_{environments.Count}";
        try { return RegisterEnvironment(grid, root, true); }
        catch { Destroy(root); throw; }
    }

    // The testing scene already contains its environment root and editor UI.
    public EnvironmentInstance RegisterEnvironment(ScenarioGrid grid, GameObject root, bool ownsRoot = false)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (environments.Exists(item => item.Root == root))
            throw new InvalidOperationException("This environment root is already registered.");

        Transform runtimeRoot = root.transform.Find("RuntimeRoot");
        RuntimeNavMeshBuilder navigation = root.GetComponentInChildren<RuntimeNavMeshBuilder>(true);
        if (runtimeRoot == null || navigation == null)
            throw new InvalidOperationException($"{root.name} needs a RuntimeRoot and RuntimeNavMeshBuilder.");

        WorldBuilder world = root.GetComponentInChildren<WorldBuilder>(true) ?? root.AddComponent<WorldBuilder>();
        InfluenceMap influence = root.GetComponentInChildren<InfluenceMap>(true) ?? root.AddComponent<InfluenceMap>();
        GridRenderer renderer = root.GetComponentInChildren<GridRenderer>(true);
        var instance = new EnvironmentInstance(root, runtimeRoot, grid, world, navigation,
            influence, renderer, ownsRoot);
        environments.Add(instance);
        return instance;
    }

    public void BuildEnvironment(EnvironmentInstance environment)
    {
        RequireOwned(environment);
        if (seekerPrefab == null || hiderPrefab == null || obstaclePrefab == null)
            throw new InvalidOperationException("Environment prefabs must be configured before building.");

        environment.RuntimeRoot.gameObject.SetActive(true);
        environment.IsBuilt = false;
        environment.NavMeshReady = false;
        environment.WorldBuilder.BuildGeometry(environment.Grid, obstaclePrefab, environment.RuntimeRoot);
        Physics.SyncTransforms();
        environment.NavMeshReady = environment.NavMeshBuilder.RebuildNavMesh();
        if (!environment.NavMeshReady)
            throw new InvalidOperationException($"NavMesh build failed for {environment.Root.name}.");

        environment.WorldBuilder.BuildAgents(seekerPrefab, hiderPrefab);
        environment.WorldBuilder.AssignRuntimeTargets();
        environment.InfluenceMap.Initialise(environment.Grid);
        foreach (SeekerAgent seeker in environment.Seekers)
            seeker.Initialize(this, environment, environment.InfluenceMap);

        environment.Grid.ClearDirty();
        environment.IsBuilt = true;
    }

    public void ResetEpisode(EnvironmentInstance environment)
    {
        RequireOwned(environment);
        if (!environment.IsBuilt) return;
        environment.WorldBuilder.ResetAgentsOnly();
        environment.WorldBuilder.AssignRuntimeTargets();
    }

    public void ClearRuntime(EnvironmentInstance environment)
    {
        RequireOwned(environment);
        environment.WorldBuilder.ClearRuntimeObjects();
        environment.NavMeshBuilder.ClearNavMesh();
        environment.IsBuilt = false;
        environment.NavMeshReady = false;
    }

    public void RemoveEnvironment(EnvironmentInstance environment)
    {
        RequireOwned(environment);
        ClearRuntime(environment);
        environments.Remove(environment);
        if (environment.OwnsRoot && environment.Root != null)
            Destroy(environment.Root);
    }

    private void RequireOwned(EnvironmentInstance environment)
    {
        if (environment == null || !environments.Contains(environment))
            throw new ArgumentException("Environment is not registered with this manager.", nameof(environment));
    }
}
