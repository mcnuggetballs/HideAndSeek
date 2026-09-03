using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using static InfluenceMap;
using static ScenarioSystem;

// true orchestrator: 1 environment = 1 simulation controller

// Listens to simulation-level events (play pause reset) and controls the active runtime agents (enable disable) and track simulation state
// Builds runtime scene from truth when "play" clicked

// reads scenariogrid data when needed

// should orchestrate updates
public class SimulationController : MonoBehaviour
{
    // mode control
    public enum SimulationMode
    {
        Testing, // use configured positions
        Training // randomised scenarios
    }

    [Header("Prefabs")]
    [SerializeField] private GameObject seekerPrefab;
    [SerializeField] private GameObject hiderPrefab;
    [SerializeField] private GameObject obstaclePrefab;

    [Header("Simulation Configuration")]
    [SerializeField] private SimulationMode simulationMode; // testing or training?
    [SerializeField] private ScenarioType scenarioType; // if training, fixed or random?
    [SerializeField] private TextAsset mapFile; // for fixed
    
    [Header("Scene Roots")]
    [SerializeField] private Transform runtimeRoot; // where runtime objects live
    [SerializeField] private GameObject editorRoot; // where visual objects live
    
    [Header("Core Systems")]
    [SerializeField] private ScenarioGrid grid;
    [SerializeField] private RuntimeNavMeshBuilder runtimeNavMeshBuilder;
    [SerializeField] private ScenarioSystem scenarioSystem;
    [SerializeField] private WorldBuilder worldBuilder;

    [Header("Editor")]
    [SerializeField] private EditorController scenarioEditor; // only required for testing

    [Header("Analysis")]
    [SerializeField] private InfluenceMap influenceMap; // analysis layer
    [SerializeField] private GridRenderer gridRenderer; // analysis layer
    [SerializeField] private LayerTag debugLayer;


    // runtime state
    //private bool isPaused = false;
    private bool isStarted = false;
    private bool worldBuilt = false;

    // runtime data
    private List<SeekerAgent> seekerAgents = new();
    private List<NavMeshAgent> hiderAgents = new();
    private Dictionary<Vector2Int, GameObject> runtimeMap = new(); // spatial look up for grid rebugging
    private readonly List<Transform> seekerBuffer = new();

    public bool IsStarted => isStarted;
    public bool IsTrainingMode => simulationMode == SimulationMode.Training;

    #region Simulation Lifecycle
    // decide simulationMode based on scene name (temporary, to be fixed with proper GameManager later)
    private void Awake()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        simulationMode = sceneName.Contains("Training")? 
            SimulationMode.Training : 
            SimulationMode.Testing;
    }

    private void Start()
    {
        isStarted = true;

        // only auto-run simulation in Training Mode
        if (simulationMode == SimulationMode.Training)
        {
            if (!worldBuilt)
            {

                InitialiseScenario(); // 1. grid ready

                worldBuilder.BuildGeometry(grid,obstaclePrefab, runtimeRoot);

                Physics.SyncTransforms(); // 3. must sync transforms before navmesh
                runtimeNavMeshBuilder.RebuildNavMesh(); // 4. bake navmesh after world exists

                worldBuilder.BuildAgents(seekerPrefab, hiderPrefab); // targets assigned

                seekerAgents = worldBuilder.GetSeekers();
                hiderAgents = worldBuilder.GetHiders();

                if(influenceMap != null)
                {
                    influenceMap.Initialise(grid); // 5. init ai perception systems
                }

                worldBuilt = true;
            }
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
        if (influenceMap.TryGetLayer(debugLayer, out var data))
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

    // For episode reset, triggered by UI button (Reset)
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
            if (!isStarted || grid.IsDirty()) // so that new seekers are reflected
            {

                worldBuilder.BuildGeometry(
                grid,
                obstaclePrefab,
                runtimeRoot);

                worldBuilt = true;



                if (influenceMap != null)
                {
                    influenceMap.Initialise(grid);
                }
                isStarted = true; // simulation has now been build at least once
                grid.ClearDirty(); // changes consumed, no longer dirty
                // change is consumed here?
            }
        }

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

    private void InitialiseScenario()
    {
        grid = scenarioSystem.Generate(scenarioType, mapFile);
        gridRenderer.BuildVisualGrid(grid);
    }
    

}


