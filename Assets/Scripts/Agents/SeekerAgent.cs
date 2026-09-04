using System.Collections.Generic;
using TMPro;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.AI;

// agents should only report state,not modify global systems directly

public class SeekerAgent : Agent
{
    [Header("Agent Setup")]
    [SerializeField] private NavMeshAgent seekerAgent;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotateSpeed = 180f;

    // runtime state
    private NavMeshAgent targetAgent;
    private readonly List<NavMeshAgent> targetAgents = new List<NavMeshAgent>();
    private float prevDistance = 0f;
    private Vector2Int previousCell; // for wasSeen layer, mark when cell changes

    [Header("Perception Settings")]
    [SerializeField] private float viewDistance = 50f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private float eyeHeight = 0.5f;
    [SerializeField] private float catchDistance = 1.5f;

    private SimulationController simulationController;

    [Header("Reward Settings")]
    private float visibleDistanceRewardScale = 0.04f;
    private float generalDistanceRewardScale = 0.005f;
    private float maintainSightReward = 0.001f;
    private float unseenPenalty = -0.0001f;
    private float searchMoveReward = 0.0001f;
    private float rotationPenaltyScale = 0.00005f;
    private float timePenalty = -0.0002f;
    private float catchReward = 10f;

    [Header("InfluenceMap Settings")]
    [SerializeField] private bool useInfluenceMap;
    [SerializeField] private InfluenceMap influenceMap;

    #region Public API
    private void Awake()
    {
        seekerAgent.updateRotation = false; // disable agent rotation

        if (seekerAgent == null)
        {
            seekerAgent = GetComponent<NavMeshAgent>();
        }

        simulationController = GetComponentInParent<SimulationController>();
    }

    public void Initialize(InfluenceMap map)
    {
        influenceMap = map;
    }
    
    void Update()
    {


    }

    // Agent reset
    public void ResetMovement(Vector3 spawnPosition)
    {
        if (seekerAgent != null)
        {
            seekerAgent.Warp(spawnPosition); // set to spawn position
            seekerAgent.ResetPath(); // navmesh path
            seekerAgent.velocity = Vector3.zero;

        }
        else
        {
            transform.position = spawnPosition;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        targetAgent = null;
    }

    // this function stores all hiders and chooses nearest one as current targetAgent
    // also linked environment to agents
    public void SetTargets(List<NavMeshAgent> targets)
    {
        targetAgents.Clear();
        targetAgents.AddRange(targets); // must only receive local hider agents
        targetAgent = FindNearestTarget();

        if (targetAgent != null && seekerAgent != null)
        {
            prevDistance = Vector3.Distance(
                seekerAgent.nextPosition, // changed to get navigation simulation position instead of visual transform
                targetAgent.nextPosition);
        }
    }
    #endregion

    #region ML Lifecycle
    public override void OnEpisodeBegin()
    {
        if (!HasTargetAndNavAgent())
        {
            return;
        }
        // no destruction, no rebuilding
        simulationController.ResetEnvironment();
        

        prevDistance = Vector3.Distance(seekerAgent.transform.position, targetAgent.transform.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // target observations //
        if (targetAgent == null)
        {
            sensor.AddObservation(0);
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(1f);
            return;
        }
        else
        {
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

        // influence map observations //
        if (useInfluenceMap && influenceMap != null)
        {
            // basic 
            //float influence = influenceMap.GetCombinedInfluence(transform.position);
            //sensor.AddObservation(influence);

            // spatial awareness
            //Vector2Int cell = influenceMap.WorldToCell(transform.position);

            //sensor.AddObservation(influenceMap.GetValue(cell));
            //sensor.AddObservation(influenceMap.GetValue(cell + Vector2Int.up));
            //sensor.AddObservation(influenceMap.GetValue(cell + Vector2Int.right));
            //sensor.AddObservation(influenceMap.GetValue(cell + Vector2Int.down));
            //sensor.AddObservation(influenceMap.GetValue(cell + Vector2Int.left));


        }
        else
        {
            sensor.AddObservation(0f);
        }

    }

    // Catch Logic
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

        // catch logic
        float dist = Vector3.Distance(seekerAgent.nextPosition, targetAgent.nextPosition);
        float combinedRadius = seekerAgent.radius + targetAgent.radius;
        float effectiveCatchDistance = Mathf.Max(catchDistance, combinedRadius);

        if (dist < effectiveCatchDistance)
        {
            AddReward(catchReward);
            EndEpisode();
            return;
        }

        ApplyVisibilityBasedRewards(rotate, move);
    }
    #endregion

    #region Helper Functions
    // uses distance for perception
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
    // safety check
    private bool HasTargetAndNavAgent()
    {
        if (targetAgent == null || !targetAgent.isOnNavMesh)
        {
            if (targetAgents.Count == 0)
            { targetAgent = FindNearestTarget(); }
        }

        return targetAgent != null
            && seekerAgent != null
            && seekerAgent.isOnNavMesh;
    }

    private void OnDrawGizmos()
    {
        if (targetAgent == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, targetAgent.transform.position);
    }
    #endregion

    #region Reward System
    // uses distance for reward shaping
    private void ApplyVisibilityBasedRewards(float rotate, float move)
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

    // chase behaviour separation
    private void ApplyChaseRewards(float distanceDiff)
    {
        // Stronger reward for getting closer while the hider is visible.
        AddReward(distanceDiff * visibleDistanceRewardScale);

        // Small reward for keeping the hider in sight.
        AddReward(maintainSightReward);
    }

    // search behaviour separation
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
    #endregion
}
