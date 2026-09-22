using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// All references and runtime state for one independently built environment.
public sealed class EnvironmentInstance
{
    private readonly Dictionary<Vector2Int, GameObject> runtimeObjects = new();
    private readonly List<SeekerAgent> seekers = new();
    private readonly List<NavMeshAgent> hiders = new();
    private readonly Dictionary<NavMeshAgent, Pose> spawnPoses = new();

    public GameObject Root { get; }
    public Transform RuntimeRoot { get; }
    public WorldBuilder WorldBuilder { get; }
    public RuntimeNavMeshBuilder NavMeshBuilder { get; }
    public InfluenceMap InfluenceMap { get; }
    public GridRenderer GridRenderer { get; }
    public EnvironmentEpisodeCoordinator EpisodeCoordinator { get; }
    public bool OwnsRoot { get; }
    public ScenarioGrid Grid { get; private set; }
    public bool IsBuilt { get; internal set; }
    public bool NavMeshReady { get; internal set; }
    public long BuiltLayoutRevision { get; private set; } = -1;
    public bool NeedsBuild => !IsBuilt || BuiltLayoutRevision != Grid.LayoutRevision;
    public IReadOnlyDictionary<Vector2Int, GameObject> RuntimeObjects => runtimeObjects;
    public IReadOnlyList<SeekerAgent> Seekers => seekers;
    public IReadOnlyList<NavMeshAgent> Hiders => hiders;

    internal IDictionary<Vector2Int, GameObject> MutableRuntimeObjects => runtimeObjects;

    public EnvironmentInstance(GameObject root, Transform runtimeRoot, ScenarioGrid grid,
        WorldBuilder worldBuilder, RuntimeNavMeshBuilder navMeshBuilder,
        InfluenceMap influenceMap, GridRenderer gridRenderer, bool ownsRoot)
    {
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
        foreach (SeekerAgent seeker in seekers)
        {
            if (seeker == null) continue;
            if (!seeker.transform.IsChildOf(RuntimeRoot))
                throw new InvalidOperationException($"{seeker.name} does not belong to {Root.name}.");
            seeker.SetTargets(hiders);
        }
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
        IsBuilt = false;
        NavMeshReady = false;
        BuiltLayoutRevision = -1;
    }
}
