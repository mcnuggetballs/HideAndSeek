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

    // visibility conditions
    public float viewDistance = 10f;
    public float viewAngle = 90f;

    private float prevDistance = 0;
    private bool canSeeTarget = false; // inside viewing frustum


    public Transform target;
    public Transform test;

    //reset seeker environmemt
    public override void OnEpisodeBegin()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f),0);

        // environment set up
        float minDistance=5f;
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
    }

    // just putting information in his brain
    public override void CollectObservations(VectorSensor sensor)
    {
        /* agent only knows target direction if target is visible */
        Vector3 dir = target.position - transform.position; // get vector to target from seeker
        float distance = dir.magnitude;
        float angle = Vector3.Angle(transform.forward, dir);

        canSeeTarget = (distance <= viewDistance) && (angle <= viewAngle * 0.5f); // everytime observing if can see target

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
        { // if not visible search
            // seeker cannot see hider
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

        // reward system
        // only reward if can see target
        if (canSeeTarget)
        { // reward based on how near
            AddReward((prevDistance - currentdistance) * 0.05f);
        }
        prevDistance = currentdistance;
        // small time penalty to encourage faster decisions
        AddReward(-0.0002f);
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
