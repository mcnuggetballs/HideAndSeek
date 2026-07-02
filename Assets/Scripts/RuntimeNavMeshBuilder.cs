using Unity.AI.Navigation;
using UnityEngine;

// runs when users press play = which indicates     
public class RuntimeNavMeshBuilder : MonoBehaviour
{
    [SerializeField] private NavMeshSurface navMeshSurface;

    public void RebuildNavMesh()
    {
        navMeshSurface.BuildNavMesh();
        Debug.Log("Runtime NavMesh rebuilt");
    }
}
