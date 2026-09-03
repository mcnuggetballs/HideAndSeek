using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// a runtime system component that builds and mutates world
public class WorldBuilder : MonoBehaviour
{
    private ScenarioGrid grid;

    private GameObject obstaclePrefab;
    private GameObject seekerPrefab;
    private GameObject hiderPrefab;

    private Transform runtimeRoot;

    private List<SeekerAgent> seekers;
    private List<NavMeshAgent> hiders;
    private Dictionary<Vector2Int, GameObject> runtimeMap;

    // getter and setters
    public List<SeekerAgent> GetSeekers() => seekers;
    public List<NavMeshAgent> GetHiders() => hiders;

    #region Public API
    // build world, build agent later
    public void BuildGeometry(
        ScenarioGrid grid,
        GameObject obstaclePrefab,
        Transform runtimeRoot)
    {
        if (grid == null)
        {
            Debug.LogError("[WorldBuilder]: Grid is null!");
            return;
        }

        // cache references FIRST
        this.grid = grid;
        this.obstaclePrefab = obstaclePrefab;
        this.runtimeRoot = runtimeRoot;

        // initalise runtime state FIRST
        runtimeMap = new Dictionary<Vector2Int, GameObject>();
        seekers = new List<SeekerAgent>();
        hiders = new List<NavMeshAgent>();

        // now safe to clear
        ClearRuntimeObjects(); // clean old world

        // build
        BuildObstaclesOnly();
    }

    // build agent after navmesh is done
    public void BuildAgents(GameObject seekerPrefab, GameObject hiderPrefab)
    {
        if(grid == null)
        {
            Debug.LogError("[WorldBuilder]: BuildGeometry must be called before BuildAgents");
            return;
        }

        this.seekerPrefab = seekerPrefab;
        this.hiderPrefab = hiderPrefab;
        BuildAgentsOnly();
        AssignRuntimeTargets();
    }
    // mini update function for editor i think?
    public void UpdateRuntimeCell(Vector2Int cell, char value) // visual
    {
        RemoveRuntimeObjectAt(cell);

        if (value == ScenarioGrid.EmptyCell)
            return;

        GameObject obj = null;

        if (value == ScenarioGrid.WallCell)
        {
            obj = SpawnRuntimeObject(cell, obstaclePrefab);
        }
        else if (value == ScenarioGrid.SeekerCell)
        {
            obj = SpawnRuntimeObject(cell, seekerPrefab);
            AddSeeker(obj.GetComponent<SeekerAgent>());
        }
        else if (value == ScenarioGrid.HiderCell)
        {
            obj = SpawnRuntimeObject(cell, hiderPrefab);
            AddHider(obj.GetComponent<NavMeshAgent>());
        }
    }
    // full reset of runtime objects
    public void ClearRuntimeObjects()
    {
        if (runtimeMap == null)
        {
            runtimeMap = new Dictionary<Vector2Int, GameObject>();
        }

        foreach (var obj in runtimeMap.Values)
            Destroy(obj);

        runtimeMap.Clear();

        seekers.Clear();
        hiders.Clear();
    }

    public void GetSeekerTransforms(List<Transform> output)
    {
        output.Clear();

        foreach (var seeker in seekers)
        {
            if (seeker != null)
                output.Add(seeker.transform);
        }
    }
    #endregion

    #region Helper Functions
    private void BuildObstaclesOnly()
    {
        foreach (var cell in grid.GetAllCells())
        {
            if (grid.GetCell(cell) != ScenarioGrid.WallCell)
                continue;

            SpawnRuntimeObject(cell, obstaclePrefab);
        }
    }

    // Split building function cos NavMeshAgent needs NavMesh to exist
    private void BuildAgentsOnly()
    {
        foreach (var cell in grid.GetAllCells())
        {
            char value = grid.GetCell(cell);

            if (value == ScenarioGrid.SeekerCell)
            {
                var obj = SpawnRuntimeObject(cell, seekerPrefab);
                AddSeeker(obj.GetComponent<SeekerAgent>());
            }
            else if (value == ScenarioGrid.HiderCell)
            {
                var obj = SpawnRuntimeObject(cell, hiderPrefab);
                AddHider(obj.GetComponent<NavMeshAgent>());
            }
        }
    }

    // Converts emptySeekerPrefab to RuntimeSeekerPrefab
    private GameObject SpawnRuntimeObject(
        Vector2Int cell,
        GameObject prefab)
    {
        Vector3 worldPos = grid.CellToWorld(cell);
        GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity, runtimeRoot);
        runtimeMap[cell] = obj; // update

        return obj;

    }

    // Targetted handling of destruction of runtime objects
    private void RemoveRuntimeObjectAt(Vector2Int cell)
    {
        if (runtimeMap.TryGetValue(cell, out GameObject obj))
        {
            var seeker = obj.GetComponent<SeekerAgent>();
            if (seeker != null)
            {
                seekers.Remove(seeker);
            }
            var hider = obj.GetComponent<NavMeshAgent>();
            if (hider != null)
            {
                hiders.Remove(hider);
            }
            Destroy(obj);
            runtimeMap.Remove(cell);
        }
    }

    private void AddSeeker(SeekerAgent agent)
    {
        if (agent == null) return;
        if (!seekers.Contains(agent))
            seekers.Add(agent);
    }

    private void AddHider(NavMeshAgent agent)
    {
        if (agent == null) return;
        if (!hiders.Contains(agent))
            hiders.Add(agent);
    }

    // Agent Movement Reset
    public void ResetAgentsOnly() // maybe private
    {
        foreach (var seeker in seekers)
        {
            if (seeker != null)
            {
                seeker.ResetMovement(seeker.transform.position);

            }
        }
    }

    // Agent Relationship Reset
    public void AssignRuntimeTargets() // maybe private
    {
        if (seekers == null || hiders == null)
        {
            Debug.LogWarning("[WorldBuilder]: Both seeker and hider needs to exist");
            return;
        }

        foreach (SeekerAgent seeker in seekers)
        {
            if (seeker == null) continue;

            //ensure seeker belongs to the environment
            if (!seeker.transform.IsChildOf(runtimeRoot))
            {
                Debug.LogWarning("Seeker not part of this environment");
                continue;
            }

            seeker.SetTargets(hiders); // pass copy
        }
    }

    #endregion
}