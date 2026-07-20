using Grpc.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// each environment prefab = 1 simulation mgr

// Listens to simulation-level events (play pause reset) and controls the active runtime agents (enable disable) and track simulation state
// Builds runtime scene from truth when "play" clicked

public class EnvironmentManager : MonoBehaviour
{
    public enum SimulationMode
    {
        Testing, // keep configured positions
        Training // randomise
    }

    [SerializeField] private SimulationMode simulationMode = SimulationMode.Testing;

    [Header("Prefabs")]
    public GameObject seekerPrefab;
    public GameObject hiderPrefab;
    public GameObject obstaclePrefab;

    // belongs to each mgr instance
    private readonly List<SeekerAgent> seekerAgents = new();
    private readonly List<NavMeshAgent> hiderAgents = new();

    [Header("References")]
    [SerializeField] private ScenarioGrid grid;
    [SerializeField] private Transform runtimeObjects; // environment container
    [SerializeField] private RuntimeNavMeshBuilder runtimeNavMeshBuilder;


    private bool isPaused = false; // yet to implement

    private void Start()
    {
        Debug.Log($"{name} Start() called");

        if (simulationMode == SimulationMode.Training)
        {
            Debug.LogWarning($"{name} Entering training intialisation");
            InitialiseEnvironment();
        }
    }

    private void InitialiseEnvironment()
    {

        Debug.Log($"{name} InitialiseEnvironment() called");

        ClearRuntimeObjects();

        if(simulationMode == SimulationMode.Training)
        { 
        var generator = GetComponent<RandomScenarioGenerator>();
        if (generator != null)
        {
            generator.Generate();

        }
        else
        {
            Debug.LogWarning("No RandomScenarioGenerator found!");
        }
        }

        BuildFromGrid();
        runtimeNavMeshBuilder.RebuildNavMesh();
        AssignRuntimeTargets();
    }

    #region Training
    private void OnEnable()
    {
        // simulation maanger listens to play/pause.reset and agent spawning
        GameEvents.PlayRequested += PlaySimulation;
        GameEvents.PauseRequested += PauseSimulation;
        GameEvents.ResetRequested += ResetSimulation;
    }

    private void OnDisable()
    {
        GameEvents.PlayRequested -= PlaySimulation;
        GameEvents.PauseRequested -= PauseSimulation;
        GameEvents.ResetRequested -= ResetSimulation;
    }

    private void PlaySimulation()
    {
        Debug.Log("Play Simulation.");

        isPaused = false;
        Time.timeScale = 1f;

        InitialiseEnvironment();
    }

    // should freeze but not destroy
    private void PauseSimulation()
    {
        Debug.Log("Pause Simulation.");

        isPaused = true;
        Time.timeScale = 0f;
    }

    // destroy
    private void ResetSimulation()
    {
        Debug.Log("Reset Simulation.");

        Time.timeScale = 1f;
        ClearRuntimeObjects();
    }

    public void ResetEnvironment()
    {
        InitialiseEnvironment();
    }

    private void BuildFromGrid()
    {
        seekerAgents.Clear();
        hiderAgents.Clear();

        foreach (var cell in grid.GetAllCells())
        {
            char value = grid.GetCell(cell); // know cell type of each grid
            Vector3 worldPos = grid.CellToWorld(cell); // converted pos 

            GameObject obj = null;

            if (value == ScenarioGrid.WallCell)
            {
                obj = Instantiate(obstaclePrefab, worldPos, Quaternion.identity, runtimeObjects);
            }
            else if (value == ScenarioGrid.SeekerCell)
            {
                obj = Instantiate(seekerPrefab, worldPos, Quaternion.identity, runtimeObjects);
                var seeker = obj.GetComponentInChildren<SeekerAgent>();
                if (seeker != null)
                {
                    seeker.SetUseRandomSpawn(ShouldUseRandomSpawn());
                    seekerAgents.Add(seeker);
                }
            }
            else if (value == ScenarioGrid.HiderCell)
            {
                obj = Instantiate(hiderPrefab, worldPos, Quaternion.identity, runtimeObjects);
                var hider = obj.GetComponentInChildren<NavMeshAgent>();
                if (hider != null)
                {
                    hiderAgents.Add(hider);
                }
            }
            if (obj != null)
            {
                PlaceObjectOnSurface(obj, worldPos);
            }
        }
        Debug.Log($"[{name}] BuildFromGrid running");
    }

    // Gives every seeker the full hider list; each seeker currently follows the nearest target.
    private void AssignRuntimeTargets()
    {
        if (seekerAgents.Count == 0 || hiderAgents.Count == 0)
        {
            Debug.LogWarning("Cannot assign target because there are not seekers or hiders.");
            return;
        }


        foreach (SeekerAgent seeker in seekerAgents)
        {
            if (seeker == null) continue;

            //ensure seeker belongs to the environment
            if (!seeker.transform.IsChildOf(runtimeObjects))
            {
                Debug.LogWarning("Seeker not part of this environment");
                continue;
            }

            seeker.SetUseRandomSpawn(ShouldUseRandomSpawn());
            seeker.SetTargets(new List<NavMeshAgent>(hiderAgents)); // pass copy
        }
    }

    private bool ShouldUseRandomSpawn()
    {
        return simulationMode == SimulationMode.Training;
    }

    // this functions makes old runtime objects inactive before destroying them to stop affecting runtime navMesh rebuilds
    private void ClearRuntimeObjects()
    {
        seekerAgents.Clear();
        hiderAgents.Clear();

        foreach (Transform child in runtimeObjects)
        {
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }

    }

    private void PlaceObjectOnSurface(GameObject objectToPlace, Vector3 surfacePosition)
    {
        // checks all non trigger collider to find lowest point
        if (!TryGetColliderBounds(objectToPlace, out Bounds bounds))
        {
            // lift up to surface
            objectToPlace.transform.position = surfacePosition + Vector3.up;
            return;
        }

        float liftAmount = surfacePosition.y - bounds.min.y;
        objectToPlace.transform.position += Vector3.up * liftAmount;
    }

    // this function solves the object spawning halfway inside plane problem
    private bool TryGetColliderBounds(GameObject targetObject, out Bounds combinedBounds)
    {
        Collider[] colliders = targetObject.GetComponentsInChildren<Collider>();
        combinedBounds = new Bounds(targetObject.transform.position, Vector3.zero);
        bool hasBounds = false;

        foreach (Collider collider in colliders)
        {
            if (collider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = collider.bounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(collider.bounds);
        }

        return hasBounds;
    }
    #endregion
}


