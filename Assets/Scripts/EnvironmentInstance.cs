using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
public class EnvironmentInstance
{
    // ROOT
    public GameObject root; // Environment_0, Environment_1 
    public Transform runtimeRoot; // where all spawned objects go
    //public Transform editorRoot; // where all spawned objects go

    // DATA
    public ScenarioGrid grid;

    // WORLD STATE
    public bool isBuilt;
    public bool navMeshReady;

    // AGENTS
    public List<SeekerAgent> seekers = new();
    public List<NavMeshAgent> hiders = new();

    // HELPERS
    public Vector3 GetOrigin()
    {
        return grid != null ? grid.Origin : root.transform.position;  
    }

    public void Clear()
    {
        seekers.Clear();
        hiders.Clear();
        isBuilt = false;
        navMeshReady = false;
    }

}
