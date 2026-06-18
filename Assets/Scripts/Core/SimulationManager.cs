using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

// Listens to simulation-level events (play pause reset) and controls the active runtime agents (enable disable) and track simulation state

/*
    TrainingScene - set startupSpawnMode to TrainingGrid, assign simulationPrefab, spawns 16 environments.
    TestingScene - set startupSpawnMode to None when one SimulationPrefab is already placed in the scene.
 
 */

public class SimulationManager : MonoBehaviour
{
    private enum StartupSpawnMode
    {
        None,
        SingleEnvironment,
        TrainingGrid
    }

    public GameObject simulationPrefab;
    [SerializeField] private StartupSpawnMode startupSpawnMode = StartupSpawnMode.None;
    [SerializeField] private int rowCount = 4;
    public float spaceBetween = 80;
    
    // keep track of agents and environments
    private readonly List<GameObject> spawnedAgents = new List<GameObject>();
    private readonly List<GameObject> spawnedEnvironments = new List<GameObject>();

    private bool isPlaying; // false = edit/setup mode, true = simulation is running

    private void OnEnable()
    {
        // simulation maanger listens to play/pause.reset and agent spawning
        GameEvents.PlayRequested += PlaySimulation;
        GameEvents.PauseRequested += PauseSimulation;
        GameEvents.ResetRequested += ResetSimulation;
        GameEvents.AgentSpawned += RegisterSpawnedAgent;
    }

    private void OnDisable()
    {
        GameEvents.PlayRequested -= PlaySimulation;
        GameEvents.PauseRequested -= PauseSimulation;
        GameEvents.ResetRequested -= ResetSimulation;
        GameEvents.AgentSpawned -= RegisterSpawnedAgent;
    }

    private void Start()
    {
        switch (startupSpawnMode)
        {
            case StartupSpawnMode.SingleEnvironment:
                SpawnTesting(simulationPrefab);
                break;
            case StartupSpawnMode.TrainingGrid:
                SpawnTraining(simulationPrefab);
                break;
        }
    }

    private void PlaySimulation()
    {
        Debug.Log("Play Simulation.");
        isPlaying = true;

        foreach (GameObject agent in spawnedAgents)
        {
            EnableAgentControl(agent);
        }
    }

    private void PauseSimulation()
    {
        Debug.Log("Pause Simulation.");
        isPlaying = false;

        foreach (GameObject agent in spawnedAgents)
        {
            DisableAgentControl(agent);
        }
    }

    private void ResetSimulation()
    {
        Debug.Log("Reset Simulation.");
        isPlaying = false;

        foreach (GameObject agent in spawnedAgents)
        {
            if (agent != null)
            {
                Destroy(agent);
            }
        }

        spawnedAgents.Clear();
    }
    // remembers seeks/hiders/agents
    private void RegisterSpawnedAgent(GameObject agent)
    {
        if (agent == null || spawnedAgents.Contains(agent))
        {
            return;
        }

        spawnedAgents.Add(agent);

        if (!isPlaying)
        {
            // if i spawn an agent while not playing, freeze agent (prevents seeker from running if hider not setup yet)
            DisableAgentControl(agent);
        }
    }

    private void SpawnTraining(GameObject simulationPrefab)
    {
        if (simulationPrefab == null)
        {
            Debug.LogWarning("Cannot spawn training environments because no simulation prefab is assigned.");
            return;
        }

        for (int i = 0; i < rowCount; i++)
        {
            for (int j = 0; j < rowCount; j++)
            {
                GameObject environment = Instantiate(simulationPrefab);
                environment.transform.position = new Vector3(
                    i * spaceBetween,
                    0,
                    j * spaceBetween);

                spawnedEnvironments.Add(environment);
            }
        }
    }

    private void SpawnTesting(GameObject simulationPrefab)
    {
        if (simulationPrefab == null)
        {
            Debug.LogWarning("Cannot spawn testing environment because no simulation prefab is assigned.");
            return;
        }

        GameObject environment = Instantiate(simulationPrefab);
        spawnedEnvironments.Add(environment);
    }

    /*
        for controlling if agent is active for ai or frozen for editing
     */
    private void EnableAgentControl(GameObject agentObject)
    {
        foreach (var behaviour in agentObject.GetComponentsInChildren<BehaviorParameters>())
        {
            behaviour.enabled = true;
        }

        foreach (var decisionRequester in agentObject.GetComponentsInChildren<DecisionRequester>())
        {
            decisionRequester.enabled = true;
        }
        foreach (var agent in agentObject.GetComponentsInChildren<Agent>())
        {
            agent.enabled = true;
        }
        foreach (var rb in agentObject.GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = false;
        }
    }

    private void DisableAgentControl(GameObject agentObject)
    {
        foreach (DecisionRequester decisionRequester in agentObject.GetComponentsInChildren<DecisionRequester>())
        {
            decisionRequester.enabled = false;
        }

        foreach (BehaviorParameters behaviorParameters in agentObject.GetComponentsInChildren<BehaviorParameters>())
        {
            behaviorParameters.enabled = false;
        }

        foreach (Agent agent in agentObject.GetComponentsInChildren<Agent>())
        {
            agent.enabled = false;
        }

        foreach (Rigidbody rb in agentObject.GetComponentsInChildren<Rigidbody>())
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }
}
