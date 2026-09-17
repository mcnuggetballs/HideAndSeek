using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using static InfluenceMap;
using static ScenarioSystem;

// what kind of world do i want to create ?

// true orchestrator: 1 environment = 1 simulation controller

// Listens to simulation-level events (play pause reset) and controls the active runtime agents (enable disable) and track simulation state
// Builds runtime scene from truth when "play" clicked

// reads scenariogrid data when needed

// should orchestrate updates
public class SimulationController : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject trainingEnvironmentPrefab;
    [SerializeField] private GameObject seekerPrefab;
    [SerializeField] private GameObject hiderPrefab;
    [SerializeField] private GameObject obstaclePrefab;

    [Header("Core Systems")]
    [SerializeField] private RuntimeNavMeshBuilder runtimeNavMeshBuilder;
    [SerializeField] private ScenarioSystem scenarioSystem;
    [SerializeField] private WorldBuilder worldBuilder;
    // mode control
    public enum SimulationMode
    {
        Testing, // use configured positions
        Training // randomised scenarios
    }

    [Header("Simulation Configuration")]
    [SerializeField] private SimulationMode simulationMode; // testing or training?
    [SerializeField] private ScenarioType scenarioType; // if training, fixed or random?
    [SerializeField] private TextAsset mapFile; // for fixed

    [Header("Scene Roots")]
    [SerializeField] private Transform runtimeRoot; // where runtime objects live
    [SerializeField] private GameObject editorRoot; // where visual objects live

    private ScenarioGrid grid;


    [Header("Editor")]
    [SerializeField] private EditorController scenarioEditor; // only required for testing

    [Header("Analysis")]
    [SerializeField] private InfluenceMap influenceMap; // analysis layer
    [SerializeField] private GridRenderer gridRenderer; // analysis layer
    [SerializeField] private LayerTag debugLayer;

    public struct WorldConfig
    {
        public int width;
        public int height;
        public float cellSize;
        public Vector3 origin;
    }

    [Header("World Configuration")]
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private float cellSize;
    //[SerializeField] private Vector3 origin;

    // any system that needs width/height gets it from SimulationController.Config
    public WorldConfig Config => new WorldConfig
    {
        width = width,
        height = height,
        cellSize = cellSize,
        origin = transform.position
    };

    // runtime state
    //private bool isPaused = false;
    private bool isStarted = false;
    private bool worldBuilt = false;

    // runtime data
    private List<SeekerAgent> seekerAgents = new();
    private List<NavMeshAgent> hiderAgents = new();
    private Dictionary<Vector2Int, GameObject> runtimeMap = new(); // spatial look up for grid rebugging
    private List<Transform> seekerBuffer = new();

    public bool IsStarted => isStarted;
    public bool IsTrainingMode => simulationMode == SimulationMode.Training;



    #region Simulation Lifecycle
    // decide simulationMode based on scene name (temporary, to be fixed with proper GameManager later)
    private void Awake()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        simulationMode = sceneName.Contains("Training") ?
            SimulationMode.Training :
            SimulationMode.Testing;

    }

    private void Start()
    {
        if (IsTrainingMode && trainingEnvironmentPrefab != null)
        {
            GameObject environment = Instantiate(trainingEnvironmentPrefab, transform.position, Quaternion.identity, transform);
            runtimeRoot = environment.transform.Find("RuntimeRoot");
            runtimeNavMeshBuilder = environment.GetComponent<RuntimeNavMeshBuilder>();
            worldBuilder = environment.GetComponent<WorldBuilder>() ?? environment.AddComponent<WorldBuilder>();
            influenceMap = environment.GetComponent<InfluenceMap>() ?? environment.AddComponent<InfluenceMap>();
        }

        if (runtimeRoot == null || worldBuilder == null || runtimeNavMeshBuilder == null ||
            scenarioSystem == null || seekerPrefab == null || hiderPrefab == null || obstaclePrefab == null)
        {
            Debug.LogError("SimulationController is missing scenario or environment references.", this);
            enabled = false;
            return;
        }

        InitialiseScenario();
        if (grid == null)
        {
            enabled = false;
            return;
        }

        if (IsTrainingMode && !ScenarioSystem.HasBothTeams(grid))
        {
            Debug.LogError("Training scenario needs at least one seeker and one hider.", this);
            enabled = false;
            return;
        }

        Camera.main?.GetComponent<TopDownCameraController>()?.FrameGrid(grid);
        FitFloorToGrid();

        if (IsTrainingMode)
        {
            BuildWorld();
            isStarted = true;
        }
        else
        {
            scenarioEditor?.Initialise(grid);
            scenarioEditor?.RebuildVisualsFromGrid();
            runtimeRoot.gameObject.SetActive(false);
        }

        if (gridRenderer != null)
        {
            gridRenderer.Initialise(grid);
            gridRenderer.BuildVisualGrid();
        }
    }

    private void BuildWorld()
    {
        runtimeRoot.gameObject.SetActive(true);
        worldBuilder.BuildGeometry(grid, obstaclePrefab, runtimeRoot);
        Physics.SyncTransforms();
        runtimeNavMeshBuilder.RebuildNavMesh();
        worldBuilder.BuildAgents(seekerPrefab, hiderPrefab);
        seekerAgents = worldBuilder.GetSeekers();
        hiderAgents = worldBuilder.GetHiders();
        influenceMap?.Initialise(grid);
        foreach (var seeker in seekerAgents)
            seeker.Initialize(this, influenceMap);
        grid.ClearDirty();
        worldBuilt = true;
    }

    // Both environment prefabs use a Unity plane, ten units wide at scale one.
    private void FitFloorToGrid()
    {
        Transform floor = runtimeRoot.parent.Find("Plane");
        if (floor == null)
        {
            Debug.LogWarning("Environment has no Plane child to size to the scenario.", this);
            return;
        }

        floor.localRotation = Quaternion.identity;
        floor.localScale = new Vector3(grid.Width * grid.CellSize / 10f, floor.localScale.y,
            grid.Height * grid.CellSize / 10f);

        Transform placementArea = runtimeRoot.parent.Find("Placement Area");
        if (placementArea != null && placementArea.TryGetComponent(out BoxCollider placementCollider))
            placementCollider.size = new Vector3(grid.Width * grid.CellSize, placementCollider.size.y,
                grid.Height * grid.CellSize);
    }

    public void SavePaintedScenario()
    {
        if (IsTrainingMode || isStarted || grid == null)
            return;

        try
        {
            string path = scenarioSystem.SavePainted(grid);
            Debug.Log($"Saved scenario {grid.Width}x{grid.Height} to {path}", this);
        }
        catch (Exception exception) when (exception is ArgumentException || exception is IOException ||
                                          exception is UnauthorizedAccessException)
        {
            Debug.LogError($"Could not save scenario: {exception.Message}", this);
        }
    }

    public void LoadPaintedScenario()
    {
        if (IsTrainingMode || isStarted || grid == null)
            return;

        try
        {
            ScenarioGrid loaded = scenarioSystem.LoadPainted(grid.CellSize, grid.Origin);
            worldBuilder.ClearRuntimeObjects();
            runtimeRoot.gameObject.SetActive(false);
            grid = loaded;
            grid.MarkDirty();
            worldBuilt = false;
            FitFloorToGrid();
            Camera.main?.GetComponent<TopDownCameraController>()?.FrameGrid(grid);
            scenarioEditor.Initialise(grid);
            scenarioEditor.RebuildVisualsFromGrid();
            if (gridRenderer != null)
            {
                gridRenderer.Initialise(grid);
                gridRenderer.BuildVisualGrid();
            }
            Debug.Log($"Loaded scenario {grid.Width}x{grid.Height} from {ScenarioStorage.GetPath(scenarioSystem.SavedScenarioName)}", this);
        }
        catch (Exception exception) when (exception is ArgumentException || exception is FormatException ||
                                          exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogError($"Could not load scenario: {exception.Message}", this);
        }
    }

    // mainly needed cos of influence maps
    void Update()
    {
        if (!worldBuilt) return;
        if (influenceMap == null) return;

        worldBuilder.GetSeekerTransforms(seekerBuffer);

        // 2. update influence map
        influenceMap.UpdateAgentPositions(seekerBuffer);

        // 3. render debug view
        if (gridRenderer != null && influenceMap.TryGetLayer(debugLayer, out var data))
        {
            gridRenderer.Render(data, debugLayer);
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

    // For auto episode reset, environment does not change (for now)
    public void ResetEnvironment()
    {
        if (!runtimeRoot.gameObject.activeSelf)
            runtimeRoot.gameObject.SetActive(true);

        worldBuilder.ResetAgentsOnly(); // resets agents and positions?
        worldBuilder.AssignRuntimeTargets(); // reassign targets (maybe)
        //influenceMap?.Reset(); //TODO: resets ai state
        // reset variables
    }
    #endregion

    #region Simulation State Management
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
            bool needsRebuild = !worldBuilt || grid.IsDirty;

            if (needsRebuild)
            {

                BuildWorld();
            }
        }

        isStarted = true;
        Time.timeScale = 1f;
        //isPaused = false;
    }

    // Freeze time, DO NOT rebuild/ destroy
    private void PauseSimulation()
    {
        Debug.Log("Pause Simulation.");

        Time.timeScale = 0f; // do nothing to editor visuals
        //isPaused = true;
    }

    // EDITOR RESET: Destroy runtime, Clear grid/ editor visuals, Return to Editor Mode
    private void ResetSimulation()
    {
        Debug.Log("Reset Simulation.");

        Time.timeScale = 1f;

        // destroy runtime objects completely
        worldBuilder.ClearRuntimeObjects();

        // destroy editor visuals completely
        scenarioEditor.ClearEditorVisuals();
        scenarioEditor.ResetEditorState();

        // clear grid data
        grid.ClearGrid();
        grid.ClearDirty();

        // reset state flags
        isStarted = false;
        worldBuilt = false;
        //isPaused = false;

        if (editorRoot != null) // this is what enables editing
        {
            editorRoot.SetActive(true);
        }
        if (runtimeRoot != null)
        {
            runtimeRoot.gameObject.SetActive(false);
        }
    }
    #endregion

    // decide scnario
    private void InitialiseScenario()
    {
        Debug.Log($"[INIT SCENARIO CALLED] frame={Time.frameCount}");
        switch (simulationMode)
        {
            case SimulationMode.Training:
                grid = scenarioSystem.Generate(
                    scenarioType,
                    mapFile,
                    Config
                );
                break;

            case SimulationMode.Testing:
                grid = scenarioSystem.Generate(
                    scenarioType,
                    mapFile,
                    Config
                );
                break;
        }

        if (grid == null)
        {
            Debug.LogError("Scenario generation failed!");
        }

    }


}


