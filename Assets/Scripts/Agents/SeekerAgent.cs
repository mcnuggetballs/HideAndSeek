using System;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Police policy shell. It senses and acts, while EnvironmentEpisodeCoordinator
/// owns rewards, outcomes and resets.
/// </summary>
public sealed class SeekerAgent : Agent
{
    private static readonly float[] MoveSpeedBuckets = { -0.5f, 0f, 0.5f, 1f };
    private static readonly float[] TurnBuckets = { -1f, -0.5f, 0f, 0.5f, 1f };
    private const int MaxTeammates = 4;
    private const int MaxHiders = 4;
    public const int VectorObservationSize = 52;

    [Header("Agent Setup")]
    [SerializeField] private NavMeshAgent seekerAgent;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotateSpeed = 180f;

    [Header("Perception")]
    [SerializeField] private float viewDistance = 50f;
    [SerializeField, Range(1f, 360f)] private float viewAngle = 90f;
    [SerializeField] private float eyeHeight = 0.5f;
    [SerializeField] private float catchDistance = 1.5f;
    [SerializeField, Min(1)] private int hostileMemorySteps = 512;

    [Header("Runtime Diagnostics")]
    [SerializeField] private int lastMoveAction;
    [SerializeField] private int lastTurnAction;
    [SerializeField] private float displacementSinceLastAction;
    [SerializeField] private int currentlyVisibleHiders;

    private readonly RaycastHit[] sightHits = new RaycastHit[32];
    private readonly HashSet<NavMeshAgent> visibleHiders = new();
    private Vector3 previousActionPosition;
    private bool hasPreviousActionPosition;

    private readonly List<NavMeshAgent> targetAgents = new();
    private EnvironmentInstance environment;
    private InfluenceMap influenceMap;
    private NavMeshAgent targetAgent;

    protected override void Awake()
    {
        base.Awake();
        if (seekerAgent == null) seekerAgent = GetComponent<NavMeshAgent>();
        if (seekerAgent != null) seekerAgent.updateRotation = false;
    }

    public void Initialize(EnvironmentInstance instance, InfluenceMap map)
    {
        environment = instance ?? throw new ArgumentNullException(nameof(instance));
        influenceMap = map;
        GetComponent<LocalGridSensorComponent>()?.Configure(instance.Grid);

        if (environment.Seekers.Count - 1 > MaxTeammates)
            throw new InvalidOperationException($"The policy supports at most {MaxTeammates + 1} seekers.");
        if (environment.Hiders.Count > MaxHiders)
            throw new InvalidOperationException($"The policy supports at most {MaxHiders} hiders.");
    }

    public void ResetMovement(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (seekerAgent != null && seekerAgent.isActiveAndEnabled && seekerAgent.isOnNavMesh)
        {
            seekerAgent.Warp(spawnPosition);
            seekerAgent.ResetPath();
            seekerAgent.velocity = Vector3.zero;
        }
        else
        {
            transform.position = spawnPosition;
        }

        transform.rotation = spawnRotation;
        targetAgent = null;
        hasPreviousActionPosition = false;
        displacementSinceLastAction = 0f;
        currentlyVisibleHiders = 0;
        visibleHiders.Clear();
    }

    public void SetTargets(IReadOnlyList<NavMeshAgent> targets)
    {
        targetAgents.Clear();
        if (targets != null)
            foreach (NavMeshAgent target in targets)
                if (target != null && target.gameObject.activeInHierarchy)
                    targetAgents.Add(target);
        targetAgent = FindNearestTarget();
    }

    public override void OnEpisodeBegin()
    {
        environment?.EpisodeCoordinator.OnAgentEpisodeBegin(this);
    }

