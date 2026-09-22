using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using static InfluenceMap;
using static ScenarioSystem;

// Scene-level controls and scenario selection. EnvironmentManager builds each runtime world.

// selects mode/scenario,
// initialises environments, frames camera,
// handles play/pause/reset and persistence
// updates influence and rendering

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
        EnvironmentSpawner spawner = GetComponent<EnvironmentSpawner>();
        bool useSpawner = IsTrainingMode && spawner != null && spawner.enabled;
        if (scenarioSystem == null || seekerPrefab == null || hiderPrefab == null || obstaclePrefab == null ||
            (IsTrainingMode && trainingEnvironmentPrefab == null &&
             (!useSpawner || spawner.environmentPrefab == null)))
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
        environmentManager.LayoutChanged -= HandleLayoutChanged;
        environmentManager.LayoutChanged += HandleLayoutChanged;
        try
        {
            if (useSpawner)
            {
                IReadOnlyList<EnvironmentInstance> batch = spawner.SpawnEnvironments(
                    environmentManager, trainingEnvironmentPrefab, grid, origin =>
                    {
                        WorldConfig config = Config;
                        config.origin = origin;
                        return scenarioSystem.Generate(scenarioType, mapFile, config);
                    });
                environment = batch[0]; // Primary environment for camera focus and existing callers.
            }
            else
            {
                environment = IsTrainingMode
                    ? environmentManager.CreateEnvironment(grid, trainingEnvironmentPrefab)
                    : environmentManager.RegisterEnvironment(grid, gameObject);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not create environment: {exception.Message}", this);
            enabled = false;
            return;
        }

        if (IsTrainingMode)
        {
            Camera.main?.GetComponent<TopDownCameraController>()?.FrameGrid(grid);
            if (!useSpawner) BuildWorld();
            isStarted = environment.IsBuilt;
        }
        else
        {
            HandleLayoutChanged(environment);
            environment.RuntimeRoot.gameObject.SetActive(false);
        }
    }

    private bool BuildWorld()
    {
        try
        {
            environmentManager.BuildEnvironment(environment);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not build environment: {exception.Message}", this);
            isStarted = false;

            if (environment != null)
                environment.RuntimeRoot.gameObject.SetActive(false);
            if (!IsTrainingMode)
            {
                if (editorRoot != null) editorRoot.SetActive(true);
                scenarioEditor?.ResetEditorState();
                scenarioEditor?.RebuildVisualsFromGrid();
            }

            return false;
        }
    }

    public void SavePaintedScenario()
    {
        if (IsTrainingMode || isStarted || environment == null) return;
        try
        {
            string path = scenarioSystem.SavePainted(environment.Grid);
            Debug.Log(
                $"Saved scenario revision {environment.Grid.SavedRevision} " +
                $"({environment.Grid.Width}x{environment.Grid.Height}) to {path}", this);
        }
        catch (Exception exception) when (exception is ArgumentException || exception is IOException ||
                                          exception is UnauthorizedAccessException)
        {
            Debug.LogError($"Could not save scenario: {exception.Message}", this);
        }
    }

    public void LoadPaintedScenario()
    {
        if (IsTrainingMode || environment == null) return;
        try
        {
            ScenarioGrid loaded = scenarioSystem.LoadPainted(environment.Grid.CellSize, environment.Grid.Origin);
            Time.timeScale = 1f;
            isStarted = false;
            environmentManager.ReplaceLayout(environment, loaded);
            EnterEditingMode(false);
            Debug.Log(
                $"Loaded saved revision {loaded.SavedRevision} " +
                $"({loaded.Width}x{loaded.Height}) from " +
                ScenarioStorage.GetPath(scenarioSystem.SavedScenarioName), this);
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
            instance.GetSeekerTransforms(seekerBuffer);
            instance.InfluenceMap.UpdateAgentPositions(seekerBuffer);
            if (instance.GridRenderer != null &&
                instance.InfluenceMap.TryGetLayer(debugLayer, out var data))
            {
                instance.GridRenderer.Render(data, debugLayer);
            }
        }
    }

    private void OnEnable()
    {
        GameEvents.PlayRequested += PlaySimulation;
        GameEvents.PauseRequested += PauseSimulation;
        GameEvents.RestartEpisodeRequested += RestartEpisode;
        GameEvents.StopSimulationRequested += StopAndReturnToEditing;
        GameEvents.ClearScenarioRequested += ClearScenario;
        GameEvents.LoadScenarioRequested += LoadPaintedScenario;
        if (environmentManager != null)
        {
            environmentManager.LayoutChanged -= HandleLayoutChanged;
            environmentManager.LayoutChanged += HandleLayoutChanged;
        }
    }

    private void OnDisable()
    {
        GameEvents.PlayRequested -= PlaySimulation;
        GameEvents.PauseRequested -= PauseSimulation;
        GameEvents.RestartEpisodeRequested -= RestartEpisode;
        GameEvents.StopSimulationRequested -= StopAndReturnToEditing;
        GameEvents.ClearScenarioRequested -= ClearScenario;
        GameEvents.LoadScenarioRequested -= LoadPaintedScenario;
        if (environmentManager != null)
            environmentManager.LayoutChanged -= HandleLayoutChanged;
    }

    private void PlaySimulation()
    {
        if (environment == null) return;
        if (IsTrainingMode)
        {
            try
            {
                foreach (EnvironmentInstance instance in environmentManager.Environments)
                {
                    if (instance.NeedsBuild)
                        environmentManager.BuildEnvironment(instance);
                    instance.RuntimeRoot.gameObject.SetActive(true);
                }
                isStarted = true;
                Time.timeScale = 1f;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not start training environments: {exception.Message}", this);
            }
            return;
        }

        environment.RuntimeRoot.gameObject.SetActive(true);
        if (editorRoot != null) editorRoot.SetActive(false);
        if (environment.NeedsBuild && !BuildWorld())
            return;
        if (!environment.IsBuilt) return;

        isStarted = true;
        Time.timeScale = 1f;
    }

    private void PauseSimulation() => Time.timeScale = 0f;

    public void RestartEpisode()
    {
        if (environment == null || !isStarted)
            return;

        Time.timeScale = 1f;
        if (IsTrainingMode)
        {
            foreach (EnvironmentInstance instance in environmentManager.Environments)
                if (instance.IsBuilt) environmentManager.RestartEpisode(instance);
            return;
        }

        if (environment.IsBuilt)
            environmentManager.RestartEpisode(environment);
    }

    public void StopAndReturnToEditing()
    {
        if (environment == null) return;
        Time.timeScale = 1f;

        if (IsTrainingMode)
        {
            foreach (EnvironmentInstance instance in environmentManager.Environments)
            {
                environmentManager.ClearRuntime(instance);
                instance.RuntimeRoot.gameObject.SetActive(false);
            }
            isStarted = false;
            return;
        }

        environmentManager.ClearRuntime(environment);
        isStarted = false;
        EnterEditingMode();
    }

    public void ClearScenario()
    {
        if (IsTrainingMode || environment == null)
            return;

        Time.timeScale = 1f;
        isStarted = false;
        environmentManager.ClearScenario(environment);
        EnterEditingMode(false);
    }

    private void EnterEditingMode(bool rebuildVisuals = true)
    {
        if (editorRoot != null) editorRoot.SetActive(true);
        environment.RuntimeRoot.gameObject.SetActive(false);
        scenarioEditor?.ResetEditorState();
        if (rebuildVisuals)
            scenarioEditor?.RebuildVisualsFromGrid();
    }

    private void HandleLayoutChanged(EnvironmentInstance changedEnvironment)
    {
        if (IsTrainingMode || changedEnvironment == null || changedEnvironment != environment)
            return;

        ScenarioGrid grid = changedEnvironment.Grid;
        Camera.main?.GetComponent<TopDownCameraController>()?.FrameGrid(grid);
        scenarioEditor?.Initialise(grid);
        scenarioEditor?.ResetEditorState();
        scenarioEditor?.RebuildVisualsFromGrid();
    }
}
