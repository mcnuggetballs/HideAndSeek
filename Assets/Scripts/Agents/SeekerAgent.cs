using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.AI;

public class SeekerAgent : Agent
{
    [Header("Agent Setup")]
    //public Rigidbody rb;
    public NavMeshAgent seekerAgent;

    public float moveSpeed = 5f;
    public float rotateSpeed = 180f;
    public NavMeshAgent targetAgent; // hider, does not have to be a navmeshnangent
    private readonly List<NavMeshAgent> targetAgents = new List<NavMeshAgent>();

    //[Header("Visibility Settings")]
    public float viewDistance = 50f;
    public float viewAngle = 90f;
    public float eyeHeight = 0.5f;
    public float catchDistance = 1.5f; // TODO:change!

    // simulation manager spawns in world, seeker agent spawn inside own world
    [Header("Spawn Settings")]
    [SerializeField] private bool useRandomSpawn = true;
    public float minSpawnDistance = 10f;
    public float spawnRadius = 10f;
    public float groundY = 0.929f;

    [Header("Reward Settings")]
    public float visibleDistanceRewardScale = 0.04f;
    public float generalDistanceRewardScale = 0.005f;
    public float maintainSightReward = 0.001f;
    public float unseenPenalty = -0.0001f;
    public float searchMoveReward = 0.0001f;
    public float rotationPenaltyScale = 0.00005f;
    public float timePenalty = -0.0002f;
    public float catchReward = 10f;

    private float prevDistance = 0f;

    private void Awake()
    {
        if (seekerAgent == null)
        {
            seekerAgent = GetComponent<NavMeshAgent>();
        }
    }

