using System;

/// <summary>
/// Diagnostic measurements for one completed arena episode. These values never
/// enter the policy observation or reward calculation.
/// </summary>
public readonly struct EpisodeBaselineMetrics
{
    public readonly EpisodeOutcome Outcome;
    public readonly int StepLimit;
    public readonly int FirstSightingStep;
    public readonly int FirstCaptureStep;
    public readonly int VisiblePhysicsSteps;
    public readonly int Reacquisitions;
    public readonly int DistinctVisitedCells;
    public readonly int SightedHiders;
    public readonly int CapturesAfterSighting;
    public readonly int SightingToCaptureStepSum;
    public readonly int CheckedCells;
    public readonly int ReachableCells;
    public readonly string MapId;
    public readonly float[] SeekerTravelDistance;
    public readonly int[] SeekerVisitedCells;
    public readonly int[] SeekerTurnInPlaceSteps;
    public readonly int[] SeekerBlockedForwardDecisions;
    public readonly int[] SeekerCaptures;
    public readonly int[] SeekerExclusiveCheckedCells;
    public readonly int SharedNewCheckedCells;
    public readonly int RewardedNovelCells;
    public readonly int SeekerSpawnSeparationCells;
    public readonly int KnownWalls;
    public readonly int FrontiersInspected;
    public readonly int FirstFrontierInspectionStep;

    public EpisodeBaselineMetrics(EpisodeOutcome outcome, int stepLimit,
        int firstSightingStep, int firstCaptureStep, int visiblePhysicsSteps,
        int reacquisitions, int distinctVisitedCells, int sightedHiders,
        int capturesAfterSighting, int sightingToCaptureStepSum,
        int checkedCells, int reachableCells, string mapId,
        float[] seekerTravelDistance, int[] seekerVisitedCells,
        int[] seekerTurnInPlaceSteps, int[] seekerBlockedForwardDecisions,
        int[] seekerCaptures, int[] seekerExclusiveCheckedCells,
        int sharedNewCheckedCells, int rewardedNovelCells,
        int seekerSpawnSeparationCells, int knownWalls = 0,
        int frontiersInspected = 0, int firstFrontierInspectionStep = -1)
    {
        Outcome = outcome;
        StepLimit = stepLimit;
        FirstSightingStep = firstSightingStep;
        FirstCaptureStep = firstCaptureStep;
        VisiblePhysicsSteps = visiblePhysicsSteps;
        Reacquisitions = reacquisitions;
        DistinctVisitedCells = distinctVisitedCells;
        SightedHiders = sightedHiders;
        CapturesAfterSighting = capturesAfterSighting;
        SightingToCaptureStepSum = sightingToCaptureStepSum;
        CheckedCells = checkedCells;
        ReachableCells = reachableCells;
        MapId = mapId;
        SeekerTravelDistance = seekerTravelDistance;
        SeekerVisitedCells = seekerVisitedCells;
        SeekerTurnInPlaceSteps = seekerTurnInPlaceSteps;
        SeekerBlockedForwardDecisions = seekerBlockedForwardDecisions;
        SeekerCaptures = seekerCaptures;
        SeekerExclusiveCheckedCells = seekerExclusiveCheckedCells;
        SharedNewCheckedCells = sharedNewCheckedCells;
        RewardedNovelCells = rewardedNovelCells;
        SeekerSpawnSeparationCells = seekerSpawnSeparationCells;
        KnownWalls = knownWalls;
        FrontiersInspected = frontiersInspected;
        FirstFrontierInspectionStep = firstFrontierInspectionStep;
    }

    public float MeanSightingToCaptureSteps => CapturesAfterSighting > 0
        ? (float)SightingToCaptureStepSum / CapturesAfterSighting : -1f;

    public float CheckedFraction => ReachableCells > 0
        ? (float)CheckedCells / ReachableCells : 0f;
}
