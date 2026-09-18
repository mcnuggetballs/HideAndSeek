using Unity.AI.Navigation;
using UnityEngine;

// runs when users press play = which indicates     
public class RuntimeNavMeshBuilder : MonoBehaviour
{
    [SerializeField] private NavMeshSurface navMeshSurface;

    public bool RebuildNavMesh()
    {
        if (navMeshSurface == null)
        {
            Debug.LogWarning("Cannot rebuild NavMesh because no NavMeshSurface is assigned.");
            return false;
        }

        navMeshSurface.BuildNavMesh();
        Debug.Log("Runtime NavMesh rebuilt");
        return navMeshSurface.navMeshData != null;
    }

    public void ClearNavMesh()
    {
        if (navMeshSurface != null)
            navMeshSurface.RemoveData();
    }
}
