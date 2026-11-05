using System;
using System.Collections.Generic;
using System.IO;
using Unity.MLAgents;
using UnityEngine;

/// <summary>
/// Two-seeker police lesson on a pool of fixed layouts.
/// Search grows by walkable path distance; seeker starts are paired nearby.
/// The one shared instance pools completed episodes from every training arena.
/// </summary>
public sealed class CortexPoliceCurriculum : ITrainingCurriculum
{
    public enum Stage { Capture, Chase, Detect, Search }

    [Serializable]
    private sealed class Progress
    {
        public int version = 4;
        public int stage;
        public int searchDistance = 3;
        public int sequence;
        public bool finalLessonMastered;
        public List<MapResult> recentResults = new();
    }

    [Serializable]
    private sealed class MapResult
    {
        public string mapId;
        public int passed;
        public int lesson;
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
    private readonly int maximumSeekerSpawnDistance;
    private readonly int[] stageDeadlines;
    private readonly string savePath;
    private readonly bool baselineMode;
    private readonly System.Random random;
    private readonly string[] requiredMapIds;
    private readonly Dictionary<string, List<Pair>> pairCache = new();
    private readonly Dictionary<string, Dictionary<Vector2Int, int[,]>> pathCache = new();
    private Progress progress;

    public Stage CurrentStage => (Stage)progress.stage;
    public int CurrentDistance => CurrentStage == Stage.Capture ? 1 :
        CurrentStage == Stage.Search ? progress.searchDistance : 2;
    public int WindowCount => progress.recentResults.Count;
    public float WindowSuccessRate
    {
        get
        {
            if (progress.recentResults.Count == 0) return 0f;
            int wins = 0;
            foreach (MapResult result in progress.recentResults) wins += result.passed;
            return (float)wins / progress.recentResults.Count;
        }
    }
    public bool FinalLessonMastered => progress.finalLessonMastered;
    public float SuccessThreshold => threshold;
    public int WindowSize => windowSize;
    public bool IsBaseline => baselineMode;
    public string MapWindowStatus
    {
        get
        {
            var parts = new List<string>();
            foreach (string mapId in requiredMapIds)
            {
                int count = 0;
                int wins = 0;
                foreach (MapResult result in progress.recentResults)
                    if (result.mapId == mapId) { count++; wins += result.passed; }
                if (CurrentStage != Stage.Search)
                {
                    parts.Add($"{mapId} {wins}/{count}");
                    continue;
                }
                int emptyCount = 0, emptyWins = 0, occupiedCount = 0, occupiedWins = 0;
                foreach (MapResult result in progress.recentResults)
                {
                    if (result.mapId != mapId) continue;
                    if (result.lesson == (int)SearchLessonKind.EmptyArea)
                    { emptyCount++; emptyWins += result.passed; }
                    else if (result.lesson == (int)SearchLessonKind.OccupiedArea)
                    { occupiedCount++; occupiedWins += result.passed; }
                }
                parts.Add($"{mapId} empty {emptyWins}/{emptyCount}, occupied {occupiedWins}/{occupiedCount}");
            }
            return string.Join("  ", parts);
        }
    }

    public CortexPoliceCurriculum(int windowSize, float threshold,
        int finalSearchDistance, int distanceIncrement, int[] stageDeadlines,
        string savePath, int randomSeed, bool resumeProgress,
        bool baselineMode = false, Stage baselineStage = Stage.Capture,
        int baselineSearchDistance = 3, string[] requiredMapIds = null,
        int maximumSeekerSpawnDistance = 5)
    {
        this.windowSize = Mathf.Max(1, windowSize);
        this.threshold = Mathf.Clamp01(threshold);
        this.finalSearchDistance = Mathf.Max(3, finalSearchDistance);
        this.distanceIncrement = Mathf.Max(1, distanceIncrement);
        this.maximumSeekerSpawnDistance = Mathf.Max(2, maximumSeekerSpawnDistance);
        if (stageDeadlines == null || stageDeadlines.Length != 4)
            throw new ArgumentException("Four stage deadlines are required.", nameof(stageDeadlines));
        this.stageDeadlines = (int[])stageDeadlines.Clone();
        foreach (int deadline in this.stageDeadlines)
            if (deadline < 1)
                throw new ArgumentOutOfRangeException(nameof(stageDeadlines));
        this.savePath = savePath;
        this.baselineMode = baselineMode;
        this.requiredMapIds = requiredMapIds ?? Array.Empty<string>();
        random = new System.Random(randomSeed);
        if (baselineMode)
            progress = new Progress
            {
                stage = (int)baselineStage,
                searchDistance = Mathf.Clamp(baselineSearchDistance, 3, this.finalSearchDistance)
            };
        else
            Load(resumeProgress);
    }

