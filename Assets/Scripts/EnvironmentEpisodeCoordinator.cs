using System;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Owns one environment's complete episode lifecycle. Agents report events; this
/// coordinator owns timing, rewards, group termination, curriculum results and reset.
/// </summary>
public sealed class EnvironmentEpisodeCoordinator
{
    private readonly EnvironmentInstance environment;
    private readonly HashSet<NavMeshAgent> caughtHiders = new();
    private readonly List<Vector2Int> captureCells = new();
    private readonly List<Transform> seekerTransforms = new();
    private readonly Dictionary<NavMeshAgent, int> firstSightingSteps = new();
    private readonly Dictionary<NavMeshAgent, int> lastSightingSteps = new();
    private readonly HashSet<Vector2Int> visitedCells = new();
    private readonly List<HashSet<Vector2Int>> seekerVisitedCells = new();
    private Vector3[] previousSeekerPositions = Array.Empty<Vector3>();
    private Quaternion[] previousSeekerRotations = Array.Empty<Quaternion>();
    private int[] previousDecisionCounts = Array.Empty<int>();
    private float[] seekerTravelDistance = Array.Empty<float>();
    private int[] seekerTurnInPlaceSteps = Array.Empty<int>();
    private int[] seekerBlockedForwardDecisions = Array.Empty<int>();
    private int[] seekerCaptures = Array.Empty<int>();
    private int[] seekerExclusiveNewCells = Array.Empty<int>();
    private int[] seekerExclusiveCheckedCells = Array.Empty<int>();
    private float[] seekerCoverageRewardGiven = Array.Empty<float>();
    private int sharedNewCheckedCells;
    private int rewardedNovelCells;
    private int seekerSpawnSeparationCells;

    private SimpleMultiAgentGroup group;
    private EpisodeRules rules = new();
    private ITrainingCurriculum curriculum;
    private bool isActive;
    private bool transitionInProgress;
    private int step;
    private float groupReward;
    private int firstSightingStep = -1;
    private int firstCaptureStep = -1;
    private int visiblePhysicsSteps;
    private int reacquisitions;
    private int capturesAfterSighting;
    private int sightingToCaptureStepSum;

    public int EpisodeNumber { get; private set; }
    public int CurrentStep => step;
    public event Action<EpisodeOutcome> EpisodeFinished;
    public event Action<EpisodeBaselineMetrics> EpisodeMeasured;

