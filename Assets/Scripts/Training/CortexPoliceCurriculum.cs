using System;
using System.Collections.Generic;
using System.IO;
using Unity.MLAgents;
using UnityEngine;

/// <summary>
/// Two-seeker Cortex1 lesson: Capture, Chase, Detect, then wall-hidden Search.
/// Search grows by walkable path distance without changing the policy inputs or rewards.
/// The one shared instance pools completed episodes from every training arena.
/// </summary>
public sealed class CortexPoliceCurriculum : ITrainingCurriculum
{
    public enum Stage { Capture, Chase, Detect, Search }

    [Serializable]
    private sealed class Progress
    {
        public int stage;
        public int searchDistance = 3;
        public int sequence;
        public bool finalLessonMastered;
        public List<int> successWindow = new();
    }

    private readonly struct Pair
    {
        public readonly Vector2Int Seeker;
        public readonly Vector2Int Hider;

        public Pair(Vector2Int seeker, Vector2Int hider)
        {
            Seeker = seeker;
            Hider = hider;
        }
    }

    private readonly int windowSize;
    private readonly float threshold;
    private readonly int finalSearchDistance;
    private readonly int distanceIncrement;
    private readonly int[] stageDeadlines;
    private readonly string savePath;
    private readonly System.Random random;
    private readonly Dictionary<int, List<Pair>> pairCache = new();
    private Progress progress;

    public Stage CurrentStage => (Stage)progress.stage;
    public int CurrentDistance => CurrentStage == Stage.Capture ? 1 :
        CurrentStage == Stage.Search ? progress.searchDistance : 2;
    public int WindowCount => progress.successWindow.Count;
    public float WindowSuccessRate
    {
        get
        {
            if (progress.successWindow.Count == 0) return 0f;
            int wins = 0;
            foreach (int result in progress.successWindow) wins += result;
            return (float)wins / progress.successWindow.Count;
        }
    }
    public bool FinalLessonMastered => progress.finalLessonMastered;

    public CortexPoliceCurriculum(int windowSize, float threshold,
        int finalSearchDistance, int distanceIncrement, int[] stageDeadlines,
        string savePath, int randomSeed, bool resumeProgress)
    {
        this.windowSize = Mathf.Max(1, windowSize);
        this.threshold = Mathf.Clamp01(threshold);
        this.finalSearchDistance = Mathf.Max(3, finalSearchDistance);
        this.distanceIncrement = Mathf.Max(1, distanceIncrement);
        if (stageDeadlines == null || stageDeadlines.Length != 4)
            throw new ArgumentException("Four stage deadlines are required.", nameof(stageDeadlines));
        this.stageDeadlines = (int[])stageDeadlines.Clone();
        foreach (int deadline in this.stageDeadlines)
            if (deadline < 1)
                throw new ArgumentOutOfRangeException(nameof(stageDeadlines));
        this.savePath = savePath;
        random = new System.Random(randomSeed);
        Load(resumeProgress);
    }

    public EpisodeSpecification CreateInitialEpisode(EnvironmentInstance environment) =>
        CreateEpisode(environment);

    public EpisodeSpecification CreateNextEpisode(EnvironmentInstance environment,
        EpisodeOutcome outcome)
    {
        // Difficulty encodes both stage and distance. Results from arenas that are
        // finishing the previous lesson cannot advance the new lesson.
        if (Academy.Instance.IsCommunicatorOn && !outcome.WasInterrupted &&
            outcome.Difficulty == Difficulty && !progress.finalLessonMastered)
        {
            bool passed = outcome.WasSuccessful &&
                outcome.Steps <= stageDeadlines[progress.stage];
            progress.successWindow.Add(passed ? 1 : 0);
            while (progress.successWindow.Count > windowSize)
                progress.successWindow.RemoveAt(0);

            if (progress.successWindow.Count == windowSize &&
                WindowSuccessRate >= threshold)
            {
                if (CurrentStage != Stage.Search)
                    progress.stage++;
                else if (progress.searchDistance < finalSearchDistance)
                    progress.searchDistance = Mathf.Min(finalSearchDistance,
                        progress.searchDistance + distanceIncrement);
                else
                    progress.finalLessonMastered = true;

                progress.successWindow.Clear();
                Debug.Log($"[Cortex Curriculum] {CurrentStage}, distance {CurrentDistance}" +
                    (progress.finalLessonMastered ? " (final lesson mastered)." : "."));
            }
            Save();
        }
        return CreateEpisode(environment);
    }

    private int Difficulty => progress.stage * 1000 + CurrentDistance;

