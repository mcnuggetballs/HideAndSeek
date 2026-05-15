using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors; // for collect observation
using UnityEngine;
using UnityEngine.UIElements;

// think of it like how to reward my agent into doing patrol/chase
public class SeekerAgent : Agent
{
    // agent set up
    public Rigidbody rb; // for collision purposes
    public float moveSpeed;
    public float rotateSpeed;
    public Transform target;
    public Transform test;

    // visibility conditions
    public float viewDistance = 10f;
    public float viewAngle = 90f;

    // private variables
    private float prevDistance = 0;
    private bool canSeeTarget = false; // inside viewing frustum
    private bool previousCanSeeTarget = false; // to encourage them to find target again and again
    private Vector3 searchAnchor;

    //reset seeker environmemt
    public override void OnEpisodeBegin()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        // environment set up
        float minDistance = 5f;
        float spawnRadius = 10f;
        float groundY = 0.929f;

        do
        {
            // random position for agent
            transform.position = new Vector3(
                test.position.x + Random.Range(-spawnRadius, spawnRadius), // randomise x value from -10 to 10
                groundY, // keep y grounded
                test.position.z + Random.Range(-spawnRadius, spawnRadius));

            //// random position for target
            target.position = new Vector3(
                test.position.x + Random.Range(-spawnRadius, spawnRadius), // randomise x value from -10 to 10
                groundY, // keep y grounded
                test.position.z + Random.Range(-spawnRadius, spawnRadius));
        }
        while (Vector3.Distance(transform.position, target.position) < minDistance); // keep randominising if too near

        prevDistance = Vector3.Distance(
            target.position,
            transform.position
            ); // to encourage agent to keep moving closer to target
        searchAnchor = transform.position; // remember spawn position
    }

    // just putting information in his brain
    public override void CollectObservations(VectorSensor sensor)
    {
        // visual info about target 
        Vector3 dir = target.position - transform.position; // get vector to target from seeker
        float angle = Vector3.Angle(transform.forward, dir);
        float distance = dir.magnitude;

        // separate distance and fov
        bool insideFOV = angle <= viewAngle * 0.5f;
        bool insideDist = distance <= viewDistance;


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

        // line of sight logic
        if (insideFOV && insideDist)
        {
            RaycastHit hit;

            if (Physics.Raycast(transform.position, dir.normalized, out hit, viewDistance))
            {

                canSeeTarget = hit.transform == target;
            }
            else
            {
                canSeeTarget = false;
            }
        }

        else { canSeeTarget = false; }

        // if can see target, feed these info
        if (canSeeTarget) // chase
        {
            // visibility state flag
            Vector3 localDir = transform.InverseTransformDirection(target.position - transform.position);
            sensor.AddObservation(1f); // visibility flag, 1 = visible & 0 == !visible

            //direction IF visible
            sensor.AddObservation(localDir.normalized); // direction towards target

            // distance IF visible
            sensor.AddObservation(distance / viewDistance); // distance to target

        }
        else
        { // if cannot see target, limited info
            // if target not visible must provide same number of observations
            sensor.AddObservation(0f); // visibility state flag
            sensor.AddObservation(Vector3.zero); // no valid direction infromation
            sensor.AddObservation(1f); // distance unknown
        }

    }

    // make decision
    public override void OnActionReceived(ActionBuffers actions)
    {

        // either rotate
        float rotate = actions.ContinuousActions[0]; // telling him he can control this
        Quaternion delta = Quaternion.Euler(0, rotate * rotateSpeed * Time.deltaTime, 0);
        rb.MoveRotation(rb.rotation * delta);

        // no backward movement      
        float move = Mathf.Clamp01(actions.ContinuousActions[1]);
        rb.MovePosition(rb.position + transform.forward * moveSpeed * move * Time.deltaTime); // move forward in current direction

        float currentdistance = Vector3.Distance(transform.position, target.position);

        // chase reward
        if (canSeeTarget)
        { // reward based on how near
            AddReward((prevDistance - currentdistance) * 0.05f);
        }
        else // move reward
        {
            float movedDistance = Vector3.Distance(transform.position, previousPosition);
            AddReward(movedDistance * 0.002f);
            previousPosition = transform.position;
        }

        prevDistance = currentdistance;

        // time penalty
        AddReward(-0.0002f);

        // reacquire target reward
        if (canSeeTarget && !previousCanSeeTarget)
        {
            AddReward(0.2f);
        }

        previousCanSeeTarget = canSeeTarget;
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