    public EpisodeSpecification CreateInitialEpisode(EnvironmentInstance environment) =>
        CreateEpisode(environment);

    public EpisodeSpecification CreateNextEpisode(EnvironmentInstance environment,
        EpisodeOutcome outcome)
    {
        if (baselineMode) return CreateEpisode(environment);
        // Difficulty encodes both stage and distance. Results from arenas that are
        // finishing the previous lesson cannot advance the new lesson.
        if (Academy.Instance.IsCommunicatorOn && !outcome.WasInterrupted &&
            outcome.Difficulty == Difficulty && !progress.finalLessonMastered)
        {
            bool passed = outcome.Steps <= stageDeadlines[progress.stage] &&
                (CurrentStage == Stage.Search &&
                 outcome.SearchLesson == SearchLessonKind.EmptyArea
                    ? outcome.FrontierInspectionPassed
                    : outcome.WasSuccessful);
            progress.recentResults.Add(new MapResult
            {
                mapId = environment.Grid.LayoutId,
                passed = passed ? 1 : 0,
                lesson = (int)outcome.SearchLesson
            });
            while (progress.recentResults.Count > windowSize)
                progress.recentResults.RemoveAt(0);

            if (WindowCount == windowSize && WindowSuccessRate >= threshold &&
                EachMapPasses() && EachSearchLessonPasses())
            {
                if (CurrentStage != Stage.Search)
                    progress.stage++;
                else if (progress.searchDistance < finalSearchDistance)
                    progress.searchDistance = Mathf.Min(finalSearchDistance,
                        progress.searchDistance + distanceIncrement);
                else
                    progress.finalLessonMastered = true;

                progress.recentResults.Clear();
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
        SearchLessonKind lesson = CurrentStage != Stage.Search
            ? SearchLessonKind.None
            : (progress.sequence & 1) == 0
                ? SearchLessonKind.EmptyArea : SearchLessonKind.OccupiedArea;
        float viewDistance = Mathf.Min(environment.Seekers[0].ViewDistance,
            environment.Seekers[1].ViewDistance);
        string layoutKey = LayoutKey(grid);
        string cacheKey = layoutKey + ":" + Difficulty;
        if (!pairCache.TryGetValue(cacheKey, out List<Pair> candidates))
        {
            candidates = BuildPairs(grid, viewDistance);
            pairCache.Add(cacheKey, candidates);
        }
        if (candidates.Count < 2)
            throw new InvalidOperationException(
                $"{grid.LayoutId} has no valid {CurrentStage} pairs at distance {CurrentDistance}.");

        for (int attempt = 0; attempt < 8192; attempt++)
        {
            Pair first = candidates[random.Next(candidates.Count)];
            Pair second = candidates[random.Next(candidates.Count)];
            if (!Compatible(grid, layoutKey, first, second, viewDistance, lesson)) continue;

            progress.sequence++;
            bool faceHider = CurrentStage <= Stage.Chase;
            bool faceAway = CurrentStage == Stage.Detect;
            return new EpisodeSpecification(progress.sequence, Difficulty,
                new[] { first.Seeker, second.Seeker },
                new[] { first.Hider, second.Hider },
                faceHider, faceAway, CurrentStage >= Stage.Chase,
                stageDeadlines[progress.stage], lesson);
        }

        throw new InvalidOperationException(
            $"No two valid {CurrentStage} pairs at distance {CurrentDistance} " +
            $"could be drawn on {grid.LayoutId}. Check the map or stage settings.");
    }

    private bool EachMapPasses()
    {
        var totals = new Dictionary<string, int>();
        var wins = new Dictionary<string, int>();
        foreach (MapResult result in progress.recentResults)
        {
            totals.TryGetValue(result.mapId, out int count);
            wins.TryGetValue(result.mapId, out int success);
            totals[result.mapId] = count + 1;
            wins[result.mapId] = success + result.passed;
        }
        // A map must contribute enough episodes to make its score meaningful.
        foreach (string mapId in requiredMapIds)
            if (!totals.ContainsKey(mapId)) return false;
        int minimumPerMap = Mathf.Max(1,
            Mathf.Min(20, windowSize / Mathf.Max(1, requiredMapIds.Length)));
        foreach (string mapId in totals.Keys)
            if (totals[mapId] < minimumPerMap ||
                (float)wins[mapId] / totals[mapId] < threshold)
                return false;
        return totals.Count > 0;
    }

    private bool EachSearchLessonPasses()
    {
        if (CurrentStage != Stage.Search) return true;
        int minimumPerCase = Mathf.Max(1,
            Mathf.Min(8, windowSize / Mathf.Max(1, requiredMapIds.Length * 4)));
        foreach (string mapId in requiredMapIds)
            foreach (SearchLessonKind lesson in new[]
                     { SearchLessonKind.EmptyArea, SearchLessonKind.OccupiedArea })
            {
                int count = 0, wins = 0;
                foreach (MapResult result in progress.recentResults)
                    if (result.mapId == mapId && result.lesson == (int)lesson)
                    { count++; wins += result.passed; }
                if (count < minimumPerCase || (float)wins / count < threshold)
                    return false;
            }
        return true;
    }

    private static string LayoutKey(ScenarioGrid grid)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (Vector2Int cell in grid.GetAllCells())
                hash = (hash ^ grid.GetCell(cell)) * 16777619;
            return $"{grid.Width}x{grid.Height}:{hash:X8}";
        }
    }

