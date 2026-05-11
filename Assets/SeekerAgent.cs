using UnityEngine;

using Unity.MLAgents;
using Unity.MLAgents.Sensors; // for collect observation
using Unity.MLAgents.Actuators;
using UnityEngine.AI;



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
        transform.position = test.position + Random.insideUnitSphere * 10;
        target.position = test.position + Random.insideUnitSphere * 10;

        //Set y to  default y
        Vector3 pos = transform.position;
        pos.y = 0.929f;
        transform.position = pos;

        Vector3 pos1 = target.position;
        pos1.y = 0.929f;
        target.position = pos1;

        prevDistance = (target.position - transform.position).magnitude; // to encourage agent to keep moving closer to target
    }

    // just putting information in his brain
    public override void CollectObservations(VectorSensor sensor)
    {
        // know where is target
        Vector3 dir = target.position - transform.position; // direction from seeker to target
        sensor.AddObservation(dir.normalized); 
        sensor.AddObservation(dir.magnitude);

        // need to know if walking/turning
        sensor.AddObservation(transform.forward);
        sensor.AddObservation(rb.linearVelocity);
    }

    // make decision
    public override void OnActionReceived(ActionBuffers actions)
    {

        // either rotate
        float rotate = actions.ContinuousActions[0]; // telling him he can control this
        Quaternion delta = Quaternion.Euler(0,rotate * rotateSpeed * Time.deltaTime,0);
        rb.MoveRotation(rb.rotation * delta);

        // or rotate      
        float move = actions.ContinuousActions[1];
        rb.MovePosition(rb.position + transform.forward * moveSpeed * move *Time.deltaTime); // move forward in current direction

        // keep track of movement
        float currentdistance = Vector3.Distance(transform.position, target.position);
        float distanceDiff = prevDistance - currentdistance;

        // small reward if moved closer, penalise if further
        AddReward(distanceDiff * 0.1f);
        prevDistance = currentdistance;

        // small penalty if wasting time every step to encourage faster decisions
        AddReward(-0.001f);

        // if not better than previous attempt penalty
        if(distanceDiff < 0)
        {
            AddReward(distanceDiff * 0.05f);
        }
    }

    // what happens after collision
    public void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag("Hider"))
        {
            // large reward
            AddReward(1.0f);
            EndEpisode();
        }

    }
}
