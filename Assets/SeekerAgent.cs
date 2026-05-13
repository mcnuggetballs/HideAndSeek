using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors; // for collect observation
using UnityEngine;

// think of it like how to reward my agent into doing patrol/chase
public class SeekerAgent : Agent
{
    public Collider[] spawnAreas;
    //public NavMeshAgent agent;
    public Rigidbody rb;
    public float moveSpeed;
    public float rotateSpeed;

    //public Rigidbody rb;

    public Transform target;

    public Transform test;

    float theDistance = 0;

    //reset seeker environmemt
    public override void OnEpisodeBegin()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // random seeker spawn
        Collider seekerArea = spawnAreas[Random.Range(0, spawnAreas.Length)];
        Bounds seekerBounds = seekerArea.bounds;

        transform.position = new Vector3(
            Random.Range(seekerBounds.min.x, seekerBounds.max.x),
            2.0f,
            Random.Range(seekerBounds.min.z, seekerBounds.max.z)
        );

        // random target spawn
        Collider targetArea = spawnAreas[Random.Range(0, spawnAreas.Length)];
        Bounds targetBounds = targetArea.bounds;

        target.position = new Vector3(
            Random.Range(targetBounds.min.x, targetBounds.max.x),
            2.0f,
            Random.Range(targetBounds.min.z, targetBounds.max.z)
        );

        transform.rotation = Quaternion.identity;
        theDistance = Vector3.Distance(transform.position, target.transform.position);
    }

    // just putting information in his brain
    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 dirToTarget = transform.InverseTransformDirection((target.position - transform.position).normalized);
        sensor.AddObservation(dirToTarget);

        sensor.AddObservation(Vector3.Distance(transform.position,target.transform.position)); // distance to target
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

        // small reward if moved closer
        float distanceReward = Mathf.Clamp(theDistance - currentdistance, -1f, 1f);
        AddReward(distanceReward * 0.01f);
        theDistance = currentdistance;

        // small time penalty to encourage faster decisions
        AddReward(-0.001f);
    }

    // what happens after collision
    public void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Hider"))
        {
            // large reward
            AddReward(1.0f);
            EndEpisode();
        }
    }
}
