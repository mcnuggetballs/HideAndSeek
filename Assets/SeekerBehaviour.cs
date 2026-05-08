using UnityEngine;
using UnityEngine.AI;

public class SeekerBehaviour : MonoBehaviour
{
    public NavMeshAgent agent; 
    public Transform target;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEpisodeBegin()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        agent.SetDestination(target.position);

    }
}
