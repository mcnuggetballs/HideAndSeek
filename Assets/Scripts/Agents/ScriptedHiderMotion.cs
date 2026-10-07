using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controlled opponent for ordered training: stationary until first spotted,
/// then takes one walkable step away from the seeker at a time. It is not an ML agent.
/// </summary>
public sealed class ScriptedHiderMotion : MonoBehaviour
{
    private EnvironmentInstance environment;
    private NavMeshAgent navAgent;
    private bool enabledForEpisode;
    private float nextReplanTime;

    private void Awake() => navAgent = GetComponent<NavMeshAgent>();

    public void Configure(EnvironmentInstance instance, bool move)
    {
        environment = instance;
        enabledForEpisode = move;
        nextReplanTime = 0f;
        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.ResetPath();
    }

    private void FixedUpdate()
    {
        if (!enabledForEpisode || environment == null || navAgent == null ||
            !navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh ||
            environment.Seekers.Count == 0 ||
            !environment.TeamSightings.TryGet(navAgent, out _) ||
            Time.time < nextReplanTime)
            return;

        nextReplanTime = Time.time + 0.5f;
        ScenarioGrid grid = environment.Grid;
        Vector2Int here = grid.WorldToCell(transform.position);
        Vector3 seeker = environment.Seekers[0].transform.position;
        float bestDistance = (transform.position - seeker).sqrMagnitude;
        Vector3 destination = transform.position;
        Vector2Int[] directions = { Vector2Int.left, Vector2Int.right,
            Vector2Int.up, Vector2Int.down };
        foreach (Vector2Int direction in directions)
        {
            Vector2Int cell = here + direction;
            if (!grid.IsInsideGrid(cell) || grid.GetCell(cell) == ScenarioGrid.WallCell)
                continue;
            Vector3 proposed = grid.CellToWorld(cell);
            float distance = (proposed - seeker).sqrMagnitude;
            if (distance <= bestDistance || !NavMesh.SamplePosition(proposed,
                    out NavMeshHit hit, grid.CellSize * 0.4f, NavMesh.AllAreas))
                continue;
            bestDistance = distance;
            destination = hit.position;
        }
        if (destination != transform.position)
            navAgent.SetDestination(destination);
    }
}
