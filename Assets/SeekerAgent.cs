using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors; // for collect observation
using UnityEngine;



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
        /* agent knows hider exact direction and distance */ 
        Vector3 localDir = transform.InverseTransformDirection(target.position - transform.position); // direction towards target
        sensor.AddObservation(localDir.normalized);

        float maxDistance = 20.0f;
        sensor.AddObservation(localDir.magnitude / maxDistance); // distance to target

        /* agent only knows target direction if target is visible */

        Vector3 direction = target.position - transform.position; // get vector to target from seeker
        float distance = direction.magnitude; 

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

        // keep track of movement
        float currentdistance = Vector3.Distance(transform.position, target.position);
        // small reward if moved closer, penalise if further
        AddReward((prevDistance - currentdistance) * 0.05f);
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
