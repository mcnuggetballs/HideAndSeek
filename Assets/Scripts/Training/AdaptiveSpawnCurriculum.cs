using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Shared curriculum for every parallel environment. It widens the allowed hider
/// distance after a pooled success window and reduces the probability of repeatedly
/// using easy capture cells. Adapted from Loh Shao Cong's RedVsBlue curriculum work.
/// </summary>
public sealed class AdaptiveSpawnCurriculum : ITrainingCurriculum
{
    [Serializable]
    private sealed class CatchCount
    {
        public int layoutHash;
        public int x;
        public int y;
        public int count;
    }

    [Serializable]
    private sealed class PersistedState
    {
        public int maxHiderDistance = -1;
        public int sequence;
        public int advanceCount;
        public List<int> successWindow = new();
        public List<CatchCount> catchCounts = new();
    }

    private readonly int minHiderDistance;
    private readonly int startingMaxHiderDistance;
    private readonly int distanceIncrement;
    private readonly int successWindowSize;
    private readonly float successThreshold;
    private readonly string savePath;
    private readonly System.Random random;
    private readonly Dictionary<(int layout, int x, int y), CatchCount> catchIndex = new();
    private PersistedState state;

    public AdaptiveSpawnCurriculum(int minHiderDistance, int startingMaxHiderDistance,
        int distanceIncrement, int successWindowSize, float successThreshold,
        string savePath, int randomSeed)
    {
        if (minHiderDistance < 1) throw new ArgumentOutOfRangeException(nameof(minHiderDistance));
        if (startingMaxHiderDistance < minHiderDistance)
            throw new ArgumentOutOfRangeException(nameof(startingMaxHiderDistance));

        this.minHiderDistance = minHiderDistance;
        this.startingMaxHiderDistance = startingMaxHiderDistance;
        this.distanceIncrement = Mathf.Max(1, distanceIncrement);
        this.successWindowSize = Mathf.Max(1, successWindowSize);
        this.successThreshold = Mathf.Clamp01(successThreshold);
        this.savePath = savePath;
        random = new System.Random(randomSeed);
        Load();
    }

    public EpisodeSpecification CreateInitialEpisode(EnvironmentInstance environment) =>
        CreateEpisode(environment);

    public EpisodeSpecification CreateNextEpisode(EnvironmentInstance environment,
        EpisodeOutcome previousOutcome)
    {
        RecordOutcome(environment.Grid, previousOutcome);
        Save();
        return CreateEpisode(environment);
    }

    private EpisodeSpecification CreateEpisode(EnvironmentInstance environment)
    {
        ScenarioGrid grid = environment?.Grid ?? throw new ArgumentNullException(nameof(environment));
        int seekerCount = environment.Seekers.Count;
        int hiderCount = environment.Hiders.Count;
        List<Vector2Int> walkable = GetWalkableCells(grid);

        if (seekerCount < 1 || hiderCount < 1)
            throw new InvalidOperationException("A curriculum episode requires both teams.");
        if (walkable.Count < seekerCount + hiderCount)
            throw new InvalidOperationException("The scenario has too few walkable cells for all participants.");

        Vector2Int[] seekers = DrawUniformWithoutReplacement(walkable, seekerCount);
        HashSet<Vector2Int> occupied = new(seekers);
        List<Vector2Int> band = ComputeDistanceBand(grid, seekers,
            minHiderDistance, state.maxHiderDistance);
        band.RemoveAll(occupied.Contains);

        if (band.Count < hiderCount)
        {
            // Relax the difficulty band while keeping every hider reachable from a seeker.
            band = ComputeDistanceBand(grid, seekers, 1, int.MaxValue);
            band.RemoveAll(occupied.Contains);
        }
        if (band.Count < hiderCount)
            throw new InvalidOperationException(
                "The scenario has too few reachable cells for all hiders.");

        int layoutHash = ComputeLayoutHash(grid);
        float[] weights = new float[band.Count];
        for (int i = 0; i < band.Count; i++)
        {
            Vector2Int cell = band[i];
            int catches = catchIndex.TryGetValue((layoutHash, cell.x, cell.y), out CatchCount entry)
                ? entry.count
                : 0;
            weights[i] = 1f / (1f + catches);
        }

        Vector2Int[] hiders = DrawWeightedWithoutReplacement(band, weights, hiderCount);
        state.sequence++;
        return new EpisodeSpecification(state.sequence, state.maxHiderDistance, seekers, hiders);
    }

