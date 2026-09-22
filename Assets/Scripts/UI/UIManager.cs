using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// this file mainly detects button clicks, publishes event through GameEvents, and TestingScenarioEditor or SimulationManager reacts
// translates buttons into game requests

public class UIManager : MonoBehaviour
{
    public Button playButton;
    public Button pauseButton;
    [FormerlySerializedAs("resetButton")]
    public Button stopButton;
    public Button restartEpisodeButton;
    public Button clearScenarioButton;
    public Button loadScenarioButton;
    public Button spawnSeekerButton;
    public Button spawnHiderButton;
    public Button spawnObstacleButton;
    public Button eraseButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // runs when UI manager is active
    private void OnEnable()
    {
        AddButtonListener(playButton, OnPlayClicked, nameof(playButton));
        AddButtonListener(pauseButton, OnPauseClicked, nameof(pauseButton));
        AddButtonListener(stopButton, OnStopClicked, nameof(stopButton));
        AddButtonListener(restartEpisodeButton, OnRestartEpisodeClicked,
            nameof(restartEpisodeButton), false);
        AddButtonListener(clearScenarioButton, OnClearScenarioClicked,
            nameof(clearScenarioButton), false);
        AddButtonListener(loadScenarioButton, OnLoadScenarioClicked,
            nameof(loadScenarioButton), false);
        AddButtonListener(spawnSeekerButton, OnSpawnSeekerClicked, nameof(spawnSeekerButton));
        AddButtonListener(spawnHiderButton, OnSpawnHiderClicked, nameof(spawnHiderButton));
        AddButtonListener(spawnObstacleButton, OnSpawnObstacleClicked, nameof(spawnObstacleButton));
        AddButtonListener(eraseButton, OnEraseClicked, nameof(eraseButton));

    }

    //unsubscribe
    public void OnDisable()
    {
        RemoveButtonListener(playButton, OnPlayClicked);
        RemoveButtonListener(pauseButton, OnPauseClicked);
        RemoveButtonListener(stopButton, OnStopClicked);
        RemoveButtonListener(restartEpisodeButton, OnRestartEpisodeClicked);
        RemoveButtonListener(clearScenarioButton, OnClearScenarioClicked);
        RemoveButtonListener(loadScenarioButton, OnLoadScenarioClicked);
        RemoveButtonListener(spawnSeekerButton, OnSpawnSeekerClicked);
        RemoveButtonListener(spawnHiderButton, OnSpawnHiderClicked);
        RemoveButtonListener(spawnObstacleButton, OnSpawnObstacleClicked);
        RemoveButtonListener(eraseButton, OnEraseClicked);
    }
    private void OnPlayClicked()
    {
        GameEvents.RequestPlay();
    }  
    private void OnPauseClicked()
    {
        GameEvents.RequestPause();
    }
    private void OnStopClicked()
    {
        GameEvents.RequestStopSimulation();
    }
    private void OnRestartEpisodeClicked()
    {
        GameEvents.RequestRestartEpisode();
    }
    private void OnClearScenarioClicked()
    {
        GameEvents.RequestClearScenario();
    }
    private void OnLoadScenarioClicked()
    {
        GameEvents.RequestLoadScenario();
    }
    private void OnSpawnSeekerClicked()
    {
        GameEvents.RequestSpawnSeeker();
    }
    private void OnSpawnHiderClicked()
    {
        GameEvents.RequestSpawnHider();
    }
    private void OnSpawnObstacleClicked()
    {
        GameEvents.RequestSpawnObstacle();
    }

    private void OnEraseClicked()
    {
        GameEvents.RequestErase();
    }

    private void AddButtonListener(Button button, UnityEngine.Events.UnityAction action,
        string fieldName, bool required = true)
    {
        if (button == null)
        {
            if (required)
                Debug.LogWarning($"UIManager is missing a reference for {fieldName}.");
            return;
        }

        button.onClick.AddListener(action);
    }

    private void RemoveButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
