using System;
using System.Collections.Generic;
using System.IO;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.SceneManagement;
using static InfluenceMap;
using static ScenarioSystem;

// Scene-level controls and scenario selection. EnvironmentManager builds each runtime world.

// selects mode/scenario,
// initialises environments, frames camera,
// handles play/pause/reset and persistence
// updates influence and rendering

[DefaultExecutionOrder(-20)]
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

    [Header("Team Episode Rules")]
    [SerializeField] private EpisodeRules episodeRules = new();

    [Header("Training Curriculum")]
    [SerializeField] private bool useAdaptiveSpawnCurriculum = true;
    [SerializeField, Min(1)] private int minimumHiderDistance = 2;
    [SerializeField, Min(1)] private int startingMaximumHiderDistance = 4;
    [SerializeField, Min(1)] private int curriculumDistanceIncrement = 1;
    [SerializeField, Min(1)] private int curriculumWindowSize = 20;
    [SerializeField, Range(0f, 1f)] private float curriculumSuccessThreshold = 0.8f;
    [SerializeField] private int curriculumRandomSeed = 12345;
    [SerializeField] private string curriculumProgressFile = "curriculum_progress_v2.json";
    [Tooltip("Resume a saved lesson only when continuing its matching policy. Disable for a fresh policy.")]
    [SerializeField] private bool resumeCurriculumProgress;
    [Tooltip("Face targets during the initial lesson; later lessons retain random headings.")]
    [SerializeField] private bool faceTargetsInFirstLesson;
    [Tooltip("Maximum physics steps for a successful episode to count toward advancement. Zero disables this limit.")]
    [SerializeField, Min(0)] private int curriculumMaximumCaptureSteps;
    [Header("Ordered Police Curriculum")]
    [Tooltip("Use Capture, Chase, Detect, Search phases instead of distance-only spawning.")]
    [SerializeField] private bool useOrderedPoliceCurriculum;
    [SerializeField, Min(1)] private int orderedWindowSize = 100;
    [SerializeField, Range(0f, 1f)] private float orderedSuccessThreshold = 0.8f;

    [Header("Cortex Police Curriculum")]
    [Tooltip("On a two-seeker, two-hider map, teach Capture, Chase, Detect, then progressively farther wall-hidden Search.")]
    [SerializeField] private bool useCortexPoliceCurriculum;
    [Tooltip("Training maps in Resources/PoliceMaps/Training are rotated across arenas. Cortex1 is always included.")]
    [SerializeField] private bool useCortexMapPool = true;
    [SerializeField, Min(1)] private int cortexWindowSize = 100;
    [SerializeField, Range(0f, 1f)] private float cortexSuccessThreshold = 0.8f;
    [SerializeField, Min(3)] private int cortexFinalSearchDistance = 12;
    [SerializeField, Min(1)] private int cortexSearchDistanceIncrement = 1;
    [SerializeField, Min(1)] private int cortexCaptureDeadline = 512;
    [SerializeField, Min(1)] private int cortexChaseDeadline = 768;
    [SerializeField, Min(1)] private int cortexDetectDeadline = 1024;
    [Header("Cortex Baseline Evaluation")]
    [Tooltip("Hold one Cortex lesson fixed for a repeatable inference evaluation. " +
        "Use an exported model in Inference Only mode.")]
    [SerializeField] private bool runCortexBaseline;
    private enum PoliceBaselineCase
    {
        Custom, OpenPursuit, ObstacleSearch, CortexSearch, HeldoutSearch
    }
    [SerializeField] private PoliceBaselineCase baselineCase;
    [Tooltip("Fixed evaluation layout: Cortex1 or a TextAsset name in Resources/PoliceMaps/Training or Evaluation.")]
    [SerializeField] private string baselineMapId = "Cortex1";
    [SerializeField] private CortexPoliceCurriculum.Stage baselineStage =
        CortexPoliceCurriculum.Stage.Capture;
    [SerializeField, Min(3)] private int baselineSearchDistance = 3;
    [SerializeField, Min(1)] private int baselineCompletedEpisodes = 100;
    [SerializeField] private string baselineModelLabel = "checkpoint-name";
    [SerializeField] private bool showCortexTrainingDashboard = true;

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
        if (runCortexBaseline && useCortexPoliceCurriculum)
            Time.timeScale = 1f;
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

        TextAsset[] trainingMaps = Array.Empty<TextAsset>();
        TextAsset selectedMap = mapFile;
        CortexPoliceCurriculum.Stage evaluationStage = baselineStage;
        int evaluationSearchDistance = baselineSearchDistance;
        string evaluationMapId = baselineMapId;
        if (runCortexBaseline)
        {
            switch (baselineCase)
            {
                case PoliceBaselineCase.OpenPursuit:
                    evaluationMapId = "EvalOpen";
                    evaluationStage = CortexPoliceCurriculum.Stage.Chase;
                    break;
                case PoliceBaselineCase.ObstacleSearch:
                    evaluationMapId = "EvalObstacles";
                    evaluationStage = CortexPoliceCurriculum.Stage.Search;
                    evaluationSearchDistance = 10;
                    break;
                case PoliceBaselineCase.CortexSearch:
                    evaluationMapId = "Cortex1";
                    evaluationStage = CortexPoliceCurriculum.Stage.Search;
                    evaluationSearchDistance = 10;
                    break;
                case PoliceBaselineCase.HeldoutSearch:
                    evaluationMapId = "HeldoutOffsetRooms";
                    evaluationStage = CortexPoliceCurriculum.Stage.Search;
                    evaluationSearchDistance = 10;
                    break;
            }
        }
        if (IsTrainingMode && useCortexPoliceCurriculum && scenarioType == ScenarioType.Fixed)
        {
            trainingMaps = Resources.LoadAll<TextAsset>("PoliceMaps/Training");
            Array.Sort(trainingMaps, (a, b) => string.CompareOrdinal(a.name, b.name));
            if (runCortexBaseline)
            {
                if (!string.IsNullOrWhiteSpace(evaluationMapId) &&
                    !string.Equals(evaluationMapId, mapFile != null ? mapFile.name : "", StringComparison.Ordinal))
                {
                    selectedMap = FindPoliceMap(evaluationMapId, trainingMaps);
                    if (selectedMap == null)
                    {
                        Debug.LogError($"Unknown baseline map '{evaluationMapId}'.", this);
                        enabled = false;
                        return;
                    }
                }
            }
            else if (useCortexMapPool && trainingMaps.Length == 0)
                Debug.LogWarning("No PoliceMaps/Training assets found; using Cortex1 only.", this);
        }

        ScenarioGrid grid = scenarioSystem.Generate(scenarioType, selectedMap, Config);
        if (grid != null) grid.LayoutId = selectedMap != null ? selectedMap.name : scenarioType.ToString();
        if (grid == null || (IsTrainingMode && !ScenarioSystem.HasBothTeams(grid)))
        {
            Debug.LogError("Scenario generation failed or the training scenario lacks a team.", this);
            enabled = false;
            return;
        }
        if (IsTrainingMode && useCortexPoliceCurriculum && useCortexMapPool &&
            !runCortexBaseline && trainingMaps.Length > 0 &&
            (!useSpawner || spawner.environmentCount < trainingMaps.Length + 1))
        {
            Debug.LogError("Cortex map pool needs at least one arena per layout.", this);
            enabled = false;
            return;
        }

        environmentManager = GetComponent<EnvironmentManager>() ?? gameObject.AddComponent<EnvironmentManager>();
        environmentManager.Configure(seekerPrefab, hiderPrefab, obstaclePrefab);
        ITrainingCurriculum curriculum = null;
        if (IsTrainingMode && (useCortexPoliceCurriculum ||
            useOrderedPoliceCurriculum || useAdaptiveSpawnCurriculum))
        {
            string progressPath = Path.Combine(Application.dataPath, "..", curriculumProgressFile);
            if (!resumeCurriculumProgress)
            {
                string directory = Path.GetDirectoryName(progressPath);
                string stem = Path.GetFileNameWithoutExtension(progressPath);
                progressPath = Path.Combine(directory,
                    $"{stem}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.json");
            }
            Debug.Log($"[Curriculum] {(resumeCurriculumProgress ? "Resuming" : "Fresh")} progress: {progressPath}", this);
            int minimumDistance = Mathf.Max(1, minimumHiderDistance);
            var requiredMaps = new List<string>();
            if (useCortexPoliceCurriculum)
            {
                requiredMaps.Add(selectedMap != null ? selectedMap.name : scenarioType.ToString());
                if (useCortexMapPool && !runCortexBaseline &&
                    scenarioType == ScenarioType.Fixed)
                    foreach (TextAsset layout in trainingMaps)
                        if (!requiredMaps.Contains(layout.name)) requiredMaps.Add(layout.name);
            }
            curriculum = useCortexPoliceCurriculum
                ? new CortexPoliceCurriculum(cortexWindowSize, cortexSuccessThreshold,
                    cortexFinalSearchDistance, cortexSearchDistanceIncrement,
                    new[] { cortexCaptureDeadline, cortexChaseDeadline,
                        cortexDetectDeadline, episodeRules.maxEnvironmentSteps },
                    progressPath, curriculumRandomSeed, resumeCurriculumProgress,
                    runCortexBaseline, evaluationStage, evaluationSearchDistance,
                    requiredMaps.ToArray())
                : useOrderedPoliceCurriculum
                    ? new OrderedPoliceCurriculum(orderedWindowSize, orderedSuccessThreshold,
                        progressPath, curriculumRandomSeed, resumeCurriculumProgress)
                    : new AdaptiveSpawnCurriculum(
                    minimumDistance,
                    Mathf.Max(minimumDistance, startingMaximumHiderDistance),
                    curriculumDistanceIncrement,
                    curriculumWindowSize,
                    curriculumSuccessThreshold,
                    progressPath,
                    curriculumRandomSeed,
                    resumeCurriculumProgress,
                    faceTargetsInFirstLesson,
                    curriculumMaximumCaptureSteps);
        }
        environmentManager.ConfigureTraining(episodeRules ?? new EpisodeRules(), curriculum);
        environmentManager.LayoutChanged -= HandleLayoutChanged;
        environmentManager.LayoutChanged += HandleLayoutChanged;
        try
        {
            if (useSpawner)
            {
                IReadOnlyList<EnvironmentInstance> batch = spawner.SpawnEnvironments(
                    environmentManager, trainingEnvironmentPrefab, grid, (index, origin) =>
                    {
                        WorldConfig config = Config;
                        config.origin = origin;
                        TextAsset layout = selectedMap;
                        if (!runCortexBaseline && useCortexPoliceCurriculum &&
                            useCortexMapPool && scenarioType == ScenarioType.Fixed &&
                            trainingMaps.Length > 0)
                            layout = index % (trainingMaps.Length + 1) == 0
                                ? mapFile : trainingMaps[(index - 1) % (trainingMaps.Length + 1)];
                        ScenarioGrid generated = scenarioSystem.Generate(scenarioType, layout, config);
                        if (generated != null)
                            generated.LayoutId = layout != null ? layout.name : scenarioType.ToString();
                        return generated;
                    });
                environment = batch[0]; // Primary environment for camera focus and existing callers.
                if (useCortexPoliceCurriculum)
                {
                    var layoutCounts = new Dictionary<string, int>();
                    foreach (EnvironmentInstance instance in batch)
                    {
                        string id = instance.Grid.LayoutId;
                        layoutCounts.TryGetValue(id, out int count);
                        layoutCounts[id] = count + 1;
                    }
                    foreach (var entry in layoutCounts)
                        Debug.Log($"[Police Maps] {entry.Key}: {entry.Value} arenas", this);
                }
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
            if (runCortexBaseline && curriculum is CortexPoliceCurriculum)
            {
                bool validInferenceSetup = true;
                foreach (EnvironmentInstance instance in environmentManager.Environments)
                {
                    foreach (SeekerAgent seeker in instance.Seekers)
                    {
                        BehaviorParameters behavior = seeker.GetComponent<BehaviorParameters>();
                        if (behavior != null && behavior.BehaviorType == BehaviorType.InferenceOnly &&
                            behavior.Model != null) continue;
                        validInferenceSetup = false;
                        break;
                    }
                    if (!validInferenceSetup) break;
                }
                if (!validInferenceSetup)
                {
                    Debug.LogError("Cortex baseline requires an exported model and " +
                        "Inference Only on every seeker. Simulation paused.", this);
                    Time.timeScale = 0f;
                }
            }
            else if (curriculum is CortexPoliceCurriculum &&
                Academy.Instance.IsCommunicatorOn)
            {
                foreach (EnvironmentInstance instance in environmentManager.Environments)
                    foreach (SeekerAgent seeker in instance.Seekers)
                    {
                        BehaviorParameters behavior = seeker.GetComponent<BehaviorParameters>();
                        if (behavior != null && behavior.BehaviorType == BehaviorType.Default)
                            continue;
                        Debug.LogError("Cortex training requires Behavior Type Default " +
                            "on every seeker when the Python trainer is connected. " +
                            "Simulation paused.", this);
                        Time.timeScale = 0f;
                        break;
                    }
            }
            if (showCortexTrainingDashboard && curriculum is CortexPoliceCurriculum cortex)
            {
                TrainingBaselineDashboard dashboard =
                    GetComponent<TrainingBaselineDashboard>() ??
                    gameObject.AddComponent<TrainingBaselineDashboard>();
                dashboard.Configure(cortex, environmentManager.Environments,
                    runCortexBaseline, baselineCompletedEpisodes, baselineModelLabel);
            }
        }
        else
        {
            HandleLayoutChanged(environment);
            environment.RuntimeRoot.gameObject.SetActive(false);
        }
    }

    private static TextAsset FindPoliceMap(string name, TextAsset[] trainingMaps)
    {
        foreach (TextAsset candidate in trainingMaps)
            if (string.Equals(candidate.name, name, StringComparison.Ordinal)) return candidate;
        foreach (TextAsset candidate in Resources.LoadAll<TextAsset>("PoliceMaps/Evaluation"))
            if (string.Equals(candidate.name, name, StringComparison.Ordinal)) return candidate;
        return null;
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

    // The testing scene's existing Load button opens a file picker. F7 still reloads
    // the named saved scenario through LoadPaintedScenario.
    public void ImportScenario()
    {
        if (IsTrainingMode || environment == null) return;

        float previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        try
        {
            string path = ScenarioFilePicker.Open(Application.dataPath);
            if (string.IsNullOrEmpty(path)) return; // Cancel keeps the current world.
            LoadScenarioFromFile(path);
        }
        catch (Exception exception) when (exception is IOException ||
                                          exception is NotSupportedException)
        {
            Debug.LogError($"Could not select a scenario: {exception.Message}", this);
        }
        finally
        {
            Time.timeScale = previousTimeScale;
        }
    }

    public bool LoadScenarioFromFile(string path)
    {
        if (IsTrainingMode || environment == null) return false;
        try
        {
            // Parse completely before stopping or replacing the existing scenario.
            ScenarioGrid loaded = ScenarioStorage.LoadFile(
                path, environment.Grid.CellSize, environment.Grid.Origin);
            environmentManager.ReplaceLayout(environment, loaded);
            isStarted = false;
            EnterEditingMode(false);
            Debug.Log($"Imported {loaded.Width}x{loaded.Height} scenario from {path}. " +
                      "Review the layout, then press Play.", this);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException ||
                                          exception is FormatException || exception is IOException ||
                                          exception is UnauthorizedAccessException)
        {
            Debug.LogError($"Could not import scenario: {exception.Message}", this);
            return false;
        }
    }

    private void Update()
    {
        if (environmentManager == null) return;
        foreach (EnvironmentInstance instance in environmentManager.Environments)
        {
            if (!instance.IsBuilt || instance.InfluenceMap == null) continue;
            if (instance.GridRenderer != null &&
                instance.InfluenceMap.TryGetLayer(debugLayer, out var data))
            {
                instance.GridRenderer.Render(data, debugLayer);
            }
        }
    }

    private void FixedUpdate()
    {
        if (environmentManager == null || Time.timeScale <= 0f) return;
        foreach (EnvironmentInstance instance in environmentManager.Environments)
            if (instance.IsBuilt)
                instance.EpisodeCoordinator.Step();
    }

    private void OnEnable()
    {
        GameEvents.PlayRequested += PlaySimulation;
        GameEvents.PauseRequested += PauseSimulation;
        GameEvents.RestartEpisodeRequested += RestartEpisode;
        GameEvents.StopSimulationRequested += StopAndReturnToEditing;
        GameEvents.ClearScenarioRequested += ClearScenario;
        GameEvents.LoadScenarioRequested += ImportScenario;
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
        GameEvents.LoadScenarioRequested -= ImportScenario;
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
