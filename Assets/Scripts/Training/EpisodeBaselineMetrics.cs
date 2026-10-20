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

    public EpisodeBaselineMetrics(EpisodeOutcome outcome, int stepLimit,
        int firstSightingStep, int firstCaptureStep, int visiblePhysicsSteps,
        int reacquisitions, int distinctVisitedCells, int sightedHiders,
        int capturesAfterSighting, int sightingToCaptureStepSum)
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
    }

    public float MeanSightingToCaptureSteps => CapturesAfterSighting > 0
        ? (float)SightingToCaptureStepSum / CapturesAfterSighting : -1f;
}
