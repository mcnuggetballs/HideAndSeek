using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// All references and runtime state for one independently built environment.

// owns participants and environment specific data
public sealed class EnvironmentInstance
{
    private readonly Dictionary<Vector2Int, GameObject> runtimeObjects = new();
    private readonly List<SeekerAgent> seekers = new();
    private readonly List<NavMeshAgent> hiders = new();
    private readonly Dictionary<NavMeshAgent, Pose> spawnPoses = new();
    private readonly List<NavMeshAgent> activeHiderBuffer = new();

    public int EnvironmentId { get; }
    public GameObject Root { get; }
    public Transform RuntimeRoot { get; }
    public WorldBuilder WorldBuilder { get; }
    public RuntimeNavMeshBuilder NavMeshBuilder { get; }
    public InfluenceMap InfluenceMap { get; }
    public GridRenderer GridRenderer { get; }
    public EnvironmentEpisodeCoordinator EpisodeCoordinator { get; }
    public TeamSightingMemory TeamSightings { get; } = new();
    public bool OwnsRoot { get; }
    public ScenarioGrid Grid { get; private set; }
    public bool IsBuilt { get; internal set; }
    public bool NavMeshReady { get; internal set; }
    public long BuiltLayoutRevision { get; private set; } = -1;
    public bool NeedsBuild => !IsBuilt || BuiltLayoutRevision != Grid.LayoutRevision;
    public IReadOnlyDictionary<Vector2Int, GameObject> RuntimeObjects => runtimeObjects;
    public IReadOnlyList<SeekerAgent> Seekers => seekers;
    public IReadOnlyList<NavMeshAgent> Hiders => hiders;
    public EpisodeSpecification CurrentEpisode { get; private set; }

    internal IDictionary<Vector2Int, GameObject> MutableRuntimeObjects => runtimeObjects;

    public EnvironmentInstance(int environmentId, GameObject root, Transform runtimeRoot, ScenarioGrid grid,
        WorldBuilder worldBuilder, RuntimeNavMeshBuilder navMeshBuilder,
        InfluenceMap influenceMap, GridRenderer gridRenderer, bool ownsRoot)
    {
        EnvironmentId = environmentId;
        Root = root ?? throw new ArgumentNullException(nameof(root));
        RuntimeRoot = runtimeRoot ?? throw new ArgumentNullException(nameof(runtimeRoot));
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        WorldBuilder = worldBuilder ?? throw new ArgumentNullException(nameof(worldBuilder));
        NavMeshBuilder = navMeshBuilder ?? throw new ArgumentNullException(nameof(navMeshBuilder));
        InfluenceMap = influenceMap ?? throw new ArgumentNullException(nameof(influenceMap));
        GridRenderer = gridRenderer;
        OwnsRoot = ownsRoot;
        EpisodeCoordinator = new EnvironmentEpisodeCoordinator(this);
    }