    public EnvironmentEpisodeCoordinator(EnvironmentInstance environment)
    {
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    internal void Configure(EpisodeRules episodeRules, ITrainingCurriculum trainingCurriculum)
    {
        rules = episodeRules ?? throw new ArgumentNullException(nameof(episodeRules));
        rules.Validate();
        curriculum = trainingCurriculum;
    }

    internal void Activate()
    {
        group?.Dispose();
        group = new SimpleMultiAgentGroup();
        foreach (SeekerAgent seeker in environment.Seekers)
            if (seeker != null)
                group.RegisterAgent(seeker);

        if (curriculum != null)
            environment.ApplyEpisodeSpecification(curriculum.CreateInitialEpisode(environment));

        isActive = true;
        BeginRuntimeEpisode();
    }

    internal void Deactivate()
    {
        isActive = false;
        transitionInProgress = false;
        group?.Dispose();
        group = null;
    }

    // ML-Agents invokes this for each member after a group boundary. Environment state
    // is deliberately not reset here; the coordinator pushes one reset after the group closes.
    public void OnAgentEpisodeBegin(SeekerAgent agent)
    {
        if (agent != null && !Contains(agent))
            throw new InvalidOperationException(
                $"{agent.name} is not registered with {environment.Root.name}.");
    }

    public void Step()
    {
        if (!isActive || transitionInProgress) return;

        step++;
        environment.GetSeekerTransforms(seekerTransforms);
        environment.InfluenceMap.UpdateAgentPositions(seekerTransforms);
        foreach (SeekerAgent seeker in environment.Seekers)
            seeker?.UpdateTeamSightings();

        // Four physics ticks match the decision interval. The team view is
        // sampled atomically so overlapping rays cannot give the first seeker
        // an arbitrary personal credit.
        if (step % 4 == 0)
        {
            int newlyChecked = environment.InfluenceMap.MarkTeamVisibleCells(
                environment.Seekers, step, seekerExclusiveNewCells,
                out int sharedNewCells);
            sharedNewCheckedCells += sharedNewCells;
            for (int i = 0; i < seekerExclusiveNewCells.Length; i++)
                seekerExclusiveCheckedCells[i] += seekerExclusiveNewCells[i];
            if (newlyChecked >= Mathf.Max(1, rules.searchMinimumNovelCellsPerDecision) &&
                (rules.searchCoverageRewardBudget > 0f ||
                 rules.searchPersonalCoverageRewardBudget > 0f) &&
                curriculum is CortexPoliceCurriculum &&
                environment.CurrentEpisode != null &&
                environment.CurrentEpisode.Difficulty / 1000 ==
                (int)CortexPoliceCurriculum.Stage.Search)
            {
                bool targetKnown = false;
                foreach (SeekerAgent seeker in environment.Seekers)
                    if (seeker != null && seeker.HasKnownActiveHider())
                    {
                        targetKnown = true;
                        break;
                    }
                if (!targetKnown)
                {
                    int reachable = Mathf.Max(1, environment.InfluenceMap.ReachableCellCount);
                    float weightedCells = newlyChecked - sharedNewCells +
                        rules.searchSharedViewRewardFraction * sharedNewCells;
                    if (rules.searchCoverageRewardBudget > 0f)
                        AddGroupReward(rules.searchCoverageRewardBudget * weightedCells / reachable);
                    float fairShare = (float)reachable /
                        Mathf.Max(1, environment.Seekers.Count);
                    for (int i = 0; i < seekerExclusiveNewCells.Length; i++)
                    {
                        SeekerAgent seeker = environment.Seekers[i];
                        if (seeker == null || seekerExclusiveNewCells[i] == 0) continue;
                        float remaining = rules.searchPersonalCoverageRewardBudget -
                            seekerCoverageRewardGiven[i];
                        float personal = Mathf.Min(remaining,
                            rules.searchPersonalCoverageRewardBudget *
                            seekerExclusiveNewCells[i] / fairShare);
                        if (personal <= 0f) continue;
                        seeker.AddReward(personal);
                        seekerCoverageRewardGiven[i] += personal;
                    }
                    rewardedNovelCells += newlyChecked;
                }
            }
        }

        // "Visited" means occupied by a seeker, not visually cleared.
        for (int i = 0; i < environment.Seekers.Count; i++)
        {
            SeekerAgent seeker = environment.Seekers[i];
            if (seeker == null) continue;
            Vector3 position = seeker.transform.position;
            float movement = Vector3.Distance(position, previousSeekerPositions[i]);
            seekerTravelDistance[i] += movement;
            float rotation = Quaternion.Angle(seeker.transform.rotation, previousSeekerRotations[i]);
            if (movement < 0.025f && rotation > 0.5f) seekerTurnInPlaceSteps[i]++;
            if (seeker.DecisionCount != previousDecisionCounts[i])
            {
                if (seeker.DecisionCount > 1 && seeker.LastMoveAction == 3 &&
                    seeker.DisplacementSinceLastAction < 0.05f)
                    seekerBlockedForwardDecisions[i]++;
                previousDecisionCounts[i] = seeker.DecisionCount;
            }
            previousSeekerPositions[i] = position;
            previousSeekerRotations[i] = seeker.transform.rotation;
            Vector2Int cell = environment.Grid.WorldToCell(position);
            visitedCells.Add(cell);
            seekerVisitedCells[i].Add(cell);
        }
        bool anyVisible = false;
        foreach (NavMeshAgent hider in environment.Hiders)
        {
            if (hider == null || !hider.gameObject.activeInHierarchy ||
                !environment.TeamSightings.TryGet(hider, out TeamSightingMemory.Sighting sighting) ||
                sighting.Step != step) continue;
            anyVisible = true;
            if (!firstSightingSteps.ContainsKey(hider))
            {
                firstSightingSteps.Add(hider, step);
                if (firstSightingStep < 0) firstSightingStep = step;
            }
            else if (lastSightingSteps.TryGetValue(hider, out int previous) &&
                     step - previous > 1)
                reacquisitions++;
            lastSightingSteps[hider] = step;
        }
        if (anyVisible) visiblePhysicsSteps++;

        float timeReward = -rules.totalTimePenalty / rules.maxEnvironmentSteps;
        AddGroupReward(timeReward);

        // Reaching the deadline completes a failed attempt. Reserve interruption for
        // manual restarts, which must not be scored as policy failures.
        int episodeLimit = environment.CurrentEpisode?.StepLimit > 0
            ? Mathf.Min(rules.maxEnvironmentSteps, environment.CurrentEpisode.StepLimit)
            : rules.maxEnvironmentSteps;
        if (step >= episodeLimit)
            FinishEpisode(interrupted: false, reportToCurriculum: true);
    }

    public void ReportCapture(SeekerAgent capturingAgent, NavMeshAgent hider)
    {
        if (!isActive || transitionInProgress || capturingAgent == null || hider == null)
            return;
        if (!Contains(capturingAgent))
            throw new InvalidOperationException(
                $"{capturingAgent.name} is not registered with {environment.Root.name}.");
        if (caughtHiders.Contains(hider)) return;

        Vector2Int captureCell = environment.Grid.WorldToCell(hider.transform.position);
        if (!environment.CaptureHider(hider)) return;

        caughtHiders.Add(hider);
        for (int i = 0; i < environment.Seekers.Count; i++)
            if (environment.Seekers[i] == capturingAgent) seekerCaptures[i]++;
        captureCells.Add(captureCell);
        if (firstCaptureStep < 0) firstCaptureStep = step;
        if (firstSightingSteps.TryGetValue(hider, out int seenAt))
        {
            capturesAfterSighting++;
            sightingToCaptureStepSum += Mathf.Max(0, step - seenAt);
        }
        AddGroupReward(rules.hiderCaughtGroupReward);
        capturingAgent.AddReward(
            rules.hiderCaughtGroupReward * rules.individualCatchRewardFraction);

        if (caughtHiders.Count >= environment.Hiders.Count)
            FinishEpisode(interrupted: false, reportToCurriculum: true);
    }

    public void RestartEpisode()
    {
        if (!isActive || transitionInProgress) return;
        FinishEpisode(interrupted: true, reportToCurriculum: false);
    }

    private void FinishEpisode(bool interrupted, bool reportToCurriculum)
    {
        transitionInProgress = true;
        try
        {
            bool success = caughtHiders.Count >= environment.Hiders.Count;
            EpisodeOutcome outcome = new(
                environment.EnvironmentId,
                EpisodeNumber,
                environment.CurrentEpisode?.Difficulty ?? 0,
                success,
                interrupted,
                caughtHiders.Count,
                environment.Hiders.Count,
                step,
                groupReward,
                captureCells.ToArray());
            EpisodeBaselineMetrics measurements = new(outcome,
                environment.CurrentEpisode?.StepLimit > 0
                    ? Mathf.Min(rules.maxEnvironmentSteps, environment.CurrentEpisode.StepLimit)
                    : rules.maxEnvironmentSteps,
                firstSightingStep, firstCaptureStep, visiblePhysicsSteps,
                reacquisitions, visitedCells.Count, firstSightingSteps.Count,
                capturesAfterSighting, sightingToCaptureStepSum,
                environment.InfluenceMap.CheckedCellCount,
                environment.InfluenceMap.ReachableCellCount,
                environment.Grid.LayoutId, (float[])seekerTravelDistance.Clone(),
                SeekerVisitCounts(), (int[])seekerTurnInPlaceSteps.Clone(),
                (int[])seekerBlockedForwardDecisions.Clone(),
                (int[])seekerCaptures.Clone(),
                (int[])seekerExclusiveCheckedCells.Clone(),
                sharedNewCheckedCells, rewardedNovelCells,
                seekerSpawnSeparationCells);

            if (reportToCurriculum)
            {
                Academy.Instance.StatsRecorder.Add("Evaluation/CaptureSuccess", success ? 1f : 0f);
                Academy.Instance.StatsRecorder.Add("Evaluation/EpisodePhysicsSteps", step);
                Academy.Instance.StatsRecorder.Add("Evaluation/CapturedFraction",
                    (float)caughtHiders.Count / Mathf.Max(1, environment.Hiders.Count));
                if (curriculum is CortexPoliceCurriculum)
                {
                    string phase = ((CortexPoliceCurriculum.Stage)
                        (outcome.Difficulty / 1000)).ToString();
                    string prefix = $"Baseline/{phase}/";
                    Academy.Instance.StatsRecorder.Add(prefix + "FullCapture", success ? 1f : 0f);
                    Academy.Instance.StatsRecorder.Add(prefix + "CapturedFraction",
                        (float)caughtHiders.Count / Mathf.Max(1, environment.Hiders.Count));
                    Academy.Instance.StatsRecorder.Add(prefix + "EpisodePhysicsSteps", step);
                    Academy.Instance.StatsRecorder.Add(prefix + "FirstSightingRate",
                        firstSightingStep >= 0 ? 1f : 0f);
                    if (firstSightingStep >= 0)
                        Academy.Instance.StatsRecorder.Add(prefix + "FirstSightingSteps", firstSightingStep);
                    if (capturesAfterSighting > 0)
                        Academy.Instance.StatsRecorder.Add(prefix + "SightingToCaptureSteps",
                            measurements.MeanSightingToCaptureSteps);
                    Academy.Instance.StatsRecorder.Add(prefix + "Reacquisitions", reacquisitions);
                    Academy.Instance.StatsRecorder.Add(prefix + "VisitedCells", visitedCells.Count);
                    Academy.Instance.StatsRecorder.Add(prefix + "CheckedFloorFraction",
                        measurements.CheckedFraction);
                    Academy.Instance.StatsRecorder.Add(prefix + "SharedNewCheckedCells",
                        sharedNewCheckedCells);
                    Academy.Instance.StatsRecorder.Add(prefix + "RewardedNovelCells",
                        rewardedNovelCells);
                    for (int i = 0; i < seekerExclusiveCheckedCells.Length; i++)
                        Academy.Instance.StatsRecorder.Add(prefix +
                            $"Seeker{i}ExclusiveCheckedCells", seekerExclusiveCheckedCells[i]);
                    string mapPrefix = $"Evaluation/Maps/{environment.Grid.LayoutId}/{phase}/";
                    Academy.Instance.StatsRecorder.Add(mapPrefix + "FullCapture", success ? 1f : 0f);
                    Academy.Instance.StatsRecorder.Add(mapPrefix + "CheckedFloorFraction",
                        measurements.CheckedFraction);
                    Academy.Instance.StatsRecorder.Add(mapPrefix + "FirstSightingRate",
                        firstSightingStep >= 0 ? 1f : 0f);
                }
                if (curriculum is CortexPoliceCurriculum cortex)
                {
                    Academy.Instance.StatsRecorder.Add("Evaluation/CurriculumStage",
                        (float)cortex.CurrentStage);
                    Academy.Instance.StatsRecorder.Add("Evaluation/CurriculumDistance",
                        cortex.CurrentDistance);
                    Academy.Instance.StatsRecorder.Add("Evaluation/StageWindowSuccessRate",
                        cortex.WindowSuccessRate);
                    Academy.Instance.StatsRecorder.Add("Evaluation/StageWindowCount",
                        cortex.WindowCount);
                    Academy.Instance.StatsRecorder.Add("Evaluation/FinalLessonMastered",
                        cortex.FinalLessonMastered ? 1f : 0f);
                }
                else
                {
                    Academy.Instance.StatsRecorder.Add(
                        curriculum is OrderedPoliceCurriculum
                            ? "Evaluation/CurriculumStage" : "Evaluation/CurriculumDistance",
                        outcome.Difficulty);
                }
            }

            // Close the trajectory before any participant is repositioned.
            if (interrupted) group.GroupEpisodeInterrupted();
            else group.EndGroupEpisode();

            EpisodeFinished?.Invoke(outcome);
            EpisodeMeasured?.Invoke(measurements);
            if (reportToCurriculum && curriculum != null)
                environment.ApplyEpisodeSpecification(
                    curriculum.CreateNextEpisode(environment, outcome));

            EpisodeNumber++;
            BeginRuntimeEpisode();
        }
        finally
        {
            transitionInProgress = false;
        }
    }

    private void BeginRuntimeEpisode()
    {
        step = 0;
        groupReward = 0f;
        caughtHiders.Clear();
        captureCells.Clear();
        firstSightingSteps.Clear();
        lastSightingSteps.Clear();
        visitedCells.Clear();
        int seekerCount = environment.Seekers.Count;
        previousSeekerPositions = new Vector3[seekerCount];
        previousSeekerRotations = new Quaternion[seekerCount];
        previousDecisionCounts = new int[seekerCount];
        seekerTravelDistance = new float[seekerCount];
        seekerTurnInPlaceSteps = new int[seekerCount];
        seekerBlockedForwardDecisions = new int[seekerCount];
        seekerCaptures = new int[seekerCount];
        seekerExclusiveNewCells = new int[seekerCount];
        seekerExclusiveCheckedCells = new int[seekerCount];
        seekerCoverageRewardGiven = new float[seekerCount];
        sharedNewCheckedCells = 0;
        rewardedNovelCells = 0;
        seekerSpawnSeparationCells = 0;
        seekerVisitedCells.Clear();
        for (int i = 0; i < seekerCount; i++) seekerVisitedCells.Add(new HashSet<Vector2Int>());
        firstSightingStep = -1;
        firstCaptureStep = -1;
        visiblePhysicsSteps = 0;
        reacquisitions = 0;
        capturesAfterSighting = 0;
        sightingToCaptureStepSum = 0;
        environment.TeamSightings.ResetEpisode();
        environment.InfluenceMap.ResetEpisode();
        environment.ResetParticipantsToSpawn();
        environment.AssignTargets();
        environment.InfluenceMap.SetEpisodeSeekers(environment.Seekers);
        // Initial visibility is known to the policy, but grants no free reward.
        foreach (SeekerAgent seeker in environment.Seekers)
            if (seeker != null) seeker.MarkVisibleCoverage();
        for (int i = 0; i < environment.Seekers.Count; i++)
        {
            SeekerAgent seeker = environment.Seekers[i];
            if (seeker == null) continue;
            previousSeekerPositions[i] = seeker.transform.position;
            previousSeekerRotations[i] = seeker.transform.rotation;
            Vector2Int cell = environment.Grid.WorldToCell(seeker.transform.position);
            visitedCells.Add(cell);
            seekerVisitedCells[i].Add(cell);
        }
        if (environment.Seekers.Count == 2 &&
            environment.Seekers[0] != null && environment.Seekers[1] != null)
        {
            Vector2Int a = environment.Grid.WorldToCell(
                environment.Seekers[0].transform.position);
            Vector2Int b = environment.Grid.WorldToCell(
                environment.Seekers[1].transform.position);
            seekerSpawnSeparationCells = Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }

    private int[] SeekerVisitCounts()
    {
        int[] counts = new int[seekerVisitedCells.Count];
        for (int i = 0; i < counts.Length; i++) counts[i] = seekerVisitedCells[i].Count;
        return counts;
    }

    private void AddGroupReward(float reward)
    {
        group?.AddGroupReward(reward);
        groupReward += reward;
    }

    private bool Contains(SeekerAgent agent)
    {
        foreach (SeekerAgent seeker in environment.Seekers)
            if (seeker == agent) return true;
        return false;
    }
}
