using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Owns the build order and lifetime of every environment in this simulation.
public class EnvironmentManager : MonoBehaviour
{
    private readonly List<EnvironmentInstance> environments = new();
    private GameObject seekerPrefab;
    private GameObject hiderPrefab;
    private GameObject obstaclePrefab;

    public IReadOnlyList<EnvironmentInstance> Environments => environments;
    public event Action<EnvironmentInstance> LayoutChanged;

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
        try
        {
            return RegisterEnvironment(grid, root, true);
        }
        catch
        {
            root.SetActive(false);
            Destroy(root);
            throw;
        }
    }

    // The testing scene already contains its environment root and editor UI.
    public EnvironmentInstance RegisterEnvironment(ScenarioGrid grid, GameObject root,
        bool ownsRoot = false)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (environments.Exists(item => item.Root == root))
            throw new InvalidOperationException("This environment root is already registered.");

        Transform runtimeRoot = root.transform.Find("RuntimeRoot");
        RuntimeNavMeshBuilder navigation = root.GetComponentInChildren<RuntimeNavMeshBuilder>(true);
        if (runtimeRoot == null || navigation == null)
            throw new InvalidOperationException(
                $"{root.name} needs a RuntimeRoot and RuntimeNavMeshBuilder.");

        WorldBuilder world = root.GetComponentInChildren<WorldBuilder>(true)
            ?? root.AddComponent<WorldBuilder>();
        InfluenceMap influence = root.GetComponentInChildren<InfluenceMap>(true)
            ?? root.AddComponent<InfluenceMap>();
        GridRenderer renderer = root.GetComponentInChildren<GridRenderer>(true);

        var instance = new EnvironmentInstance(root, runtimeRoot, grid, world, navigation,
            influence, renderer, ownsRoot);
        environments.Add(instance);
        RefreshLayoutDependents(instance);
        return instance;
    }

    public void BuildEnvironment(EnvironmentInstance environment)
    {
        RequireOwned(environment);
        ValidateBuildConfiguration(environment);

        ClearRuntime(environment);
        FitFloorToGrid(environment);
        environment.RuntimeRoot.gameObject.SetActive(true);

        GameObject rendererRoot = environment.GridRenderer != null
            ? environment.GridRenderer.gameObject
            : null;
        bool hideRendererDuringBake = rendererRoot != null &&
            rendererRoot != environment.Root &&
            rendererRoot != environment.RuntimeRoot.gameObject &&
            rendererRoot.activeSelf;

        try
        {
            environment.WorldBuilder.BuildGeometry(environment.Grid, obstaclePrefab,
                environment.RuntimeRoot, environment.MutableRuntimeObjects);

            if (hideRendererDuringBake)
                rendererRoot.SetActive(false);

            Physics.SyncTransforms();
            environment.NavMeshReady = environment.NavMeshBuilder.RebuildNavMesh();
            if (!environment.NavMeshReady)
                throw new InvalidOperationException("NavMesh build returned no data.");

            ValidateSpawnCellsOnNavMesh(environment.Grid);

            WorldBuildAgentsResult agents = environment.WorldBuilder.BuildAgents(
                environment.Grid, seekerPrefab, hiderPrefab, environment.RuntimeRoot,
                environment.MutableRuntimeObjects);
            environment.SetAgents(agents.Seekers, agents.Hiders);
            ValidateRuntimeAgents(environment);

            environment.InfluenceMap.Initialise(environment.Grid);
            foreach (SeekerAgent seeker in environment.Seekers)
                seeker.Initialize(environment, environment.InfluenceMap);

            environment.MarkBuilt();
            environment.EpisodeCoordinator.Activate();
        }
        catch (Exception exception)
        {
            environment.NavMeshBuilder.ClearNavMesh();
            environment.ClearRuntimeState();
            throw new InvalidOperationException(
                $"Build failed for {environment.Root.name}: {exception.Message}", exception);
        }
        finally
        {
            if (hideRendererDuringBake && rendererRoot != null)
                rendererRoot.SetActive(true);
        }
    }

    // Both prefabs use a Unity plane, ten units wide at scale one.
    public void FitFloorToGrid(EnvironmentInstance environment)
    {
        RequireOwned(environment);
        Transform floor = environment.Root.transform.Find("Plane");
        if (floor == null)
        {
            Debug.LogWarning("Environment has no Plane child to size to the scenario.", this);
            return;
        }

        ScenarioGrid grid = environment.Grid;
        floor.localRotation = Quaternion.identity;
        floor.localScale = new Vector3(
            grid.Width * grid.CellSize / 10f,
            floor.localScale.y,
            grid.Height * grid.CellSize / 10f);

        Transform placementArea = environment.Root.transform.Find("Placement Area");
        if (placementArea != null &&
            placementArea.TryGetComponent(out BoxCollider placementCollider))
        {
            placementCollider.size = new Vector3(
                grid.Width * grid.CellSize,
                placementCollider.size.y,
                grid.Height * grid.CellSize);
        }
    }

    public void RestartEpisode(EnvironmentInstance environment)
    {
        RequireOwned(environment);
        if (environment.IsBuilt)
            environment.EpisodeCoordinator.RestartEpisode();
    }

    public void ReplaceLayout(EnvironmentInstance environment, ScenarioGrid replacement)
    {
        RequireOwned(environment);
        if (replacement == null) throw new ArgumentNullException(nameof(replacement));

        ClearRuntime(environment);
        environment.ReplaceGrid(replacement);
        RefreshLayoutDependents(environment);
        LayoutChanged?.Invoke(environment);
    }

    public void ClearScenario(EnvironmentInstance environment)
    {
        RequireOwned(environment);
        ClearRuntime(environment);
        environment.Grid.ClearGrid();
        RefreshLayoutDependents(environment);
        LayoutChanged?.Invoke(environment);
    }

    public void ClearRuntime(EnvironmentInstance environment)
    {
        RequireOwned(environment);
        environment.EpisodeCoordinator.Deactivate();
        environment.NavMeshBuilder.ClearNavMesh();
        environment.ClearRuntimeState();
    }

    private void RefreshLayoutDependents(EnvironmentInstance environment)
    {
        FitFloorToGrid(environment);
        if (environment.GridRenderer == null)
            return;

        environment.GridRenderer.Initialise(environment.Grid);
        environment.GridRenderer.BuildVisualGrid();
    }

    public void RemoveEnvironment(EnvironmentInstance environment)
    {
        RequireOwned(environment);
        ClearRuntime(environment);
        environments.Remove(environment);

        if (environment.OwnsRoot && environment.Root != null)
        {
            environment.Root.SetActive(false);
            Destroy(environment.Root);
        }
    }

    private void ValidateBuildConfiguration(EnvironmentInstance environment)
    {
        if (seekerPrefab == null || hiderPrefab == null || obstaclePrefab == null)
            throw new InvalidOperationException(
                "Environment prefabs must be configured before building.");
        if (!ScenarioSystem.HasBothTeams(environment.Grid))
            throw new InvalidOperationException(
                "A runtime environment requires at least one seeker and one hider.");
    }

    private static void ValidateSpawnCellsOnNavMesh(ScenarioGrid grid)
    {
        float sampleDistance = Mathf.Max(1f, grid.CellSize * 0.45f);
        foreach (Vector2Int cell in grid.GetAllCells())
        {
            char value = grid.GetCell(cell);
            if (value != ScenarioGrid.SeekerCell && value != ScenarioGrid.HiderCell)
                continue;

            Vector3 position = grid.CellToWorld(cell);
            if (!NavMesh.SamplePosition(position, out _, sampleDistance, NavMesh.AllAreas))
                throw new InvalidOperationException(
                    $"Agent spawn cell {cell} is not on the NavMesh.");
        }
    }

    private static void ValidateRuntimeAgents(EnvironmentInstance environment)
    {
        foreach (SeekerAgent seeker in environment.Seekers)
        {
            NavMeshAgent navAgent = seeker != null
                ? seeker.GetComponent<NavMeshAgent>()
                : null;
            if (navAgent == null || !navAgent.isOnNavMesh)
                throw new InvalidOperationException(
                    $"A seeker in {environment.Root.name} is not on the NavMesh.");
        }

        foreach (NavMeshAgent hider in environment.Hiders)
        {
            if (hider == null || !hider.isOnNavMesh)
                throw new InvalidOperationException(
                    $"A hider in {environment.Root.name} is not on the NavMesh.");
        }
    }

    private void RequireOwned(EnvironmentInstance environment)
    {
        if (environment == null || !environments.Contains(environment))
            throw new ArgumentException(
                "Environment is not registered with this manager.", nameof(environment));
    }
}
