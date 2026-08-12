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
        Testing, // use configured positions
        Training // randomised scenarios
    }

    [SerializeField] private SimulationMode simulationMode = SimulationMode.Testing;

    [Header("Prefabs")]
    [SerializeField] private GameObject seekerPrefab;
    [SerializeField] private GameObject hiderPrefab;
    [SerializeField] private GameObject obstaclePrefab;

    [Header("References")]
    [SerializeField] private ScenarioGrid grid;
    [SerializeField] private RuntimeNavMeshBuilder runtimeNavMeshBuilder;
    [SerializeField] private TestingScenarioEditor scenarioEditor;
    [SerializeField] private Transform runtimeRoot; // where runtime objects live
    [SerializeField] private GameObject editorRoot; // where visual objects live

    // runtime state
    private bool isPaused = false;
    private bool isStarted = false;

    // runtime data
    private readonly List<SeekerAgent> seekerAgents = new();
    private readonly List<NavMeshAgent> hiderAgents = new();
    private Dictionary<Vector2Int, GameObject> runtimeMap = new();

    #region Lifecycle
    // Builds runtime from grid, Hide editor, Build environment, Start simultion
    private void PlaySimulation()
    {
        Debug.Log("Play Simulation.");

        // ALWAYS ensure runtime container is active BEFORE building
        if (runtimeRoot != null)
            runtimeRoot.gameObject.SetActive(true);

        // in Testing mode, hide editor visuals (switch from edit -> runtime)
        if (simulationMode == SimulationMode.Testing && editorRoot != null)
            editorRoot.SetActive(false);

        // only applies to Testing mode (user designed scenes)
        if (simulationMode == SimulationMode.Testing)
        {
            /* rebuild conditions:
             * !isStarted -> first time pressing Play (nothing has been build yet)
             * grid.IsDirty() -> grid was modified after last build so runtime must be updated
            */
            if (!isStarted || grid.IsDirty()) // so that new seekers are reflected
            {
                InitialiseEnvironment(); // consuming changes
                isStarted = true; // simulation has now been build at least once
                grid.ClearDirty(); // changes consumed, no longer dirty
                // change is consumed here?
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
        grid.ClearDirty();

        // reset state flags
        isStarted = false;
        isPaused = false;

        if (editorRoot != null) // this is what enables editing
        {
            editorRoot.SetActive(true);
        }
        if (runtimeRoot != null)
        {
            runtimeRoot.gameObject.SetActive(false);
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
    #endregion

    #region Simulation Mode & Initialisation
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
        if (!runtimeRoot.gameObject.activeSelf)
        {
            Debug.LogWarning("RuntimeObjects was inactive during build. Fixing.");
            runtimeRoot.gameObject.SetActive(true);
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

        BuildEnvironmentFromGrid();
    }
    #endregion

    #region Build Pipeline
    private void BuildEnvironmentFromGrid()
    {
        BuildObstaclesOnly();
        Physics.SyncTransforms(); // important when colliders instantiated, bake NavMesh
        runtimeNavMeshBuilder.RebuildNavMesh();
        BuildAgentsOnly();
        AssignRuntimeTargets();
    }
    void BuildObstaclesOnly()
    {
        foreach (var cell in grid.GetAllCells())
        {
            if (grid.GetCell(cell) == ScenarioGrid.WallCell)
            {
                Vector3 worldPos = grid.CellToWorld(cell);
                GameObject obj = null;

                obj = Instantiate(obstaclePrefab, worldPos, Quaternion.identity, runtimeRoot);
                //PlaceObjectOnSurface(obj, worldPos);
            }
        }
    }

    // FULL rebuild
    void BuildAgentsOnly()
    {
        seekerAgents.Clear();
        hiderAgents.Clear();

        foreach (var cell in grid.GetAllCells())
        {
            char value = grid.GetCell(cell);
            if (value == ScenarioGrid.WallCell)
                continue;
            SpawnRuntimeObject(cell, value);
        }
    }
    #endregion

    #region Runtime Updates
    // INCREMENTAL update, only 1 cell change
    public void UpdateRuntimeCell(Vector2Int cell, char value) // visual
    {
        RemoveRuntimeObjectAt(cell);

        if (value == ScenarioGrid.EmptyCell)
            return;

        SpawnRuntimeObject(cell, value);
    }

    // Helper function to build agents
    private GameObject SpawnRuntimeObject(Vector2Int cell, char value)
    {
        Vector3 worldPos = grid.CellToWorld(cell);
        GameObject obj = null;

        switch (value)
        {
            case ScenarioGrid.WallCell:
                obj = Instantiate(obstaclePrefab, worldPos, Quaternion.identity, runtimeRoot);
                break;

            case ScenarioGrid.SeekerCell:
                obj = Instantiate(seekerPrefab, worldPos, Quaternion.identity, runtimeRoot);
                var seeker = obj.GetComponentInChildren<SeekerAgent>();
                if (seeker != null)
                    seekerAgents.Add(seeker);
                break;

            case ScenarioGrid.HiderCell:
                obj = Instantiate(hiderPrefab, worldPos, Quaternion.identity, runtimeRoot);
                var hider = obj.GetComponentInChildren<NavMeshAgent>();
                if (hider != null)
                    hiderAgents.Add(hider);
                break;
        }

        if (obj != null)
            runtimeMap[cell] = obj;

        return obj;
    }

    private void RemoveRuntimeObjectAt(Vector2Int cell)
    {
        if (runtimeMap.TryGetValue(cell, out GameObject obj))
        {
            var seeker = obj.GetComponentInChildren<SeekerAgent>();
            if (seeker != null)
            {
                seekerAgents.Remove(seeker);
            }
            var hider = obj.GetComponent<NavMeshAgent>();
            if (hider != null)
            {
                hiderAgents.Remove(hider);
            }
            Destroy(obj);
            runtimeMap.Remove(cell);
        }
    }

    // For merging Shao Cong's progress
    // Gives every seeker the full hider list; each seeker currently the ffollows the nearest target.
    private void AssignRuntimeTargets()
    {
        if (seekerAgents.Count == 0)
        {
            Debug.LogWarning("No seekers found.");
            return;
        }

        if (hiderAgents.Count == 0)
        {
            Debug.LogWarning("No hiders found. Seekers will have no targets.");
        }

        foreach (SeekerAgent seeker in seekerAgents)
        {
            if (seeker == null) continue;

            //ensure seeker belongs to the environment
            if (!seeker.transform.IsChildOf(runtimeRoot))
            {
                Debug.LogWarning("Seeker not part of this environment");
                continue;
            }

            seeker.SetTargets(hiderAgents); // pass copy
        }
    }
    #endregion

    #region Cleanup & Utilities
    private void ClearRuntimeObjects()
    {
        seekerAgents.Clear();
        hiderAgents.Clear();

        foreach (Transform child in runtimeRoot)
        {
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }

        runtimeMap.Clear();
    }
    public bool IsStarted()
    {
        return isStarted;
    }

    public bool IsPaused()
    {
        return isPaused;
    }

    public bool IsTrainingMode()
    {
        return simulationMode == SimulationMode.Training;
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


