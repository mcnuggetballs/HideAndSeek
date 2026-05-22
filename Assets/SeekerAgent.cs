using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors; // for CollectObservation()
using UnityEngine;

public class SeekerAgent : Agent
{
    // agent set up
    public Rigidbody rb; // for collision purposes
    public float moveSpeed;
    public float rotateSpeed;
    public Transform target;
    public Transform test;

    // visibility conditions
    public float viewDistance = 50f;
    public float viewAngle = 90f;

    // private variables
    private float prevDistance = 0;
    private bool prevCanSeeTarget = false; // to encourage them to find target again and again
    private bool canSeeTarget = false; // inside viewing frustum
    private Vector3 searchAnchor;

    /*
       calculates target information for the seeker

       return: 
       - BOOL canSeeTarget, true(seeker has clear line of sight of target) & false(too far, outside fov, blocked by something)

       out:
       - VECTOR3 localDir, direction from seeker to target, LOCAL SPACE
               why local? because seeker moves based on its own facing direction helps agent learn to rotate towards target
       - FLOAT normalisedDistance, distance from seeker to target, NORMALISED TO 0(close) to 1(far) 
               why normalise? ML agents learn better when input values stay small, consistent range.

       use world space if object cares about the world (using way points, avoid danger), 
       use local space if object cares about itself (target is in front of me, rotate towards target)
    */
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

    /*
        reset seeker environmemt
     */
    public override void OnEpisodeBegin()
    {
        // initialise rb
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        // environment set up
        float minDistance = 5f;
        float spawnRadius = 10f;
        float groundY = 0.929f;
        
        // randomise position for
        do
        {
            // agent
            transform.position = new Vector3(
                test.position.x + Random.Range(-spawnRadius, spawnRadius), // randomise x value from -10 to 10
                groundY, // keep y grounded
                test.position.z + Random.Range(-spawnRadius, spawnRadius));

            // target
            target.position = new Vector3(
                test.position.x + Random.Range(-spawnRadius, spawnRadius), // randomise x value from -10 to 10
                groundY, // keep y grounded
                test.position.z + Random.Range(-spawnRadius, spawnRadius));
        }
        while (Vector3.Distance(transform.position, target.position) < minDistance); // keep randominising if too near

        // remember previous distance between
        prevDistance = Vector3.Distance(
            target.position,
            transform.position
            ); 

        // remember starting spawn position
        searchAnchor = transform.position;

        //need to reset
        prevCanSeeTarget = false;
        canSeeTarget = false;
    }

   

    // just putting information in his brain
    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 localDir; ;
        float normalisedDist;

        DebugRenderer();

        canSeeTarget = GetTargetInfo(out localDir, out normalisedDist);
        sensor.AddObservation(canSeeTarget ? 1f : 0f); // 1 = visible, 0 = !visible
        
        // if can see target, feed these info
        if (canSeeTarget) // chase
        {
            //direction IF visible
            sensor.AddObservation(localDir); // direction towards target
            // distance IF visible
            sensor.AddObservation(normalisedDist); // distance to target 
        }
        // if cannot see target, limited info
        else
        { 
            sensor.AddObservation(Vector3.zero); // no valid direction infromation
            sensor.AddObservation(1f); // distance unknown
        }

    }

    // make decision
    public override void OnActionReceived(ActionBuffers actions)
    {
        // read rotation action
        float rotate = actions.ContinuousActions[0];

        // calculate how much i want to rotate
        Quaternion delta = Quaternion.Euler(0, rotate * rotateSpeed * Time.deltaTime, 0);

        // calculate new intended rotation
        Quaternion targetRotation = rb.rotation * delta;

        // apply the rotation
        rb.MoveRotation(targetRotation);

        // read move action                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              //float move = (actions.ContinuousActions[1] + 1f) * 0.5f;
        float move = Mathf.Clamp01(actions.ContinuousActions[1]); // negative/0 -> no movement, positive -> have movement

        // move using new intended forward direction, move in the direction of THAT rotation
        rb.MovePosition(rb.position + (targetRotation * Vector3.forward) * moveSpeed * move * Time.deltaTime); // move forward in current direction

        /* for rewards system */    

        // out variables for GetTargetInfo()
        Vector3 localDir; // localised direction
        float normalisedDist; // normalised distance

        bool currentlyCanSeeTarget = GetTargetInfo(out localDir, out normalisedDist); // reward is based on what seeker sees AFTER move/rotate
        float currentdistance = Vector3.Distance(transform.position, target.position); // positive if nearer, negative if further

        // chase reward
        if (currentlyCanSeeTarget)
        { 
            AddReward((prevDistance - currentdistance) * 0.05f); // reward based on how near
            AddReward(0.001f); // for looking at target
        }
        else // penality for not seeing target
        {
            AddReward(-0.001f); // cannot see target penalty
        }

        // time penalty
        AddReward(-0.002f);

        // reacquire target reward
        //if (currentlyCanSeeTarget && !prevCanSeeTarget)
        //{
        //    AddReward(0.01f);
        //}

        /*
            store current values so the next decision can compare against them:
            prevDistance
                - used next step to check if seeker closer or further
            prevCanSeeTarget:
                - used next step to detect whether seeker reacquired target
            canSeeTarget:
                - stores latest visibility state for debugging/observation 
         
         */
        prevDistance = currentdistance;
        prevCanSeeTarget = currentlyCanSeeTarget;
        canSeeTarget = currentlyCanSeeTarget;
    }

    // what happens after collision
    public void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Hider"))
        {
            // large reward
            AddReward(10.0f);
            EndEpisode();
        }
    }

}
