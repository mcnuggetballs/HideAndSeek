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
    public const int VectorObservationSize = 40;

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
        foreach (NavMeshAgent hider in environment.Hiders)
        {
            if (hider == null || !hider.gameObject.activeInHierarchy) continue;
            if (CanSee(hider))
                environment.TeamSightings.Record(hider, hider.transform.position,
                    environment.EpisodeCoordinator.CurrentStep);
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
        Vector3 fromCenter = transform.position - grid.Origin;
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
                for (int value = 0; value < 4; value++) sensor.AddObservation(0f);
                continue;
            }

            NavMeshAgent hider = environment.Hiders[index];
            if (!environment.TeamSightings.TryGet(hider, out TeamSightingMemory.Sighting sighting))
            {
                for (int value = 0; value < 4; value++) sensor.AddObservation(0f);
                continue;
            }

            int memorySteps = Mathf.Max(1, hostileMemorySteps);
            int age = environment.EpisodeCoordinator.CurrentStep - sighting.Step;
            if (age > memorySteps)
            {
                for (int value = 0; value < 4; value++) sensor.AddObservation(0f);
                continue;
            }

            Vector3 relative = transform.InverseTransformPoint(sighting.WorldPosition);
            sensor.AddObservation(1f);
            sensor.AddObservation(Mathf.Clamp01((float)age / memorySteps));
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
        float move = MoveSpeedBuckets[moveIndex];
        float turn = TurnBuckets[turnIndex];

        transform.Rotate(0f, turn * rotateSpeed * Time.fixedDeltaTime, 0f);
        seekerAgent.Move(transform.forward * move * moveSpeed * Time.fixedDeltaTime);

        targetAgent = FindNearestTarget();
        if (targetAgent == null) return;

        float combinedRadius = seekerAgent.radius + targetAgent.radius;
        float effectiveCatchDistance = Mathf.Max(catchDistance, combinedRadius);
        if (Vector3.Distance(seekerAgent.nextPosition, targetAgent.nextPosition) <= effectiveCatchDistance)
            environment?.EpisodeCoordinator.ReportCapture(this, targetAgent);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discrete = actionsOut.DiscreteActions;
        discrete[0] = 1;
        discrete[1] = 2;
    }

    private void WriteTeammate(VectorSensor sensor, Transform teammate,
        float halfWidth, float halfHeight)
    {
        Vector3 relative = transform.InverseTransformPoint(teammate.position);
        Vector3 heading = transform.InverseTransformDirection(teammate.forward);
        sensor.AddObservation(1f);
        sensor.AddObservation(Mathf.Clamp(relative.x / halfWidth, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(relative.z / halfHeight, -1f, 1f));
        sensor.AddObservation(heading.x);
        sensor.AddObservation(heading.z);
    }

    private bool CanSee(NavMeshAgent hider)
    {
        Vector3 direction = hider.transform.position - transform.position;
        float distance = direction.magnitude;
        if (distance > viewDistance || distance < 0.001f) return false;
        if (Vector3.Angle(transform.forward, direction) > viewAngle * 0.5f) return false;

        float forwardOffset = seekerAgent != null ? seekerAgent.radius + 0.05f : 0.55f;
        Vector3 origin = transform.position + Vector3.up * eyeHeight + transform.forward * forwardOffset;
        if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance + 0.25f))
            return false;
        return hit.transform == hider.transform || hit.transform.IsChildOf(hider.transform);
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
