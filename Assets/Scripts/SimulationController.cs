using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using static InfluenceMap;
using static ScenarioSystem;

// Scene-level controls and scenario selection. EnvironmentManager builds each runtime world.
public class SimulationController : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject trainingEnvironmentPrefab;
    [SerializeField] private GameObject seekerPrefab;
    [SerializeField] private GameObject hiderPrefab;
    [SerializeField] private GameObject obstaclePrefab;

    [Header("Scenario")]
    [SerializeField] private ScenarioSystem scenarioSystem;
    public enum SimulationMode { Testing, Training }
    [SerializeField] private SimulationMode simulationMode;
    [SerializeField] private ScenarioType scenarioType;
    [SerializeField] private TextAsset mapFile;

    [Header("Scene Roots")]
    [SerializeField] private GameObject editorRoot;
    [SerializeField] private EditorController scenarioEditor;

    [Header("Analysis")]
    [SerializeField] private LayerTag debugLayer;

    [Header("World Configuration")]
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private float cellSize;

    public struct WorldConfig
    {
        public int width;
        public int height;
        public float cellSize;
        public Vector3 origin;
    }

    public WorldConfig Config => new WorldConfig
    {
        width = width,
        height = height,
        cellSize = cellSize,
        origin = transform.position
    };

    private EnvironmentManager environmentManager;
    private EnvironmentInstance environment;
    private readonly List<Transform> seekerBuffer = new();
    private bool isStarted;

    public bool IsStarted => isStarted;
    public bool IsTrainingMode => simulationMode == SimulationMode.Training;
    public EnvironmentInstance Environment => environment;

    private void Awake()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        simulationMode = sceneName.Contains("Training") ? SimulationMode.Training : SimulationMode.Testing;
    }

    private void Start()
    {
        if (scenarioSystem == null || seekerPrefab == null || hiderPrefab == null || obstaclePrefab == null ||
            (IsTrainingMode && trainingEnvironmentPrefab == null))
        {
            Debug.LogError("SimulationController is missing scenario or prefab references.", this);
            enabled = false;
            return;
        }

        ScenarioGrid grid = scenarioSystem.Generate(scenarioType, mapFile, Config);
        if (grid == null || (IsTrainingMode && !ScenarioSystem.HasBothTeams(grid)))
        {
            Debug.LogError("Scenario generation failed or the training scenario lacks a team.", this);
            enabled = false;
            return;
        }

        environmentManager = GetComponent<EnvironmentManager>() ?? gameObject.AddComponent<EnvironmentManager>();
        environmentManager.Configure(seekerPrefab, hiderPrefab, obstaclePrefab);
        try
        {
            environment = IsTrainingMode
                ? environmentManager.CreateEnvironment(grid, trainingEnvironmentPrefab)
                : environmentManager.RegisterEnvironment(grid, gameObject);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not create environment: {exception.Message}", this);
            enabled = false;
            return;
        }

        FitFloorToGrid();
        Camera.main?.GetComponent<TopDownCameraController>()?.FrameGrid(grid);

        if (IsTrainingMode)
        {
            BuildWorld();
            isStarted = environment.IsBuilt;
        }
        else
        {
            scenarioEditor?.Initialise(grid);
            scenarioEditor?.RebuildVisualsFromGrid();
            environment.RuntimeRoot.gameObject.SetActive(false);
        }

        if (environment.GridRenderer != null)
        {
            environment.GridRenderer.Initialise(grid);
            environment.GridRenderer.BuildVisualGrid();
        }
    }

    private void BuildWorld()
    {
        try { environmentManager.BuildEnvironment(environment); }
        catch (Exception exception)
        {
            Debug.LogError($"Could not build environment: {exception.Message}", this);
            enabled = false;
        }
    }

    // Both prefabs use a Unity plane, ten units wide at scale one.
    private void FitFloorToGrid()
    {
        Transform floor = environment.Root.transform.Find("Plane");
        if (floor == null)
        {
            Debug.LogWarning("Environment has no Plane child to size to the scenario.", this);
            return;
        }

        ScenarioGrid grid = environment.Grid;
        floor.localRotation = Quaternion.identity;
        floor.localScale = new Vector3(grid.Width * grid.CellSize / 10f, floor.localScale.y,
            grid.Height * grid.CellSize / 10f);

        Transform placementArea = environment.Root.transform.Find("Placement Area");
        if (placementArea != null && placementArea.TryGetComponent(out BoxCollider placementCollider))
            placementCollider.size = new Vector3(grid.Width * grid.CellSize, placementCollider.size.y,
                grid.Height * grid.CellSize);
    }

    public void SavePaintedScenario()
    {
        if (IsTrainingMode || isStarted || environment == null) return;
        try
        {
            string path = scenarioSystem.SavePainted(environment.Grid);
            Debug.Log($"Saved scenario {environment.Grid.Width}x{environment.Grid.Height} to {path}", this);
        }
        catch (Exception exception) when (exception is ArgumentException || exception is IOException ||
                                          exception is UnauthorizedAccessException)
        {
            Debug.LogError($"Could not save scenario: {exception.Message}", this);
        }
    }

    public void LoadPaintedScenario()
    {
        if (IsTrainingMode || isStarted || environment == null) return;
        try
        {
            ScenarioGrid loaded = scenarioSystem.LoadPainted(environment.Grid.CellSize, environment.Grid.Origin);
            environmentManager.ClearRuntime(environment);
            environment.RuntimeRoot.gameObject.SetActive(false);
            environment.SetGrid(loaded);
            loaded.MarkDirty();
            FitFloorToGrid();
            Camera.main?.GetComponent<TopDownCameraController>()?.FrameGrid(loaded);
            scenarioEditor?.Initialise(loaded);
            scenarioEditor?.RebuildVisualsFromGrid();
            if (environment.GridRenderer != null)
            {
                environment.GridRenderer.Initialise(loaded);
                environment.GridRenderer.BuildVisualGrid();
            }
            Debug.Log($"Loaded scenario {loaded.Width}x{loaded.Height} from {ScenarioStorage.GetPath(scenarioSystem.SavedScenarioName)}", this);
        }
        catch (Exception exception) when (exception is ArgumentException || exception is FormatException ||
                                          exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogError($"Could not load scenario: {exception.Message}", this);
        }
    }

    private void Update()
    {
        if (environmentManager == null) return;
        foreach (EnvironmentInstance instance in environmentManager.Environments)
        {
            if (!instance.IsBuilt || instance.InfluenceMap == null) continue;
            instance.WorldBuilder.GetSeekerTransforms(seekerBuffer);
            instance.InfluenceMap.UpdateAgentPositions(seekerBuffer);
            if (instance.GridRenderer != null && instance.InfluenceMap.TryGetLayer(debugLayer, out var data))
                instance.GridRenderer.Render(data, debugLayer);
        }
    }

    private void OnEnable()
    {
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
        if (environment == null) return;
        environment.RuntimeRoot.gameObject.SetActive(true);
        if (!IsTrainingMode && editorRoot != null) editorRoot.SetActive(false);
        if (!environment.IsBuilt || environment.Grid.IsDirty) BuildWorld();
        if (!environment.IsBuilt) return;
        isStarted = true;
        Time.timeScale = 1f;
    }

    private void PauseSimulation() => Time.timeScale = 0f;

    private void ResetSimulation()
    {
        if (environment == null) return;
        Time.timeScale = 1f;
        environmentManager.ClearRuntime(environment);
        scenarioEditor?.ClearEditorVisuals();
        scenarioEditor?.ResetEditorState();
        environment.Grid.ClearGrid();
        environment.Grid.ClearDirty();
        isStarted = false;
        if (editorRoot != null) editorRoot.SetActive(true);
        environment.RuntimeRoot.gameObject.SetActive(false);
    }
}
