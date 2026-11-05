using System;
using UnityEngine;

/// <summary>One environment-level result reported to training and curriculum systems.</summary>
public readonly struct EpisodeOutcome
{
    public int EnvironmentId { get; }
    public int EpisodeNumber { get; }
    public int Difficulty { get; }
    public bool WasSuccessful { get; }
    public bool WasInterrupted { get; }
    public int HidersCaught { get; }
    public int HiderCount { get; }
    public int Steps { get; }
    public float GroupReward { get; }
    public Vector2Int[] CaptureCells { get; }
    public SearchLessonKind SearchLesson { get; }
    public bool FrontierInspectionPassed { get; }

    public EpisodeOutcome(int environmentId, int episodeNumber, int difficulty,
        bool wasSuccessful, bool wasInterrupted, int hidersCaught, int hiderCount,
        int steps, float groupReward, Vector2Int[] captureCells,
        SearchLessonKind searchLesson = SearchLessonKind.None,
        bool frontierInspectionPassed = false)
    {
        EnvironmentId = environmentId;
        EpisodeNumber = episodeNumber;
        Difficulty = difficulty;
        WasSuccessful = wasSuccessful;
        WasInterrupted = wasInterrupted;
        HidersCaught = hidersCaught;
        HiderCount = hiderCount;
        Steps = steps;
        GroupReward = groupReward;
        CaptureCells = captureCells ?? Array.Empty<Vector2Int>();
        SearchLesson = searchLesson;
        FrontierInspectionPassed = frontierInspectionPassed;
    }
}
