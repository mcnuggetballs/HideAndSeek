using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors; // for collect observation
using UnityEngine;



// think of it like how to reward my agent into doing patrol/chase
public class SeekerAgent : Agent
{
    //public NavMeshAgent agent;
    public Rigidbody rb;
    public float moveSpeed;
    public float rotateSpeed;
    private float prevDistance = 0;

    //public Rigidbody rb;

    public Transform target;

    public Transform test;

    //reset seeker environmemt
    public override void OnEpisodeBegin()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.rotation = Quaternion.identity;
        float minDistance=5f;
        float spawnRadius = 10f;
        float groundY = 0.929f;


        //Set y to  default yk
        Vector3 pos = transform.position;
        pos.y = groundY;
        transform.position = pos;

        Vector3 pos1 = target.position;
        pos1.y = groundY;
        target.position = pos1;

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

        prevDistance = Vector3.Distance(target.position, transform.position); // to encourage agent to keep moving closer to target
    }

    // just putting information in his brain
    public override void CollectObservations(VectorSensor sensor)
    {
        // know where is target
        Vector3 dir = target.position - transform.position; // direction towards target
        sensor.AddObservation(dir.normalized);

        float maxDistance = 20.0f;
        sensor.AddObservation(dir.magnitude / maxDistance); // distance to target

        sensor.AddObservation(transform.forward); // direction im facing now
    }

    // make decision
    public override void OnActionReceived(ActionBuffers actions)
    {

        // either rotate
        float rotate = actions.ContinuousActions[0]; // telling him he can control this
        Quaternion delta = Quaternion.Euler(0, rotate * rotateSpeed * Time.deltaTime, 0);
        rb.MoveRotation(rb.rotation * delta);

        // or rotate      
        float move = actions.ContinuousActions[1];
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

    // 1 fixed update = 1 step/ 1 decision from agent
}