    internal void ReplaceGrid(ScenarioGrid grid)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        EpisodeCoordinator.Deactivate();
        IsBuilt = false;
        NavMeshReady = false;
        BuiltLayoutRevision = -1;
        CurrentEpisode = null;
    }

    internal void MarkBuilt()
    {
        IsBuilt = true;
        BuiltLayoutRevision = Grid.LayoutRevision;
    }

    internal void SetAgents(IReadOnlyList<SeekerAgent> builtSeekers,
        IReadOnlyList<NavMeshAgent> builtHiders)
    {
        seekers.Clear();
        hiders.Clear();
        spawnPoses.Clear();

        if (builtSeekers != null)
        {
            foreach (SeekerAgent seeker in builtSeekers)
            {
                if (seeker == null) continue;
                NavMeshAgent navAgent = seeker.GetComponent<NavMeshAgent>();
                if (navAgent == null || !navAgent.isOnNavMesh)
                    throw new InvalidOperationException(
                        $"{seeker.name} needs an active NavMeshAgent.");

                seekers.Add(seeker);
                spawnPoses[navAgent] = new Pose(
                    navAgent.nextPosition, seeker.transform.rotation);
            }
        }

        if (builtHiders != null)
        {
            foreach (NavMeshAgent hider in builtHiders)
            {
                if (hider == null) continue;
                if (!hider.isOnNavMesh)
                    throw new InvalidOperationException(
                        $"{hider.name} needs to be on the NavMesh.");

                hiders.Add(hider);
                spawnPoses[hider] = new Pose(
                    hider.nextPosition, hider.transform.rotation);
            }
        }
    }

    internal void ResetParticipantsToSpawn()
    {
        foreach (KeyValuePair<NavMeshAgent, Pose> entry in spawnPoses)
        {
            NavMeshAgent navAgent = entry.Key;
            if (navAgent == null) continue;

            Pose spawn = entry.Value;
            if (!navAgent.gameObject.activeSelf)
                navAgent.gameObject.SetActive(true);
            if (navAgent.TryGetComponent(out SeekerAgent seeker))
            {
                seeker.ResetMovement(spawn.position, spawn.rotation);
                continue;
            }

            if (navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            {
                if (!navAgent.Warp(spawn.position))
                    throw new InvalidOperationException(
                        $"Could not reset {navAgent.name} to its spawn position.");
                navAgent.ResetPath();
                navAgent.velocity = Vector3.zero;
            }
            else
            {
                navAgent.transform.position = spawn.position;
            }

            navAgent.transform.rotation = spawn.rotation;
        }
    }

    internal void AssignTargets()
    {
        activeHiderBuffer.Clear();
        foreach (NavMeshAgent hider in hiders)
            if (hider != null && hider.gameObject.activeInHierarchy)
                activeHiderBuffer.Add(hider);

        foreach (SeekerAgent seeker in seekers)
        {
            if (seeker == null) continue;
            if (!seeker.transform.IsChildOf(RuntimeRoot))
                throw new InvalidOperationException($"{seeker.name} does not belong to {Root.name}.");
            seeker.SetTargets(activeHiderBuffer);
        }
    }

    internal void ApplyEpisodeSpecification(EpisodeSpecification specification)
    {
        if (specification == null) throw new ArgumentNullException(nameof(specification));
        if (specification.SeekerSpawnCells.Length != seekers.Count ||
            specification.HiderSpawnCells.Length != hiders.Count)
            throw new InvalidOperationException(
                "Episode participant counts must match the built environment.");

        HashSet<Vector2Int> occupied = new();
        for (int i = 0; i < seekers.Count; i++)
            SetSpawnCell(seekers[i].GetComponent<NavMeshAgent>(),
                specification.SeekerSpawnCells[i], occupied);
        for (int i = 0; i < hiders.Count; i++)
            SetSpawnCell(hiders[i], specification.HiderSpawnCells[i], occupied);

        CurrentEpisode = specification;
    }

    private void SetSpawnCell(NavMeshAgent participant, Vector2Int cell,
        HashSet<Vector2Int> occupied)
    {
        if (participant == null) throw new InvalidOperationException("Episode participant is missing.");
        if (!Grid.IsInsideGrid(cell) || Grid.GetCell(cell) == ScenarioGrid.WallCell)
            throw new InvalidOperationException($"Episode spawn cell {cell} is not walkable.");
        if (!occupied.Add(cell))
            throw new InvalidOperationException($"Episode spawn cell {cell} is assigned twice.");

        Vector3 requested = Grid.CellToWorld(cell);
        float radius = Mathf.Max(1f, Grid.CellSize * 0.45f);
        if (!NavMesh.SamplePosition(requested, out NavMeshHit hit, radius, NavMesh.AllAreas))
            throw new InvalidOperationException($"Episode spawn cell {cell} is not on the NavMesh.");

        spawnPoses[participant] = new Pose(hit.position,
            Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
    }

    internal bool CaptureHider(NavMeshAgent hider)
    {
        if (hider == null || !hiders.Contains(hider) || !hider.gameObject.activeSelf)
            return false;

        hider.ResetPath();
        hider.velocity = Vector3.zero;
        hider.gameObject.SetActive(false);
        AssignTargets();
        return true;
    }

    internal void GetSeekerTransforms(List<Transform> output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        output.Clear();
        foreach (SeekerAgent seeker in seekers)
            if (seeker != null) output.Add(seeker.transform);
    }

    internal void ClearRuntimeState()
    {
        EpisodeCoordinator.Deactivate();

        foreach (GameObject runtimeObject in runtimeObjects.Values)
        {
            if (runtimeObject == null) continue;
            runtimeObject.SetActive(false);
            UnityEngine.Object.Destroy(runtimeObject);
        }

        runtimeObjects.Clear();
        seekers.Clear();
        hiders.Clear();
        spawnPoses.Clear();
        activeHiderBuffer.Clear();
        TeamSightings.ResetEpisode();
        CurrentEpisode = null;
        IsBuilt = false;
        NavMeshReady = false;
        BuiltLayoutRevision = -1;
    }
}
