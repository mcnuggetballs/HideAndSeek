using Grpc.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

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
    [SerializeField] private TestingScenarioEditor scenarioEditor;

    // game state
    [SerializeField] public GameObject editorRoot;
    private bool isPaused = false; // yet to implement
    private bool isStarted = false;
    private bool isEdited = false;
    private Dictionary<Vector2Int, GameObject> runtimeMap = new();

    #region Lifecycle Runs
    // Builds runtime from grid, Hide editor, Build environment, Start simultion
    private void PlaySimulation()
    {
        Debug.Log("Play Simulation.");

        // ALWAYS ensure runtime container is active BEFORE building
        if (runtimeObjects != null)
            runtimeObjects.gameObject.SetActive(true);

        // hide editor visuals
        if (simulationMode == SimulationMode.Testing && editorRoot != null)
            editorRoot.SetActive(false);

        // build after editor is hidden
        if (simulationMode == SimulationMode.Testing)
        {
            // first play, grid has been edited
            if (!isStarted || isEdited) // so that new seekers are reflected
            {
                InitialiseEnvironment();
                isStarted = true;
                isEdited = false;
            }
        }
        else // training
        {
            InitialiseEnvironment();
        }

        Time.timeScale = 1f;
        isPaused = false;


    }

    // Freeze time, DO NOT rebuild/ destroy
    private void PauseSimulation()
    {
        Debug.Log("Pause Simulation.");

        Time.timeScale = 0f; // do nothing to editor visuals
        isPaused = true;
    }

    // Destroy runtime, Clear grid/ editor visuals, Return to Editor Mode
    private void ResetSimulation()
    {
        Debug.Log("Reset Simulation.");

        Time.timeScale = 1f;

        // destroy runtime objects completely
        ClearRuntimeObjects();

        // destroy editor visuals completely
        scenarioEditor.ClearEditorVisuals();
        scenarioEditor.ResetEditorState();

        // clear grid data
        grid.ClearGrid();

        // reset state flags
        isStarted = false;
        isPaused = false;
        isEdited = false;

        if (editorRoot != null) // this is what enables editing
        {
            editorRoot.SetActive(true);
        }
        if (runtimeObjects != null)
        {
            runtimeObjects.gameObject.SetActive(false);
        }
    }
    #endregion

    // decide simulationMode based on scene name (temporary, to be fixed with proper GameManager later)
    private void Awake()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        simulationMode = sceneName.Contains("Training") ? SimulationMode.Training : SimulationMode.Testing;
    }

    private void Start()
    {
        if (simulationMode == SimulationMode.Training)
        {
            Debug.Log("Training: Auto intialise");
            InitialiseEnvironment();
            isStarted = true;
            Time.timeScale = 1f;
        }
    }

    private void InitialiseEnvironment()
    {

        Debug.Log($"{name} InitialiseEnvironment() called");

        ClearRuntimeObjects();

        // safety check
        if (!runtimeObjects.gameObject.activeSelf)
        {
            Debug.LogWarning("RuntimeObjects was inactive during build. Fixing.");
            runtimeObjects.gameObject.SetActive(true);
        }

        if (simulationMode == SimulationMode.Training)
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

        BuildRuntime();
    }

    private void BuildRuntime()
    {
        BuildObstaclesOnly();
        Physics.SyncTransforms(); // important when colliders instantiated, bake NavMesh
        runtimeNavMeshBuilder.RebuildNavMesh();
        BuildAgentsOnly();
        AssignRuntimeTargets();
    }


    // to inform runtime, grid changed (user painted a cell
    public void MarkGridDirty()
    {
        isEdited = true;
    }

    public void UpdateRuntimeCell(Vector2Int cell, char value)
    {
        Vector3 worldPos = grid.CellToWorld(cell);

        // remove existing runtime object at that cell
        RemoveRuntimeObjectAt(cell);

        GameObject obj = null;

        if (value == ScenarioGrid.WallCell)
            obj = Instantiate(obstaclePrefab, worldPos, Quaternion.identity, runtimeObjects);

        else if (value == ScenarioGrid.SeekerCell)
            obj = Instantiate(seekerPrefab, worldPos, Quaternion.identity, runtimeObjects);

        else if (value == ScenarioGrid.HiderCell)
            obj = Instantiate(hiderPrefab, worldPos, Quaternion.identity, runtimeObjects);

        if (obj != null)
            //PlaceObjectOnSurface(obj, worldPos);
            // store runtime objects when creataed
            runtimeMap[cell] = obj;

        //runtimeNavMeshBuilder.RebuildNavMesh();
    }

    private void RemoveRuntimeObjectAt(Vector2Int cell)
    {
        if (runtimeMap.TryGetValue(cell, out GameObject obj))
        {
            Destroy(obj);
            runtimeMap.Remove(cell);
        }
    }

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

    void BuildObstaclesOnly()
    {
        foreach (var cell in grid.GetAllCells())
        {
            if (grid.GetCell(cell) == ScenarioGrid.WallCell)
            {
                Vector3 worldPos = grid.CellToWorld(cell);
                GameObject obj = null;

                obj = Instantiate(obstaclePrefab, worldPos, Quaternion.identity, runtimeObjects);
                //PlaceObjectOnSurface(obj, worldPos);
            }
        }
    }
    void BuildAgentsOnly()
    {
        seekerAgents.Clear();
        hiderAgents.Clear();

        foreach (var cell in grid.GetAllCells())
        {
            char value = grid.GetCell(cell);

            if (value == ScenarioGrid.WallCell)
                continue;

            Vector3 worldPos = grid.CellToWorld(cell);

            GameObject obj = null;

            if (value == ScenarioGrid.SeekerCell)
            {
                obj = Instantiate(seekerPrefab, worldPos, Quaternion.identity, runtimeObjects);
                var seeker = obj.GetComponentInChildren<SeekerAgent>();
                if (seeker != null)
                {
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
                if (runtimeMap.TryGetValue(cell, out GameObject existing))
                {
                    Destroy(runtimeMap[cell]);runtimeMap.Remove(cell);
                }
                runtimeMap[cell] = obj;


            }
        }

    }

    // Gives every seeker the full hider list; each seeker currently the ffollows the nearest target.
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

        runtimeMap.Clear();
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

    #region Getters
    public bool IsStarted()
    {
        return isStarted;
    }

    public bool IsPaused()
    {
        return isPaused;
    }

    public Transform GetEditorRoot()
    {
        return editorRoot.transform;
    }
    public GameObject GetSeekerPrefab()
    {
        return seekerPrefab;
    }

    public GameObject GetHiderPrefab()
    {
        return hiderPrefab;
    }
    #endregion

}


