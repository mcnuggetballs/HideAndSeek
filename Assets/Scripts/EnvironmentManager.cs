using System.Collections.Generic;
using UnityEngine;


public class EnvironmentManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject environmentPrefab;
    [SerializeField] private GameObject seekerPrefab;
    [SerializeField] private GameObject hiderPrefab;
    [SerializeField] private GameObject obstaclePrefab;

    [Header("Scene References (Shared Systems)")]
    [SerializeField] private WorldBuilder worldBuilder;
    [SerializeField] private RuntimeNavMeshBuilder runtimeNavMeshBuilder;
    [SerializeField] private InfluenceMap influenceMap;
    [SerializeField] private GridRenderer gridRenderer;

    private readonly List<EnvironmentInstance> environments = new();
    public IReadOnlyList<EnvironmentInstance> Environments => environments;

    //  ENTRY POINT
    public void CreateEnvironments(List<ScenarioGrid> grids)
    {
        ClearExisting();

        foreach (var grid in grids)
        {
            CreateSingleEnvironment(grid);
        }

        Debug.Log("[EnvironmeentManager] Create {environments.Count} environments");
    }

    // SINGLE ENVIRONMENT BUILD
    private void CreateSingleEnvironment(ScenarioGrid grid)
    {
        // 1. create root to store all instantiated environments
        GameObject envRoot = Instantiate(environmentPrefab, transform);
        envRoot.name = $"Environment_{environments.Count}";

        Transform runtimeRoot = envRoot.transform.Find("RuntimeRoot");
        //Transform editorRoot = envRoot.transform.Find("EditorRoot");

        // 2. create instance
        EnvironmentInstance env = new EnvironmentInstance
        {
            root = envRoot,
            runtimeRoot = runtimeRoot,
            grid = grid,

            //worldBuilder = worldBuilder, 
            //runtimeNavMeshBuilder = runtimeNavMeshBuilder,
            //influenceMap = influenceMap,
            //gridRenderer = gridRenderer,

            //isBuilt Built = false,
            //navMeshReady = false,
        };

        // 3. build world
        BuildEnvironment(env);
        environments.Add(env);

    }

    // BUILD PIPELINE
    private void BuildEnvironment(EnvironmentInstance env)
    {
        // 1. build geometry

        // 2. navmesh

        // 3. analysis layers
    }

    // CLEAN UP 
    private void ClearExisting()
    {
        foreach (var env in environments)
        {
            if (env.root != null)
                Destroy(env.root);
        }

        environments.Clear();
    }
}
