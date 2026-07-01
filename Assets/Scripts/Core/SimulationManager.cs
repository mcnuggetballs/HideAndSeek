using JetBrains.Annotations;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

// Listens to simulation-level events (play pause reset) and controls the active runtime agents (enable disable) and track simulation state
// Builds runtime scene from truth when "play" clicked

/*
    TrainingScene - set startupSpawnMode to TrainingGrid, assign simulationPrefab, spawns 16 environments.
    TestingScene - set startupSpawnMode to None when one SimulationPrefab is already placed in the scene.
 
 */

public class SimulationManager : MonoBehaviour
{
    private enum StartupSpawnMode
    {
        None,
        SingleEnvironment,
        TrainingGrid
    }

    public GameObject simulationPrefab;
    [SerializeField] private StartupSpawnMode startupSpawnMode = StartupSpawnMode.None;

    [SerializeField] private int rowCount = 4;
    public float spaceBetween = 80;

    public GameObject seekerPrefab;
    public GameObject hiderPrefab;
    public GameObject obstaclePrefab;

    [SerializeField] private Transform environment;
    [SerializeField] private ScenarioGrid grid;
    [SerializeField] private float placementYOffset = 0.02f;

    // keep track of agents and environments
    private readonly List<GameObject> runtimeObjects = new List<GameObject>();
    private readonly List<GameObject> spawnedEnvironments = new List<GameObject>();

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

    private void Start()
    {
        switch (startupSpawnMode)
        {
            case StartupSpawnMode.SingleEnvironment:
                SpawnTesting(simulationPrefab);
                break;
            case StartupSpawnMode.TrainingGrid:
                SpawnTraining(simulationPrefab);
                break;
        }
    }

    private void PlaySimulation()
    {
        Debug.Log("Play Simulation.");

        ClearRuntimeObjects();
        BuildRuntimeObjectsFromGrid();
    }

    private void PauseSimulation()
    {
        Debug.Log("Pause Simulation.");

        ClearRuntimeObjects();
    }

    private void ResetSimulation()
    {
        Debug.Log("Reset Simulation.");

        ClearRuntimeObjects();
    }

    private void BuildRuntimeObjectsFromGrid()
    {
        if (grid == null)
        {
            Debug.LogWarning("Cannot build simulation because no ScenarioGrid is assigned.");
            return;
        }

        for (int row = 0; row < grid.Height; row++)
        {
            for (int col = 0; col < grid.Width; col++)
            {
                Vector2Int cell = new Vector2Int(col, row);
                char cellValue = grid.GetCell(cell);
                GameObject prefab = GetRuntimePrefabForCell(cellValue);

                if (prefab == null)
                {
                    continue;
                }

                Vector3 worldPosition = grid.CellToWorld(cell);
                GameObject runtimeObject = Instantiate(prefab, worldPosition, Quaternion.identity, environment);
                PlaceObjectOnSurface(runtimeObject, worldPosition);
                runtimeObjects.Add(runtimeObject);
            }
        }
    }

    private GameObject GetRuntimePrefabForCell(char cellValue)
    {
        switch (cellValue)
        {
            case ScenarioGrid.SeekerCell:
                return seekerPrefab;
            case ScenarioGrid.HiderCell:
                return hiderPrefab;
            case ScenarioGrid.WallCell:
                return obstaclePrefab;
            default:
                return null;
        }
    }

    private void ClearRuntimeObjects()
    {
        foreach (GameObject runtimeObject in runtimeObjects)
        {
            if (runtimeObject != null)
            {
                Destroy(runtimeObject);
            }
        }

        runtimeObjects.Clear();
    }

    private void SpawnTraining(GameObject simulationPrefab)
    {
        if (simulationPrefab == null)
        {
            Debug.LogWarning("Cannot spawn training environments because no simulation prefab is assigned.");
            return;
        }

        for (int i = 0; i < rowCount; i++)
        {
            for (int j = 0; j < rowCount; j++)
            {
                GameObject environment = Instantiate(simulationPrefab);
                environment.transform.position = new Vector3(
                    i * spaceBetween,
                    0,
                    j * spaceBetween);

                spawnedEnvironments.Add(environment);
            }
        }
    }

    private void SpawnTesting(GameObject simulationPrefab)
    {
        if (simulationPrefab == null)
        {
            Debug.LogWarning("Cannot spawn testing environment because no simulation prefab is assigned.");
            return;
        }

        GameObject environment = Instantiate(simulationPrefab);
        spawnedEnvironments.Add(environment);
    }

    /*
        for controlling if agent is active for ai or frozen for editing
     */
    private void EnableAgentControl(GameObject agentObject)
    {
        foreach (var behaviour in agentObject.GetComponentsInChildren<BehaviorParameters>())
        {
            behaviour.enabled = true;
        }

        foreach (var decisionRequester in agentObject.GetComponentsInChildren<DecisionRequester>())
        {
            decisionRequester.enabled = true;
        }
        foreach (var agent in agentObject.GetComponentsInChildren<Agent>())
        {
            agent.enabled = true;
        }
        foreach (var rb in agentObject.GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = false;
        }
    }

    private void DisableAgentControl(GameObject agentObject)
    {
        foreach (DecisionRequester decisionRequester in agentObject.GetComponentsInChildren<DecisionRequester>())
        {
            decisionRequester.enabled = false;
        }

        foreach (BehaviorParameters behaviorParameters in agentObject.GetComponentsInChildren<BehaviorParameters>())
        {
            behaviorParameters.enabled = false;
        }

        foreach (Agent agent in agentObject.GetComponentsInChildren<Agent>())
        {
            agent.enabled = false;
        }

        foreach (Rigidbody rb in agentObject.GetComponentsInChildren<Rigidbody>())
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    private void PlaceObjectOnSurface(GameObject objectToPlace, Vector3 surfacePosition)
    {
        if (!TryGetColliderBounds(objectToPlace, out Bounds bounds))
        {
            objectToPlace.transform.position = surfacePosition + Vector3.up * placementYOffset;
            return;
        }

        float liftAmount = surfacePosition.y - bounds.min.y + placementYOffset;
        objectToPlace.transform.position += Vector3.up * liftAmount;
    }

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
