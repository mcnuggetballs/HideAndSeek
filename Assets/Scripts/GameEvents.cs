                            using System;
using UnityEngine;

// UI Manager will tell GameEvents that this button was clicked, GameEvents broadcast to the whole system
// UI manager says "Play was requested"
// GameEvents announces Playrequested
// SimulationManager hears it and runs PlaySimulation

public static class GameEvents 
{
    // use case : GameEvents.PlayRequested += PlaySimulation;
    public static event Action SpawnSeekerRequested;
    public static event Action SpawnHiderRequested;
    public static event Action SpawnWallRequested;
    public static event Action EraseRequested;
    public static event Action PlayRequested;
    public static event Action PauseRequested;
    public static event Action RestartEpisodeRequested;
    public static event Action StopSimulationRequested;
    public static event Action ClearScenarioRequested;
    public static event Action LoadScenarioRequested;
    public static event Action<GameObject> AgentSpawned;

    public static void RequestSpawnSeeker()
    {
        Debug.Log("GameEvents: Spawn seeker requested.");
        SpawnSeekerRequested?.Invoke(); // Run all methods subscribed to this event
    }
    public static void RequestSpawnHider()
    {
        SpawnHiderRequested?.Invoke();
    }
    public static void RequestSpawnObstacle()
    {
        SpawnWallRequested?.Invoke();
    }
    public static void RequestErase()
    {
        EraseRequested?.Invoke();
    }
    public static void RequestPlay()
    {
        PlayRequested?.Invoke();
    }
    public static void RequestPause()
    {
        PauseRequested?.Invoke();
    }
    public static void RequestRestartEpisode()
    {
        RestartEpisodeRequested?.Invoke();
    }
    public static void RequestStopSimulation()
    {
        StopSimulationRequested?.Invoke();
    }
    public static void RequestClearScenario()
    {
        ClearScenarioRequested?.Invoke();
    }
    public static void RequestLoadScenario()
    {
        LoadScenarioRequested?.Invoke();
    }
    public static void NotifyAgentSpawned(GameObject agent)
    {
        AgentSpawned?.Invoke(agent);
    }
}
