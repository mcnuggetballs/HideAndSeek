using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared, per-arena memory of floor cells the seeker team has actually viewed.
/// A checked cell is walkable, grid-reachable from a seeker spawn, inside the
/// seeker's view cone and range, and not hidden by a grid wall.
/// </summary>
public class InfluenceMap : MonoBehaviour
{
    public enum LayerTag { WasSeen, TimeSinceLastSeen, AgentPositions }

    public const int CoverageAgeHorizonSteps = 512;

    private static readonly Vector2Int[] Neighbours =
    {
        Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down
    };

    private ScenarioGrid grid;
    private Dictionary<LayerTag, float[,]> layers;
    private int[,] componentIds;
    private int[,] lastSeenSteps;
    private int[,] teamVisibilityMasks;
    private readonly List<Vector2Int> visibleBuffer = new();
    private readonly List<Vector2Int> teamVisibleUnion = new();
    private readonly List<List<Vector2Int>> teamVisibleBuffers = new();
    private bool[] reachableComponents;
    private int componentCount;
    private int currentStep;

    public int CheckedCellCount { get; private set; }
    public int ReachableCellCount { get; private set; }

    public void Initialise(ScenarioGrid gridRef)
    {
        if (gridRef == null) throw new System.ArgumentNullException(nameof(gridRef));
        grid = gridRef;
        layers = new Dictionary<LayerTag, float[,]>
        {
            { LayerTag.WasSeen, new float[grid.Height, grid.Width] },
            { LayerTag.TimeSinceLastSeen, new float[grid.Height, grid.Width] },
            { LayerTag.AgentPositions, new float[grid.Height, grid.Width] }
        };
        lastSeenSteps = new int[grid.Width, grid.Height];
        teamVisibilityMasks = new int[grid.Width, grid.Height];
        BuildWalkableComponents();
        ResetEpisode();
    }

    /// <summary>Exclude disconnected floor regions that neither seeker can enter.</summary>
    public void SetEpisodeSeekers(IReadOnlyList<SeekerAgent> seekers)
    {
        if (grid == null || reachableComponents == null) return;
        System.Array.Clear(reachableComponents, 0, reachableComponents.Length);
        if (seekers != null)
            foreach (SeekerAgent seeker in seekers)
            {
                if (seeker == null) continue;
                Vector2Int cell = grid.WorldToCell(seeker.transform.position);
                if (!grid.IsInsideGrid(cell)) continue;
                int id = componentIds[cell.x, cell.y];
                if (id >= 0) reachableComponents[id] = true;
            }

        ReachableCellCount = 0;
        foreach (Vector2Int cell in grid.GetAllCells())
        {
            int id = componentIds[cell.x, cell.y];
            if (id >= 0 && reachableComponents[id]) ReachableCellCount++;
        }
    }

    /// <returns>Floor cells newly checked by the whole team this episode.</returns>
    public int MarkVisibleCells(Vector3 observer, Vector3 forward,
        float viewDistance, float viewAngle, int step)
    {
        if (grid == null || layers == null) return 0;
        visibleBuffer.Clear();
        CollectVisibleFloorCells(observer, forward, viewDistance, viewAngle,
            visibleBuffer);
        currentStep = step;
        int newCells = 0;
        float[,] checkedLayer = layers[LayerTag.WasSeen];
        foreach (Vector2Int cell in visibleBuffer)
        {
            lastSeenSteps[cell.x, cell.y] = step;
            if (checkedLayer[cell.y, cell.x] > 0f) continue;
            checkedLayer[cell.y, cell.x] = 1f;
            CheckedCellCount++;
            newCells++;
        }
        return newCells;
    }