    public override void OnEpisodeBegin()
    {
        if (!HasTargetAndNavAgent())
        {
            return;
        }

        ResetMovement();

        if (useRandomSpawn){ RandomizeSpawn(); }

        prevDistance = Vector3.Distance(seekerAgent.transform.position, targetAgent.transform.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (targetAgent == null)
        {
            sensor.AddObservation(0);
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(1f);
            return;
        }

        Vector3 localDir;
        float normalisedDistance;

        bool visible = GetTargetInfo(out localDir, out normalisedDistance);
        sensor.AddObservation(visible ? 1 : 0); // target visible?

        if (visible)
        {
            sensor.AddObservation(localDir);
            sensor.AddObservation(normalisedDistance);
        }
        else
        { // need to return same amount of observations
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(1f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!HasTargetAndNavAgent())
        {
            return;
        }

        float rotate = actions.ContinuousActions[0];
        float move = Mathf.Clamp01(actions.ContinuousActions[1]);

        transform.Rotate(0f, rotate * rotateSpeed * Time.deltaTime, 0f);
        Vector3 moveDirection = transform.forward * move * moveSpeed * Time.deltaTime;
        seekerAgent.Move(moveDirection);

        if (Vector3.Distance(transform.position, targetAgent.transform.position) < catchDistance)
        {
            AddReward(catchReward);
            EndEpisode();
            return;
        }

        ApplyVisibilityBasedRewards(rotate, move);
    }

    private bool GetTargetInfo(out Vector3 localDir, out float normalisedDistance)
    {
        if (targetAgent == null)
        {   
            localDir = Vector3.zero;
            normalisedDistance = 1f;
            return false;
        }

        // calculate for distance check
        Vector3 dir = targetAgent.transform.position - transform.position; // get the dir vector from current position to target position
        float distance = dir.magnitude;

        localDir = transform.InverseTransformDirection(dir).normalized; // localised dir
        normalisedDistance = Mathf.Clamp01(distance / viewDistance); // normalised dist

        // calculate for direction check
        float angle = Vector3.Angle(transform.forward, dir); // measure angle between hider and seeker, every game object has its own forward direction

        // calculate if target in FOV/Dist
        bool insideFOV = angle <= viewAngle * 0.5f;
        bool insideDist = distance <= viewDistance;

        // if can see target, give target info to agent
        if (!insideFOV || !insideDist) { return false; }

        // 
        Vector3 rayOrigin = transform.position + Vector3.up * eyeHeight; // shoot ray from seeker's eye level

        if (Physics.Raycast(rayOrigin, dir.normalized, out RaycastHit hit, viewDistance))
        {
            return hit.transform == targetAgent || hit.transform.IsChildOf(targetAgent.transform); // if ray hits target OR child of target first, means can see
        }

        return false;
    }

    private void ResetMovement()
    {
        if (seekerAgent == null)
        {
            return;
        }

        seekerAgent.ResetPath();
        seekerAgent.velocity = Vector3.zero;
        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
    }

    private void RandomizeSpawn()
    {
        if (!HasTargetAndNavAgent())
        {
            return;
        }

        int attempts = 0;
        int maxAttempts = 100;

        Vector3 rootPosition = transform.parent != null ? transform.parent.position : Vector3.zero; // root position of simulation prefab
        Vector3 seekerSpawn = Vector3.zero;
        Vector3 targetSpawn = Vector3.zero;
        bool foundValidSpawn = false;

        // randomise the seeker/hider position based on the simulation prefab root position
        do
        {
            attempts++;
            Vector3 seekerCandidate = new Vector3(
                rootPosition.x + Random.Range(-spawnRadius, spawnRadius),
                groundY,
                rootPosition.z + Random.Range(-spawnRadius, spawnRadius)
            );

            Vector3 targetCandidate = new Vector3(
                rootPosition.x + Random.Range(-spawnRadius, spawnRadius),
                groundY,
                rootPosition.z + Random.Range(-spawnRadius, spawnRadius)
            );

            bool seekerFound = NavMesh.SamplePosition(
                seekerCandidate,
                out NavMeshHit seekerHit,
                5f,
                NavMesh.AllAreas);

            bool targetFound = NavMesh.SamplePosition(
                targetCandidate,
                out NavMeshHit targetHit,
                5f,
                NavMesh.AllAreas);

            if(!seekerFound || !targetFound)
            {
                continue;
            }

            seekerSpawn = seekerHit.position; 
            targetSpawn = targetHit.position;

            foundValidSpawn = Vector3.Distance(seekerSpawn, targetSpawn) >= minSpawnDistance;
            
        }
        while (
            (!foundValidSpawn && attempts < maxAttempts) // keep looping above if
        );

        if (!foundValidSpawn)
        {
            Debug.LogWarning("Could not ifnd valid spawn positions on the NavMesh");
            return;
        }

        seekerAgent.Warp(seekerSpawn);

        targetAgent.Warp(targetSpawn);
    }

    public void SetUseRandomSpawn(bool decision)
    {
        useRandomSpawn = decision;
    }

    // this function stores all hiders and chooses nearest one as current targetAgent
    public void SetTargets(List<NavMeshAgent> targets)
    {
        targetAgents.Clear();
        targetAgents.AddRange(targets);
        targetAgent = FindNearestTarget();

        if (targetAgent != null && seekerAgent != null)
        {
            prevDistance = Vector3.Distance(seekerAgent.transform.position, targetAgent.transform.position);
        }
    }

    private void ApplyVisibilityBasedRewards(float rotate,float move)
    {
        if (targetAgent == null || seekerAgent == null)
        {
            return;
        }

        Vector3 localDir;
        float normalisedDist;

        bool currentlyCanSeeTarget = GetTargetInfo(out localDir, out normalisedDist);

        float currentDistance = Vector3.Distance(seekerAgent.transform.position, targetAgent.transform.position);

        // Positive if seeker got closer.
        // Negative if seeker moved farther away.
        float distanceDiff = prevDistance - currentDistance;

        /*
            Small general progress reward.

            This helps learning because the agent still gets feedback when it
            accidentally moves closer, even if the target is not visible yet.

            If you want stricter realism later, reduce this or remove it.
        */
        AddReward(distanceDiff * generalDistanceRewardScale);

        if (currentlyCanSeeTarget)
        {
            ApplyChaseRewards(distanceDiff);
        }
        else
        {
            ApplySearchRewards(rotate, move);
        }

        AddReward(timePenalty);

        prevDistance = currentDistance;
    }

    private void ApplyChaseRewards(float distanceDiff)
    {
        // Stronger reward for getting closer while the hider is visible.
        AddReward(distanceDiff * visibleDistanceRewardScale);

        // Small reward for keeping the hider in sight.
        AddReward(maintainSightReward);
    }

    private void ApplySearchRewards(float rotate, float move)
    {
        // Small penalty because the seeker does not currently see the hider.
        AddReward(unseenPenalty);

        // Tiny reward for meaningful movement during search.
        // This discourages spinning in place forever.
        if (move > 0.1f)
        {
            AddReward(searchMoveReward);
        }

        // Very small penalty for excessive spinning.
        // Keep this tiny because the seeker still needs to rotate to scan with rays.
        AddReward(-Mathf.Abs(rotate) * rotationPenaltyScale);
    }

    private bool HasTargetAndNavAgent()
    {
        if (targetAgent == null && targetAgents.Count > 0)
        {
            targetAgent = FindNearestTarget();
        }

        return targetAgent != null && seekerAgent != null && seekerAgent.isOnNavMesh; // maybe exclude isOnNavMesh
    }

    private NavMeshAgent FindNearestTarget()
    {
        NavMeshAgent nearestTarget = null;
        float nearestDistance = float.MaxValue;

        foreach (NavMeshAgent possibleTarget in targetAgents)
        {
            if (possibleTarget == null) // skip function if there are no possible targets
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, possibleTarget.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTarget = possibleTarget;
            }
        }

        return nearestTarget;
    }
}