    private EpisodeSpecification CreateEpisode(EnvironmentInstance environment)
    {
        if (environment == null) throw new ArgumentNullException(nameof(environment));
        if (environment.Seekers.Count != 2 || environment.Hiders.Count != 2)
            throw new InvalidOperationException(
                "CortexPoliceCurriculum requires exactly two seekers and two hiders.");

        ScenarioGrid grid = environment.Grid;
        float viewDistance = Mathf.Min(environment.Seekers[0].ViewDistance,
            environment.Seekers[1].ViewDistance);
        int cacheKey = Difficulty;
        if (!pairCache.TryGetValue(cacheKey, out List<Pair> candidates))
        {
            candidates = BuildPairs(grid, viewDistance);
            pairCache.Add(cacheKey, candidates);
        }
        if (candidates.Count < 2)
            throw new InvalidOperationException(
                $"Cortex1 has no valid {CurrentStage} pairs at distance {CurrentDistance}.");

        for (int attempt = 0; attempt < 8192; attempt++)
        {
            Pair first = candidates[random.Next(candidates.Count)];
            Pair second = candidates[random.Next(candidates.Count)];
            if (!Compatible(grid, first, second, viewDistance)) continue;

            progress.sequence++;
            bool faceHider = CurrentStage <= Stage.Chase;
            bool faceAway = CurrentStage == Stage.Detect;
            return new EpisodeSpecification(progress.sequence, Difficulty,
                new[] { first.Seeker, second.Seeker },
                new[] { first.Hider, second.Hider },
                faceHider, faceAway, CurrentStage >= Stage.Chase,
                stageDeadlines[progress.stage]);
        }

        throw new InvalidOperationException(
            $"No two valid {CurrentStage} pairs at distance {CurrentDistance} " +
            "could be drawn on Cortex1. Check the map or stage settings.");
    }

    private List<Pair> BuildPairs(ScenarioGrid grid, float viewDistance)
    {
        var walkable = new List<Vector2Int>();
        foreach (Vector2Int cell in grid.GetAllCells())
            if (grid.GetCell(cell) != ScenarioGrid.WallCell)
                walkable.Add(cell);

        var candidates = new List<Pair>();
        int maximum = CurrentDistance;
        int minimum = CurrentStage == Stage.Search ? Mathf.Max(3, maximum - 1) : maximum;
        foreach (Vector2Int seeker in walkable)
        {
            int[,] distance = PathDistances(grid, seeker);
            foreach (Vector2Int hider in walkable)
            {
                int steps = distance[hider.x, hider.y];
                if (steps < minimum || steps > maximum) continue;
                bool clear = HasClearGridLine(grid, seeker, hider);
                if (CurrentStage == Stage.Search ? clear : !clear) continue;
                if (CurrentStage != Stage.Search &&
                    Vector3.Distance(grid.CellToWorld(seeker), grid.CellToWorld(hider)) >
                    viewDistance - 0.05f)
                    continue;
                candidates.Add(new Pair(seeker, hider));
            }
        }
        return candidates;
    }

    private bool Compatible(ScenarioGrid grid, Pair first, Pair second,
        float viewDistance)
    {
        if (first.Seeker == second.Seeker || first.Hider == second.Hider ||
            first.Seeker == second.Hider || second.Seeker == first.Hider)
            return false;
        // Keep each pair independent during the first three lessons and avoid
        // a teammate accidentally exposing a Search hider at episode start.
        int separation = Mathf.Abs(first.Seeker.x - second.Seeker.x) +
            Mathf.Abs(first.Seeker.y - second.Seeker.y);
        if (separation < 6) return false;
        if (InitiallyVisible(grid, first.Seeker, second.Hider, viewDistance) ||
            InitiallyVisible(grid, second.Seeker, first.Hider, viewDistance))
            return false;
        return true;
    }

    private static bool InitiallyVisible(ScenarioGrid grid, Vector2Int seeker,
        Vector2Int hider, float viewDistance) =>
        Vector3.Distance(grid.CellToWorld(seeker), grid.CellToWorld(hider)) <=
        viewDistance && HasClearGridLine(grid, seeker, hider);

    private static bool HasClearGridLine(ScenarioGrid grid, Vector2Int start,
        Vector2Int end)
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
        var queue = new Queue<Vector2Int>();
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
            Debug.LogWarning($"[Cortex Curriculum] Could not load progress: {exception.Message}");
        }
        progress ??= new Progress();
        progress.stage = Mathf.Clamp(progress.stage, 0, (int)Stage.Search);
        progress.searchDistance = Mathf.Clamp(progress.searchDistance, 3,
            finalSearchDistance);
        progress.successWindow ??= new List<int>();
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(savePath)) return;
        try { File.WriteAllText(savePath, JsonUtility.ToJson(progress, true)); }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Cortex Curriculum] Could not save progress: {exception.Message}");
        }
    }
}
