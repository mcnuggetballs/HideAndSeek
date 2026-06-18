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
    public static event Action PlayRequested;
    public static event Action PauseRequested;
    public static event Action ResetRequested;
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
    public static void RequestPlay()
    {
        PlayRequested?.Invoke();
    }
    public static void RequestPause()
    {
        PauseRequested?.Invoke();
    }
    public static void RequestReset()
    {
        ResetRequested?.Invoke();
    }

    public static void NotifyAgentSpawned(GameObject agent)
    {
        AgentSpawned?.Invoke(agent);
    }
}
