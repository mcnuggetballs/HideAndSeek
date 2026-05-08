using UnityEngine;

using Unity.MLAgents;
using Unity.MLAgents.Sensors; // for collect observation
using Unity.MLAgents.Actuators;
using UnityEngine.AI;

// think of it like how to reward my agent into doing patrol/chase
public class SeekerAgent : Agent
{
    public NavMeshAgent agent;
    public float moveSpeed;
    public float rotateSpeed;
    private float prevDistance=0;

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

        float prevDistance = (target.position - transform.position).magnitude; // to encourage agent to keep moving closer to target
    }

    // just putting information in his brain
    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 dir = target.position - transform.position;
        sensor.AddObservation(dir.normalized); // telling the agent to add into his brain
        sensor.AddObservation(dir.magnitude); 
    }

    // make decision
    public override void OnActionReceived(ActionBuffers actions)
    {
        // i want it to move and rotate
        float rotate = actions.ContinuousActions[0]; // telling him he can control this
        transform.Rotate(Vector3.up,rotateSpeed*rotate*Time.deltaTime);

        float move = actions.ContinuousActions[1];
        transform.Translate(transform.forward * moveSpeed * move * Time.deltaTime);

        float currentdistance = Vector3.Distance(transform.position, target.position);

        float distanceDiff = prevDistance - currentdistance;

        // rewards
        AddReward(distanceDiff * 0.1f);
        prevDistance = currentdistance;

        if (currentdistance < 1.5f) 
        {
            AddReward(1.0f);
            EndEpisode();
        }
    }
}