    /// <summary>
    /// Marks both seekers' views against the same pre-update coverage snapshot.
    /// Exclusive counts have no iteration-order bias; shared cells count once.
    /// </summary>
    public int MarkTeamVisibleCells(IReadOnlyList<SeekerAgent> seekers, int step,
        int[] exclusiveNewCells, out int sharedNewCells)
    {
        sharedNewCells = 0;
        if (seekers == null || exclusiveNewCells == null ||
            exclusiveNewCells.Length < seekers.Count || seekers.Count > 30)
            throw new System.ArgumentException("Provide one exclusive-cell slot per seeker (at most 30).");
        System.Array.Clear(exclusiveNewCells, 0, exclusiveNewCells.Length);
        if (grid == null || layers == null) return 0;

        currentStep = step;
        teamVisibleUnion.Clear();
        while (teamVisibleBuffers.Count < seekers.Count)
            teamVisibleBuffers.Add(new List<Vector2Int>());
        for (int i = 0; i < seekers.Count; i++)
        {
            List<Vector2Int> cells = teamVisibleBuffers[i];
            cells.Clear();
            SeekerAgent seeker = seekers[i];
            if (seeker == null) continue;
            CollectVisibleFloorCells(seeker.transform.position,
                seeker.transform.forward, seeker.ViewDistance, seeker.ViewAngle, cells);
            int bit = 1 << i;
            foreach (Vector2Int cell in cells)
            {
                if (teamVisibilityMasks[cell.x, cell.y] == 0)
                    teamVisibleUnion.Add(cell);
                teamVisibilityMasks[cell.x, cell.y] |= bit;
            }
        }

        int newCells = 0;
        float[,] checkedLayer = layers[LayerTag.WasSeen];
        foreach (Vector2Int cell in teamVisibleUnion)
        {
            int mask = teamVisibilityMasks[cell.x, cell.y];
            teamVisibilityMasks[cell.x, cell.y] = 0;
            lastSeenSteps[cell.x, cell.y] = step;
            if (checkedLayer[cell.y, cell.x] > 0f) continue;
            checkedLayer[cell.y, cell.x] = 1f;
            CheckedCellCount++;
            newCells++;
            if ((mask & (mask - 1)) != 0)
            {
                sharedNewCells++;
                continue;
            }
            for (int i = 0; i < seekers.Count; i++)
                if ((mask & (1 << i)) != 0) { exclusiveNewCells[i]++; break; }
        }
        return newCells;
    }

    private void CollectVisibleFloorCells(Vector3 observer, Vector3 forward,
        float viewDistance, float viewAngle, List<Vector2Int> output)
    {
        if (grid == null || layers == null || viewDistance <= 0f) return;
        Vector2Int origin = grid.WorldToCell(observer);
        if (!grid.IsInsideGrid(origin)) return;
        int component = componentIds[origin.x, origin.y];
        if (component < 0 || !reachableComponents[component]) return;

        float maxDistanceSquared = viewDistance * viewDistance;
        float halfAngleCos = Mathf.Cos(Mathf.Clamp(viewAngle, 1f, 360f) * 0.5f * Mathf.Deg2Rad);
        Vector2 flatForward = new Vector2(forward.x, forward.z).normalized;
        int radius = Mathf.CeilToInt(viewDistance / grid.CellSize) + 1;
        for (int y = Mathf.Max(0, origin.y - radius);
             y <= Mathf.Min(grid.Height - 1, origin.y + radius); y++)
            for (int x = Mathf.Max(0, origin.x - radius);
                 x <= Mathf.Min(grid.Width - 1, origin.x + radius); x++)
            {
                if (componentIds[x, y] != component) continue;
                Vector2Int cell = new Vector2Int(x, y);
                Vector3 target = grid.CellToWorld(cell);
                Vector2 offset = new Vector2(target.x - observer.x, target.z - observer.z);
                float distanceSquared = offset.sqrMagnitude;
                bool occupiedCell = cell == origin;
                if (!occupiedCell && (distanceSquared > maxDistanceSquared ||
                    Vector2.Dot(flatForward, offset) <
                    Mathf.Sqrt(distanceSquared) * halfAngleCos)) continue;
                if (!occupiedCell && !HasClearGridLine(observer, cell)) continue;

                output.Add(cell);
            }
    }

    public void GetCoverage(Vector2Int cell, int step, out float checkedBefore,
        out float normalizedAge)
    {
        checkedBefore = 0f;
        normalizedAge = 1f;
        if (grid == null || layers == null || !grid.IsInsideGrid(cell)) return;
        checkedBefore = layers[LayerTag.WasSeen][cell.y, cell.x];
        if (checkedBefore > 0f)
            normalizedAge = Mathf.Clamp01((float)Mathf.Max(0,
                step - lastSeenSteps[cell.x, cell.y]) / CoverageAgeHorizonSteps);
    }

    public float GetValue(Vector3 worldPosition, LayerTag layer)
    {
        if (grid == null || layers == null) return 0f;
        Vector2Int cell = grid.WorldToCell(worldPosition);
        if (!grid.IsInsideGrid(cell) || !layers.TryGetValue(layer, out float[,] data))
            return 0f;
        if (layer == LayerTag.TimeSinceLastSeen) UpdateTimeLastSeen();
        return data[cell.y, cell.x];
    }

    public bool TryGetLayer(LayerTag layer, out float[,] data)
    {
        data = null;
        if (layers == null || !layers.TryGetValue(layer, out data)) return false;
        if (layer == LayerTag.TimeSinceLastSeen) UpdateTimeLastSeen();
        return true;
    }

