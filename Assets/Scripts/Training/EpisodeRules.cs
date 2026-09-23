using System;

[Serializable]
public sealed class EpisodeRules
{
    public int maxEnvironmentSteps = 2048;
    public float totalTimePenalty = 2f;
    public float hiderCaughtGroupReward = 5f;
    public float individualCatchRewardFraction = 0.5f;

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
    }
}