    private List<Pair> BuildPairs(ScenarioGrid grid, float viewDistance)
    {
        string layoutKey = LayoutKey(grid);
        if (!pathCache.TryGetValue(layoutKey, out Dictionary<Vector2Int, int[,]> paths))
        {
            paths = new Dictionary<Vector2Int, int[,]>();
            pathCache.Add(layoutKey, paths);
        }
        var walkable = new List<Vector2Int>();
        foreach (Vector2Int cell in grid.GetAllCells())
            if (grid.GetCell(cell) != ScenarioGrid.WallCell)
                walkable.Add(cell);

        var candidates = new List<Pair>();
        int maximum = CurrentDistance;
        int minimum = CurrentStage == Stage.Search ? Mathf.Max(3, maximum - 1) : maximum;
        foreach (Vector2Int seeker in walkable)
        {
            if (!paths.TryGetValue(seeker, out int[,] distance))
            {
                distance = PathDistances(grid, seeker);
                paths.Add(seeker, distance);
            }
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

    private bool Compatible(ScenarioGrid grid, string layoutKey, Pair first, Pair second,
        float viewDistance, SearchLessonKind lesson)
    {
        if (first.Seeker == second.Seeker || first.Hider == second.Hider ||
            first.Seeker == second.Hider || second.Seeker == first.Hider)
            return false;
        // Pair the seekers in the same reachable area, within five walking cells.
        int[,] seekerDistances = pathCache[layoutKey][first.Seeker];
        int separation = seekerDistances[second.Seeker.x, second.Seeker.y];
        if (separation < 2 || separation > maximumSeekerSpawnDistance ||
            !HasClearGridLine(grid, first.Seeker, second.Seeker)) return false;
        // The first few steps around either seeker are empty in the empty-area lesson.
        if (lesson == SearchLessonKind.EmptyArea &&
            (seekerDistances[first.Hider.x, first.Hider.y] < 3 ||
             seekerDistances[second.Hider.x, second.Hider.y] < 3 ||
             pathCache[layoutKey][second.Seeker][first.Hider.x, first.Hider.y] < 3 ||
             pathCache[layoutKey][second.Seeker][second.Hider.x, second.Hider.y] < 3))
            return false;
        // Search must still begin without either hider visible to either seeker.
        if (CurrentStage == Stage.Search &&
            (InitiallyVisible(grid, first.Seeker, second.Hider, viewDistance) ||
             InitiallyVisible(grid, second.Seeker, first.Hider, viewDistance)))
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
        if (progress != null && progress.version != 4)
        {
            Debug.LogWarning("[Cortex Curriculum] Saved progress predates the two Search lessons; starting a fresh curriculum.");
            progress = null;
        }
        progress ??= new Progress();
        progress.stage = Mathf.Clamp(progress.stage, 0, (int)Stage.Search);
        progress.searchDistance = Mathf.Clamp(progress.searchDistance, 3,
            finalSearchDistance);
        progress.recentResults ??= new List<MapResult>();
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
