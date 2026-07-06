using UnityEngine;
using UnityEngine.UI;
// this file mainly detects button clicks, publishes event through GameEvents, and TestingScenarioEditor or SimulationManager reacts
// translates buttons into game requests

public class UIManager : MonoBehaviour
{
    public Button playButton;
    public Button pauseButton;
    public Button resetButton;
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
        AddButtonListener(resetButton, OnResetClicked, nameof(resetButton));
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
        RemoveButtonListener(resetButton, OnResetClicked);
        RemoveButtonListener(spawnSeekerButton, OnSpawnSeekerClicked);
        RemoveButtonListener(spawnHiderButton, OnSpawnHiderClicked);
        RemoveButtonListener(spawnObstacleButton, OnSpawnObstacleClicked);
        RemoveButtonListener(eraseButton, OnEraseClicked);
    }
    private void OnPlayClicked()
    {
        GameEvents.RequestPlay();
        Debug.Log("Play Button clicked");
    }  
    private void OnPauseClicked()
    {
        GameEvents.RequestPause();
        Debug.Log("Pause Button clicked");
    }
    private void OnResetClicked()
    {
        GameEvents.RequestReset();
        Debug.Log("Reset Button clicked");
    }
    private void OnSpawnSeekerClicked()
    {
        GameEvents.RequestSpawnSeeker();
        Debug.Log("Spawn Seeker Button clicked");
    }
    private void OnSpawnHiderClicked()
    {
        GameEvents.RequestSpawnHider();
        Debug.Log("Spawn Hider Button clicked");
    }
    private void OnSpawnObstacleClicked()
    {
        GameEvents.RequestSpawnObstacle();
        Debug.Log("Spawn Obstacle Button clicked");
    }

    private void OnEraseClicked()
    {
        GameEvents.RequestErase();
        Debug.Log("Erase Button clicked");
    }

    private void AddButtonListener(Button button, UnityEngine.Events.UnityAction action, string fieldName)
    {
        if (button == null)
        {
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