    public void UpdateAgentPositions(List<Transform> agents)
    {
        if (grid == null || layers == null) return;
        float[,] data = layers[LayerTag.AgentPositions];
        System.Array.Clear(data, 0, data.Length);
        foreach (Transform agent in agents)
        {
            if (agent == null) continue;
            Vector2Int cell = grid.WorldToCell(agent.position);
            if (grid.IsInsideGrid(cell)) data[cell.y, cell.x] = 1f;
        }
    }

    public void ResetEpisode()
    {
        if (layers == null) return;
        foreach (float[,] layer in layers.Values)
            System.Array.Clear(layer, 0, layer.Length);
        for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                lastSeenSteps[x, y] = -1;
        CheckedCellCount = 0;
        if (teamVisibilityMasks != null)
            System.Array.Clear(teamVisibilityMasks, 0, teamVisibilityMasks.Length);
        teamVisibleUnion.Clear();
        ReachableCellCount = 0;
        currentStep = 0;
        if (reachableComponents != null)
            System.Array.Clear(reachableComponents, 0, reachableComponents.Length);
    }

    private void UpdateTimeLastSeen()
    {
        float[,] ages = layers[LayerTag.TimeSinceLastSeen];
        for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                ages[y, x] = lastSeenSteps[x, y] < 0 ? 1f :
                    Mathf.Clamp01((float)Mathf.Max(0,
                        currentStep - lastSeenSteps[x, y]) / CoverageAgeHorizonSteps);
    }

    private void BuildWalkableComponents()
    {
        componentIds = new int[grid.Width, grid.Height];
        for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++) componentIds[x, y] = -1;

        componentCount = 0;
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        foreach (Vector2Int start in grid.GetAllCells())
        {
            if (grid.GetCell(start) == ScenarioGrid.WallCell ||
                componentIds[start.x, start.y] >= 0) continue;
            componentIds[start.x, start.y] = componentCount;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int move in Neighbours)
                {
                    Vector2Int next = current + move;
                    if (!grid.IsInsideGrid(next) ||
                        grid.GetCell(next) == ScenarioGrid.WallCell ||
                        componentIds[next.x, next.y] >= 0) continue;
                    componentIds[next.x, next.y] = componentCount;
                    queue.Enqueue(next);
                }
            }
            componentCount++;
        }
        reachableComponents = new bool[componentCount];
    }

    // Traverse every grid cell crossed by the sight line. Corner crossings test
    // both adjacent wall cells, so a diagonal gap cannot reveal cells behind walls.
    private bool HasClearGridLine(Vector3 observer, Vector2Int target)
    {
        Vector2Int cell = grid.WorldToCell(observer);
        Vector3 end = grid.CellToWorld(target);
        float dx = end.x - observer.x;
        float dz = end.z - observer.z;
        int stepX = dx > 0f ? 1 : dx < 0f ? -1 : 0;
        int stepZ = dz > 0f ? 1 : dz < 0f ? -1 : 0;
        float cellSize = grid.CellSize;
        float left = grid.Origin.x - grid.Width * cellSize * 0.5f;
        float bottom = grid.Origin.z - grid.Height * cellSize * 0.5f;
        float tMaxX = stepX == 0 ? float.PositiveInfinity :
            (left + (cell.x + (stepX > 0 ? 1 : 0)) * cellSize - observer.x) / dx;
        float tMaxZ = stepZ == 0 ? float.PositiveInfinity :
            (bottom + (cell.y + (stepZ > 0 ? 1 : 0)) * cellSize - observer.z) / dz;
        float tDeltaX = stepX == 0 ? float.PositiveInfinity : cellSize / Mathf.Abs(dx);
        float tDeltaZ = stepZ == 0 ? float.PositiveInfinity : cellSize / Mathf.Abs(dz);

        for (int i = 0; i <= grid.Width + grid.Height; i++)
        {
            if (cell == target) return true;
            if (Mathf.Abs(tMaxX - tMaxZ) < 0.00001f)
            {
                if (IsWall(cell + new Vector2Int(stepX, 0)) ||
                    IsWall(cell + new Vector2Int(0, stepZ))) return false;
                cell += new Vector2Int(stepX, stepZ);
                tMaxX += tDeltaX;
                tMaxZ += tDeltaZ;
            }
            else if (tMaxX < tMaxZ)
            {
                cell.x += stepX;
                tMaxX += tDeltaX;
            }
            else
            {
                cell.y += stepZ;
                tMaxZ += tDeltaZ;
            }
            if (!grid.IsInsideGrid(cell) || IsWall(cell)) return false;
        }
        return false;
    }

    private bool IsWall(Vector2Int cell) =>
        !grid.IsInsideGrid(cell) || grid.GetCell(cell) == ScenarioGrid.WallCell;
}
