using Unity.MLAgents.Sensors;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LocalGridSensorComponent : SensorComponent
{
    [SerializeField, Min(1)] private int radius = 5;
    [SerializeField, Min(1)] private int supersample = 2;
    [Tooltip("Reveal walls only after they enter the team view. Use a fresh model when enabled.")]
    [SerializeField] private bool useDiscoveredMap;
    public bool UseDiscoveredMap => useDiscoveredMap;

    private ScenarioGrid grid;
    private InfluenceMap coverage;
    private System.Func<int> currentStep;

    public void Configure(ScenarioGrid scenarioGrid, InfluenceMap teamCoverage,
        System.Func<int> stepProvider)
    {
        grid = scenarioGrid;
        coverage = teamCoverage;
        currentStep = stepProvider;
    }

    public override ISensor[] CreateSensors()
    {
        return new ISensor[]
        {
            new LocalGridSensor(transform, () => grid, () => coverage,
                () => currentStep != null ? currentStep() : 0, radius, supersample,
                useDiscoveredMap)
        };
    }
}
