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

    public GameObject seekerPrefab;
    public GameObject hiderPrefab;
    public GameObject obstaclePrefab;

    // belongs to each mgr instance
    private readonly List<SeekerAgent> seekerAgents = new List<SeekerAgent>();
    private readonly List<NavMeshAgent> hiderAgents = new List<NavMeshAgent>();

    [Header("References")]
    [SerializeField] private ScenarioGrid grid;
    private readonly List<GameObject> runtimeObjects = new List<GameObject>();
    [SerializeField] private RuntimeNavMeshBuilder runtimeNavMeshBuilder;
    [SerializeField] private Transform environment; // runtime objects



    // yet to implement
    private bool isPaused = false; // for pause simulation

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
        Time.timeScale = 1f; // let time continue 

        ClearRuntimeObjects();
        BuildObstaclesFromGrid();

        if (runtimeNavMeshBuilder != null)
        {
            runtimeNavMeshBuilder.RebuildNavMesh();
        }
        else
        {
            Debug.LogWarning("Cannot rebuild NavMesh because no RuntimeNavMeshBuilder is assigned.");
        }

        BuildAgentsFromGrid();
        AssignRuntimeTargets();
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


    private void BuildObstaclesFromGrid()
    {
        BuildObjectsFromCellType(ScenarioGrid.WallCell, obstaclePrefab);

    }

    private void BuildObjectsFromCellType(char targetCellValue, GameObject prefab)
    {
        if (grid == null)
        {
            Debug.LogWarning("Cannot build simulation because no ScenarioGrid is assigned.");
            return;
        }

        if (prefab == null)
        {
            Debug.LogWarning($"Cannot build objects for cell '{targetCellValue}'");
            return;
        }

        for (int row = 0; row < grid.Height; row++)
        {
            for (int col = 0; col < grid.Width; col++)
            {
                Vector2Int cell = new Vector2Int(col, row);
                char cellValue = grid.GetCell(cell); // get the character "x,s,h"

                if (cellValue != targetCellValue)
                {
                    continue;
                }

                Vector3 worldPosition = grid.CellToWorld(cell);
                GameObject runtimeObject = Instantiate(prefab, worldPosition, Quaternion.identity, environment);
                PlaceObjectOnSurface(runtimeObject, worldPosition);
                runtimeObjects.Add(runtimeObject);

                if (targetCellValue == ScenarioGrid.HiderCell)
                {
                    NavMeshAgent spawnedHider = runtimeObject.GetComponentInChildren<NavMeshAgent>();
                    if (spawnedHider != null)
                    {
                        hiderAgents.Add(spawnedHider);
                    }
                }
                if (targetCellValue == ScenarioGrid.SeekerCell)
                {
                    SeekerAgent spawnedSeeker = runtimeObject.GetComponentInChildren<SeekerAgent>();
                    if (spawnedSeeker != null)
                    {
                        spawnedSeeker.SetUseRandomSpawn(ShouldUseRandomSpawn());
                        seekerAgents.Add(spawnedSeeker);

                    }
                }
            }
        }
    }
    private void BuildAgentsFromGrid()
    {
        BuildObjectsFromCellType(ScenarioGrid.SeekerCell, seekerPrefab);
        BuildObjectsFromCellType(ScenarioGrid.HiderCell, hiderPrefab);


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
            if (!seeker.transform.IsChildOf(environment))
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

        foreach (GameObject runtimeObject in runtimeObjects)
        {
            if (runtimeObject != null)
            {
                runtimeObject.SetActive(false); // make inactive first
                Destroy(runtimeObject);
            }
        }

        runtimeObjects.Clear();
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
}
