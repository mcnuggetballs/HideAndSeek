using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class SeekerAgent : Agent
{
    [Header("Agent Setup")]
    public Rigidbody rb;
    public float moveSpeed = 5f;
    public float rotateSpeed = 180f;
    public Transform target;
    public Transform test;

    [Header("Visibility Settings")]
    public float viewDistance = 50f;
    public float viewAngle = 90f;
    public float eyeHeight = 0.5f;

    [Header("Spawn Settings")]
    public float minSpawnDistance = 5f;
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

    public override void OnEpisodeBegin()
    {
        ResetMovement();
        RandomizeSpawn();

        prevDistance = Vector3.Distance(rb.position, target.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 localDir;
        float normalisedDistance;

        bool visible = GetTargetInfo(out localDir, out normalisedDistance);
        sensor.AddObservation(visible? 1 : 0); // target visible?
        
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
        float rotate = actions.ContinuousActions[0];
        float move = Mathf.Clamp01(actions.ContinuousActions[1]);

        Quaternion targetRotation = ApplyMovement(rotate, move);

        ApplyVisibilityBasedRewards(targetRotation, rotate, move);
    }

    private bool GetTargetInfo(out Vector3 localDir, out float normalisedDistance)
    {
        // calculate for distance check
        Vector3 dir = target.position - transform.position; // get the dir vector from current position to target position
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
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f; // shoot ray from seeker's eye level

        if (Physics.Raycast(rayOrigin, dir.normalized, out RaycastHit hit, viewDistance))
        {
            return hit.transform == target || hit.transform.IsChildOf(target); // if ray hits target OR child of target first, means can see
        }

        return false;
    }
    private void ResetMovement()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    private void RandomizeSpawn()
    {
        int attempts = 0;
        int maxAttempts = 100;

        Vector3 seekerSpawn;
        Vector3 targetSpawn;

        do
        {
            attempts++;

            seekerSpawn = new Vector3(
                test.position.x + Random.Range(-spawnRadius, spawnRadius),
                groundY,
                test.position.z + Random.Range(-spawnRadius, spawnRadius)
            );

            targetSpawn = new Vector3(
                test.position.x + Random.Range(-spawnRadius, spawnRadius),
                groundY,
                test.position.z + Random.Range(-spawnRadius, spawnRadius)
            );
        }
        while (
            Vector3.Distance(seekerSpawn, targetSpawn) < minSpawnDistance &&
            attempts < maxAttempts
        );

        rb.position = seekerSpawn;
        target.position = targetSpawn;
    }

    private Quaternion ApplyMovement(float rotate, float move)
    {
        Quaternion delta = Quaternion.Euler(
            0f,
            rotate * rotateSpeed * Time.deltaTime,
            0f
        );

        Quaternion targetRotation = rb.rotation * delta;

        rb.MoveRotation(targetRotation);

        Vector3 moveDirection = targetRotation * Vector3.forward;

        rb.MovePosition(
            rb.position + moveDirection * moveSpeed * move * Time.deltaTime
        );

        return targetRotation;
    }

    private void ApplyVisibilityBasedRewards(
        Quaternion targetRotation,
        float rotate,
        float move
    )
    {
        Vector3 localDir;
        float normalisedDist;

        bool currentlyCanSeeTarget = GetTargetInfo(out localDir, out normalisedDist);

        float currentDistance = Vector3.Distance(rb.position, target.position);

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

    private void DebugRenderer()
    {
        Vector3 leftBoundary =
            Quaternion.Euler(0, -viewAngle * 0.5f, 0) *
            transform.forward;

        Vector3 rightBoundary =
            Quaternion.Euler(0, viewAngle * 0.5f, 0) *
            transform.forward;

        Debug.DrawRay(
            transform.position,
            leftBoundary * viewDistance,
            Color.yellow
        );

        Debug.DrawRay(
            transform.position,
            rightBoundary * viewDistance,
            Color.yellow
        );

        Debug.DrawRay(
            transform.position,
            transform.forward * viewDistance,
            Color.red
        );
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Hider"))
        {
            AddReward(catchReward);
            EndEpisode();
        }
    }
}