    private void RecordOutcome(ScenarioGrid grid, EpisodeOutcome outcome)
    {
        int layoutHash = ComputeLayoutHash(grid);
        foreach (Vector2Int cell in outcome.CaptureCells)
        {
            var key = (layoutHash, cell.x, cell.y);
            if (!catchIndex.TryGetValue(key, out CatchCount entry))
            {
                entry = new CatchCount { layoutHash = layoutHash, x = cell.x, y = cell.y };
                catchIndex.Add(key, entry);
                state.catchCounts.Add(entry);
            }
            entry.count++;
        }

        state.successWindow.Add(outcome.WasSuccessful ? 1 : 0);
        while (state.successWindow.Count > successWindowSize)
            state.successWindow.RemoveAt(0);

        if (state.successWindow.Count < successWindowSize)
            return;

        int successes = 0;
        foreach (int result in state.successWindow) successes += result;
        if ((float)successes / successWindowSize < successThreshold)
            return;

        state.maxHiderDistance += distanceIncrement;
        state.advanceCount++;
        state.successWindow.Clear();
        Debug.Log($"[Curriculum] Hider maximum distance advanced to {state.maxHiderDistance}.");
    }

    private static List<Vector2Int> GetWalkableCells(ScenarioGrid grid)
    {
        List<Vector2Int> cells = new();
        foreach (Vector2Int cell in grid.GetAllCells())
            if (grid.GetCell(cell) != ScenarioGrid.WallCell)
                cells.Add(cell);
        return cells;
    }

    private static List<Vector2Int> ComputeDistanceBand(ScenarioGrid grid,
        IReadOnlyList<Vector2Int> starts, int minimum, int maximum)
    {
        int[,] distances = new int[grid.Width, grid.Height];
        for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
                distances[x, y] = -1;

        Queue<Vector2Int> frontier = new();
        foreach (Vector2Int start in starts)
        {
            distances[start.x, start.y] = 0;
            frontier.Enqueue(start);
        }

        Vector2Int[] directions = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            int nextDistance = distances[current.x, current.y] + 1;
            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (!grid.IsInsideGrid(next) || distances[next.x, next.y] >= 0 ||
                    grid.GetCell(next) == ScenarioGrid.WallCell)
                    continue;
                distances[next.x, next.y] = nextDistance;
                frontier.Enqueue(next);
            }
        }

        List<Vector2Int> result = new();
        for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
                if (distances[x, y] >= minimum && distances[x, y] <= maximum)
                    result.Add(new Vector2Int(x, y));
        return result;
    }

    private Vector2Int[] DrawUniformWithoutReplacement(List<Vector2Int> source, int count)
    {
        List<Vector2Int> pool = new(source);
        Vector2Int[] result = new Vector2Int[count];
        for (int i = 0; i < count; i++)
        {
            int index = random.Next(pool.Count);
            result[i] = pool[index];
            pool.RemoveAt(index);
        }
        return result;
    }

    private Vector2Int[] DrawWeightedWithoutReplacement(List<Vector2Int> cells,
        float[] weights, int count)
    {
        List<Vector2Int> pool = new(cells);
        List<float> poolWeights = new(weights);
        Vector2Int[] result = new Vector2Int[count];

        for (int draw = 0; draw < count; draw++)
        {
            float total = 0f;
            foreach (float weight in poolWeights) total += weight;
            double roll = random.NextDouble() * total;
            int selected = pool.Count - 1;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= poolWeights[i];
                if (roll <= 0d) { selected = i; break; }
            }

            result[draw] = pool[selected];
            pool.RemoveAt(selected);
            poolWeights.RemoveAt(selected);
        }
        return result;
    }

    private static int ComputeLayoutHash(ScenarioGrid grid)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + grid.Width;
            hash = hash * 31 + grid.Height;
            foreach (Vector2Int cell in grid.GetAllCells())
                if (grid.GetCell(cell) == ScenarioGrid.WallCell)
                    hash = (hash * 31 + cell.x * 397) ^ cell.y;
            return hash;
        }
    }

    private void Load()
    {
        state = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(savePath) && File.Exists(savePath))
                state = JsonUtility.FromJson<PersistedState>(File.ReadAllText(savePath));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Curriculum] Could not load progress: {exception.Message}");
        }

        state ??= new PersistedState();
        state.successWindow ??= new List<int>();
        state.catchCounts ??= new List<CatchCount>();
        if (state.maxHiderDistance < minHiderDistance)
            state.maxHiderDistance = startingMaxHiderDistance;

        catchIndex.Clear();
        foreach (CatchCount entry in state.catchCounts)
            catchIndex[(entry.layoutHash, entry.x, entry.y)] = entry;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(savePath)) return;
        try
        {
            File.WriteAllText(savePath, JsonUtility.ToJson(state, true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Curriculum] Could not save progress: {exception.Message}");
        }
    }
}
