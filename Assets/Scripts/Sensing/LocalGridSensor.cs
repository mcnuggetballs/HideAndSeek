using System;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// Heading-aligned wall and team-coverage window. The sensor reads ScenarioGrid
/// and its arena's InfluenceMap; render objects never become training data.
/// </summary>
public sealed class LocalGridSensor : ISensor
{
    private readonly Transform agent;
    private readonly Func<ScenarioGrid> getGrid;
    private readonly Func<InfluenceMap> getCoverage;
    private readonly Func<int> getStep;
    private readonly int radius;
    private readonly int supersample;
    private readonly int size;
    private readonly ObservationSpec specification;

    public LocalGridSensor(Transform agent, Func<ScenarioGrid> getGrid,
        Func<InfluenceMap> getCoverage, Func<int> getStep,
        int radius, int supersample)
    {
        this.agent = agent;
        this.getGrid = getGrid;
        this.getCoverage = getCoverage;
        this.getStep = getStep;
        this.radius = Mathf.Max(1, radius);
        this.supersample = Mathf.Max(1, supersample);
        size = 2 * this.radius * this.supersample + 1;
        specification = ObservationSpec.Visual(4, size, size);
    }

    public ObservationSpec GetObservationSpec() => specification;

    public int Write(ObservationWriter writer)
    {
        ScenarioGrid grid = getGrid?.Invoke();
        InfluenceMap coverage = getCoverage?.Invoke();
        int step = getStep != null ? getStep() : 0;
        int half = radius * supersample;
        float sampleSpacing = grid != null ? grid.CellSize / supersample : 1f;

        Vector3 forward = agent.forward;
        Vector3 right = agent.right;
        forward.y = 0f;
        right.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;

        for (int row = 0; row < size; row++)
        {
            float forwardDistance = (row - half) * sampleSpacing;
            for (int column = 0; column < size; column++)
            {
                float rightDistance = (column - half) * sampleSpacing;
                Vector3 sample = agent.position + forward * forwardDistance + right * rightDistance;
                Vector2Int cell = grid != null ? grid.WorldToCell(sample) : new Vector2Int(-1, -1);
                bool outside = grid == null || !grid.IsInsideGrid(cell);
                writer[0, row, column] = !outside &&
                    grid.GetCell(cell) == ScenarioGrid.WallCell ? 1f : 0f;
                writer[1, row, column] = outside ? 1f : 0f;
                float checkedBefore = 0f;
                float age = 1f;
                if (!outside && coverage != null)
                    coverage.GetCoverage(cell, step, out checkedBefore, out age);
                writer[2, row, column] = checkedBefore;
                writer[3, row, column] = age;
            }
        }

        return 4 * size * size;
    }

    public byte[] GetCompressedObservation() => null;
    public void Update() { }
    public void Reset() { }
    public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();
    public string GetName() => "LocalGridSensor";
}
