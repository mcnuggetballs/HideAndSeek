using System;

[Serializable]
public sealed class EpisodeRules
{
    public int maxEnvironmentSteps = 2048;
    public float totalTimePenalty = 2f;
    public float hiderCaughtGroupReward = 5f;
    public float individualCatchRewardFraction = 0.5f;
    // Maximum total group reward from first-time visible floor coverage in a
    // Search episode. Capture remains worth five per hider by default.
    public float searchCoverageRewardBudget = 1f;
    // Personal maximum per seeker for cells that only this seeker reveals.
    public float searchPersonalCoverageRewardBudget = 0.5f;
    // Small one-off glimpses do not produce a coverage reward.
    public int searchMinimumNovelCellsPerDecision = 4;
    // Cells seen by both seekers are still useful, but encourage divided views.
    public float searchSharedViewRewardFraction = 0.25f;

    public void Validate()
    {
        if (maxEnvironmentSteps < 1)
            throw new ArgumentOutOfRangeException(nameof(maxEnvironmentSteps));
        if (totalTimePenalty < 0f)
            throw new ArgumentOutOfRangeException(nameof(totalTimePenalty));
        if (hiderCaughtGroupReward < 0f)
            throw new ArgumentOutOfRangeException(nameof(hiderCaughtGroupReward));
        if (individualCatchRewardFraction < 0f)
            throw new ArgumentOutOfRangeException(nameof(individualCatchRewardFraction));
        if (searchCoverageRewardBudget < 0f)
            throw new ArgumentOutOfRangeException(nameof(searchCoverageRewardBudget));
        if (searchPersonalCoverageRewardBudget < 0f)
            throw new ArgumentOutOfRangeException(nameof(searchPersonalCoverageRewardBudget));
        if (searchMinimumNovelCellsPerDecision < 0)
            throw new ArgumentOutOfRangeException(nameof(searchMinimumNovelCellsPerDecision));
        if (searchSharedViewRewardFraction < 0f || searchSharedViewRewardFraction > 1f)
            throw new ArgumentOutOfRangeException(nameof(searchSharedViewRewardFraction));
    }
}
