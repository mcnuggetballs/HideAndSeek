using Unity.MLAgents.Sensors;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LocalGridSensorComponent : SensorComponent
{
    [SerializeField, Min(1)] private int radius = 5;
    [SerializeField, Min(1)] private int supersample = 2;

    private ScenarioGrid grid;

    public void Configure(ScenarioGrid scenarioGrid) => grid = scenarioGrid;

    public override ISensor[] CreateSensors()
    {
        return new ISensor[]
        {
            new LocalGridSensor(transform, () => grid, radius, supersample)
        };
    }
}