    /// <summary>Called for every teammate in a coordinator pass before observations are consumed.</summary>
    public void UpdateTeamSightings()
    {
        if (environment == null) return;
        currentlyVisibleHiders = 0;
        visibleHiders.Clear();
        foreach (NavMeshAgent hider in environment.Hiders)
        {
            if (hider == null || !hider.gameObject.activeInHierarchy) continue;
            if (CanSee(hider))
            {
                currentlyVisibleHiders++;
                visibleHiders.Add(hider);
                environment.TeamSightings.Record(hider, hider.transform.position,
                    environment.EpisodeCoordinator.CurrentStep);
            }
        }
        influenceMap?.MarkWasSeen(transform.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (environment == null)
        {
            for (int i = 0; i < VectorObservationSize; i++) sensor.AddObservation(0f);
            return;
        }

        ScenarioGrid grid = environment.Grid;
        float halfWidth = Mathf.Max(grid.CellSize, grid.Width * grid.CellSize * 0.5f);
        float halfHeight = Mathf.Max(grid.CellSize, grid.Height * grid.CellSize * 0.5f);
        Vector3 fromCenter = environment.Root.transform.InverseTransformDirection(
            transform.position - grid.Origin);
        Vector3 localForward = environment.Root.transform.InverseTransformDirection(transform.forward);

        sensor.AddObservation(Mathf.Clamp(fromCenter.x / halfWidth, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(fromCenter.z / halfHeight, -1f, 1f));
        sensor.AddObservation(localForward.x);
        sensor.AddObservation(localForward.z);

        int teammateSlots = 0;
        foreach (SeekerAgent teammate in environment.Seekers)
        {
            if (teammate == null || teammate == this) continue;
            WriteTeammate(sensor, teammate.transform, halfWidth, halfHeight);
            teammateSlots++;
        }
        while (teammateSlots++ < MaxTeammates)
            for (int value = 0; value < 5; value++) sensor.AddObservation(0f);

        for (int index = 0; index < MaxHiders; index++)
        {
            if (index >= environment.Hiders.Count)
            {
                for (int value = 0; value < 7; value++) sensor.AddObservation(0f);
                continue;
            }

            NavMeshAgent hider = environment.Hiders[index];
            if (hider == null || !hider.gameObject.activeInHierarchy)
            {
                for (int value = 0; value < 7; value++) sensor.AddObservation(0f);
                continue;
            }

            int memorySteps = Mathf.Max(1, hostileMemorySteps);
            bool hasSighting = environment.TeamSightings.TryGet(hider,
                out TeamSightingMemory.Sighting sighting);
            int age = hasSighting ? Mathf.Max(0,
                environment.EpisodeCoordinator.CurrentStep - sighting.Step) : memorySteps + 1;
            hasSighting &= age <= memorySteps;
            bool visibleNow = visibleHiders.Contains(hider);
            bool teamVisibleNow = hasSighting && age == 0;
            Vector3 relative = hasSighting
                ? transform.InverseTransformDirection(sighting.WorldPosition - transform.position)
                : Vector3.zero;
            // Active, known position, personally visible now, teammate visible now,
            // normalized age, and last-known X/Z. Zero position means unknown when known=0.
            sensor.AddObservation(1f);
            sensor.AddObservation(hasSighting ? 1f : 0f);
            sensor.AddObservation(visibleNow ? 1f : 0f);
            sensor.AddObservation(teamVisibleNow && !visibleNow ? 1f : 0f);
            sensor.AddObservation(hasSighting ? Mathf.Clamp01((float)age / memorySteps) : 1f);
            sensor.AddObservation(Mathf.Clamp(relative.x / halfWidth, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(relative.z / halfHeight, -1f, 1f));
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (seekerAgent == null || !seekerAgent.isOnNavMesh) return;
        ActionSegment<int> discrete = actions.DiscreteActions;
        if (discrete.Length < 2) return;

        int moveIndex = Mathf.Clamp(discrete[0], 0, MoveSpeedBuckets.Length - 1);
        int turnIndex = Mathf.Clamp(discrete[1], 0, TurnBuckets.Length - 1);
        lastMoveAction = moveIndex;
        lastTurnAction = turnIndex;
        displacementSinceLastAction = hasPreviousActionPosition
            ? Vector3.Distance(transform.position, previousActionPosition) : 0f;
        previousActionPosition = transform.position;
        hasPreviousActionPosition = true;
        float move = MoveSpeedBuckets[moveIndex];
        float turn = TurnBuckets[turnIndex];

        transform.Rotate(0f, turn * rotateSpeed * Time.fixedDeltaTime, 0f);
        seekerAgent.Move(transform.forward * move * moveSpeed * Time.fixedDeltaTime);

        targetAgent = FindNearestTarget();
        if (targetAgent == null) return;

        // NavMeshAgent radii are expressed in each object's local scale. Both character
        // prefab roots are scaled by two, so comparing centers to the unscaled radii
        // leaves an unreachable capture threshold while avoidance keeps them apart.
        float seekerScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        float targetScale = Mathf.Max(Mathf.Abs(targetAgent.transform.lossyScale.x),
            Mathf.Abs(targetAgent.transform.lossyScale.z));
        float combinedRadius = seekerAgent.radius * seekerScale + targetAgent.radius * targetScale;
        float effectiveCatchDistance = Mathf.Max(catchDistance, combinedRadius + 0.1f);
        if (Vector3.Distance(seekerAgent.nextPosition, targetAgent.nextPosition) <= effectiveCatchDistance)
            environment?.EpisodeCoordinator.ReportCapture(this, targetAgent);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discrete = actionsOut.DiscreteActions;
        NavMeshAgent target = FindNearestTarget();
        if (target == null || environment == null ||
            !environment.TeamSightings.TryGet(target, out TeamSightingMemory.Sighting sighting))
        {
            discrete[0] = 1;
            discrete[1] = 3;
            return;
        }

        Vector3 direction = sighting.WorldPosition - transform.position;
        direction.y = 0f;
        float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
        float absAngle = Mathf.Abs(angle);
        discrete[0] = absAngle > 60f ? 2 : 3;
        discrete[1] = angle < -25f ? 0 : angle < -5f ? 1 :
            angle > 25f ? 4 : angle > 5f ? 3 : 2;
    }

    private void WriteTeammate(VectorSensor sensor, Transform teammate,
        float halfWidth, float halfHeight)
    {
        Vector3 relative = transform.InverseTransformDirection(teammate.position - transform.position);
        Vector3 heading = transform.InverseTransformDirection(teammate.forward);
        sensor.AddObservation(1f);
        sensor.AddObservation(Mathf.Clamp(relative.x / halfWidth, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(relative.z / halfHeight, -1f, 1f));
        sensor.AddObservation(heading.x);
        sensor.AddObservation(heading.z);
    }

    private bool CanSee(NavMeshAgent hider)
    {
        if (hider == null || !hider.gameObject.activeInHierarchy) return false;
        Vector3 direction = hider.transform.position - transform.position;
        float distance = direction.magnitude;
        if (distance > viewDistance || distance < 0.001f) return false;
        if (Vector3.Angle(transform.forward, direction) > viewAngle * 0.5f) return false;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Collider targetCollider = hider.GetComponent<Collider>();
        Vector3 targetPoint = targetCollider != null
            ? targetCollider.bounds.center : hider.transform.position + Vector3.up * eyeHeight;
        Vector3 eyeDirection = targetPoint - origin;
        float rayDistance = eyeDirection.magnitude;
        if (rayDistance < 0.001f) return true;
        int count = Physics.RaycastNonAlloc(origin, eyeDirection / rayDistance, sightHits,
            rayDistance + 0.01f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        // NonAlloc hits are unordered. A full buffer may omit the nearest blocker;
        // use an allocating fallback only in that exceptional crowded case.
        RaycastHit[] hits = sightHits;
        if (count == sightHits.Length)
        {
            hits = Physics.RaycastAll(origin, eyeDirection / rayDistance,
                rayDistance + 0.01f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }
        float nearestDistance = float.PositiveInfinity;
        Transform nearest = null;
        for (int index = 0; index < count; index++)
        {
            Transform hitTransform = hits[index].transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
            if (hits[index].distance >= nearestDistance) continue;
            nearestDistance = hits[index].distance;
            nearest = hitTransform;
        }
        return nearest != null && (nearest == hider.transform || nearest.IsChildOf(hider.transform));
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(origin, Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward * viewDistance);
        Gizmos.DrawRay(origin, Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward * viewDistance);
        if (environment == null) return;
        foreach (NavMeshAgent hider in environment.Hiders)
        {
            if (hider == null || !hider.gameObject.activeInHierarchy) continue;
            Collider collider = hider.GetComponent<Collider>();
            Gizmos.color = CanSee(hider) ? Color.green : Color.red;
            Gizmos.DrawLine(origin, collider != null ? collider.bounds.center : hider.transform.position);
        }
    }

    private NavMeshAgent FindNearestTarget()
    {
        NavMeshAgent nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (NavMeshAgent possible in targetAgents)
        {
            if (possible == null || !possible.gameObject.activeInHierarchy || !possible.isOnNavMesh)
                continue;
            float distance = (transform.position - possible.transform.position).sqrMagnitude;
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearest = possible;
        }
        return nearest;
    }
}
