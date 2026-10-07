using System;
using System.Collections.Generic;
using System.IO;
using Unity.MLAgents;
using UnityEngine;

/// <summary>
/// One-policy, four-stage curriculum. Each stage changes only the initial state;
/// observations, actions, reward amounts and episode termination stay constant.
/// This lesson deliberately uses one seeker and one hider. From Chase onward,
/// the hider flees after the seeker first spots it.
/// </summary>
public sealed class OrderedPoliceCurriculum : ITrainingCurriculum
{
    public enum Stage { Capture, Chase, Detect, Search }

    [Serializable]
    private sealed class Progress
    {
        public int stage;
        public int sequence;
        public List<int> successWindow = new();
    }

    // A success must be prompt enough to demonstrate the current skill, not merely
    // a lucky capture near the end of the shared 512-step episode deadline.
    private static readonly int[] StageStepLimits = { 60, 160, 240, 512 };
    private readonly int windowSize;
    private readonly float threshold;
    private readonly string savePath;
    private readonly System.Random random;
    private Progress progress;

    public Stage CurrentStage => (Stage)progress.stage;

    public OrderedPoliceCurriculum(int windowSize, float threshold, string savePath,
        int randomSeed, bool resumeProgress)
    {
        this.windowSize = Mathf.Max(1, windowSize);
        this.threshold = Mathf.Clamp01(threshold);
        this.savePath = savePath;
        random = new System.Random(randomSeed);
        Load(resumeProgress);
    }

    public EpisodeSpecification CreateInitialEpisode(EnvironmentInstance environment) =>
        CreateEpisode(environment);

    public EpisodeSpecification CreateNextEpisode(EnvironmentInstance environment,
        EpisodeOutcome outcome)
    {
        // Forty arenas may still finish an earlier stage after another arena advances it.
        // A manually opened scene falls back to the heuristic. Its captures must
        // not skip lessons before the Python trainer attaches.
        if (Academy.Instance.IsCommunicatorOn && !outcome.WasInterrupted &&
            outcome.Difficulty == progress.stage)
        {
            bool mastered = outcome.WasSuccessful &&
                outcome.Steps <= StageStepLimits[progress.stage];
            progress.successWindow.Add(mastered ? 1 : 0);
            while (progress.successWindow.Count > windowSize)
                progress.successWindow.RemoveAt(0);

            if (progress.successWindow.Count == windowSize)
            {
                int wins = 0;
                foreach (int result in progress.successWindow) wins += result;
                if ((float)wins / windowSize >= threshold &&
                    progress.stage < (int)Stage.Search)
                {
                    progress.stage++;
                    progress.successWindow.Clear();
                    Debug.Log($"[Ordered Curriculum] Advanced to {CurrentStage}.");
                }
            }
            Save();
        }
        return CreateEpisode(environment);
    }

    private EpisodeSpecification CreateEpisode(EnvironmentInstance environment)
    {
        if (environment == null) throw new ArgumentNullException(nameof(environment));
        if (environment.Seekers.Count != 1 || environment.Hiders.Count != 1)
            throw new InvalidOperationException(
                "OrderedPoliceCurriculum requires exactly one seeker and one hider. " +
                "Complete these four skills before a separate team curriculum.");

        ScenarioGrid grid = environment.Grid;
        List<Vector2Int> walkable = new();
        foreach (Vector2Int cell in grid.GetAllCells())
            if (grid.GetCell(cell) != ScenarioGrid.WallCell) walkable.Add(cell);

        // Try many seeker cells because Search requires a reachable, wall-occluded pair.
        for (int attempt = 0; attempt < 512; attempt++)
        {
            Vector2Int seeker = walkable[random.Next(walkable.Count)];
            int[,] distances = PathDistances(grid, seeker);
            List<Vector2Int> candidates = new();
            foreach (Vector2Int hider in walkable)
            {
                int distance = distances[hider.x, hider.y];
                if (!MatchesStage(grid, seeker, hider, distance)) continue;
                candidates.Add(hider);
            }
            if (candidates.Count == 0) continue;
            Vector2Int selected = candidates[random.Next(candidates.Count)];
            progress.sequence++;
            bool faceTarget = progress.stage <= (int)Stage.Chase;
            bool faceAway = progress.stage == (int)Stage.Detect;
            return new EpisodeSpecification(progress.sequence, progress.stage,
                new[] { seeker }, new[] { selected }, faceTarget, faceAway,
                progress.stage >= (int)Stage.Chase);
        }

        throw new InvalidOperationException(
            $"Map cannot supply reachable {CurrentStage} spawn pairs. " +
            "Use an open area for Capture/Chase/Detect and a wall with a route around it for Search.");
    }

    private bool MatchesStage(ScenarioGrid grid, Vector2Int seeker,
        Vector2Int hider, int pathDistance)
    {
        if (pathDistance < 0) return false;
        bool clearLine = HasClearGridLine(grid, seeker, hider);
        switch (CurrentStage)
        {
            case Stage.Capture: return pathDistance == 1 && clearLine;
            case Stage.Chase:
            case Stage.Detect:
                // Two 5-unit cells equal the seeker's 10-unit sight range.
                return pathDistance == 2 && clearLine;
            case Stage.Search:
                return pathDistance >= 3 && pathDistance <= 8 && !clearLine;
            default: return false;
        }
    }

    private static bool HasClearGridLine(ScenarioGrid grid,
        Vector2Int start, Vector2Int end)
    {
        Vector3 a = grid.CellToWorld(start);
        Vector3 b = grid.CellToWorld(end);
        int samples = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) /
            (grid.CellSize * 0.25f)));
        for (int i = 1; i < samples; i++)
            if (grid.GetCell(grid.WorldToCell(Vector3.Lerp(a, b, (float)i / samples))) ==
                ScenarioGrid.WallCell)
                return false;
        return true;
    }

    private static int[,] PathDistances(ScenarioGrid grid, Vector2Int start)
    {
        int[,] distance = new int[grid.Width, grid.Height];
        for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++) distance[x, y] = -1;
        Queue<Vector2Int> queue = new();
        distance[start.x, start.y] = 0;
        queue.Enqueue(start);
        Vector2Int[] moves = { Vector2Int.left, Vector2Int.right,
            Vector2Int.up, Vector2Int.down };
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            foreach (Vector2Int move in moves)
            {
                Vector2Int next = current + move;
                if (!grid.IsInsideGrid(next) || distance[next.x, next.y] >= 0 ||
                    grid.GetCell(next) == ScenarioGrid.WallCell) continue;
                distance[next.x, next.y] = distance[current.x, current.y] + 1;
                queue.Enqueue(next);
            }
        }
        return distance;
    }

    private void Load(bool resume)
    {
        try
        {
            if (resume && !string.IsNullOrWhiteSpace(savePath) && File.Exists(savePath))
                progress = JsonUtility.FromJson<Progress>(File.ReadAllText(savePath));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Ordered Curriculum] Could not load progress: {exception.Message}");
        }
        progress ??= new Progress();
        progress.stage = Mathf.Clamp(progress.stage, 0, (int)Stage.Search);
        progress.successWindow ??= new List<int>();
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(savePath)) return;
        try { File.WriteAllText(savePath, JsonUtility.ToJson(progress, true)); }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Ordered Curriculum] Could not save progress: {exception.Message}");
        }
    }
}
