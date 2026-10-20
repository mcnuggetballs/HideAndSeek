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

        // "Visited" means occupied by a seeker, not visually cleared.
        foreach (SeekerAgent seeker in environment.Seekers)
            if (seeker != null)
                visitedCells.Add(environment.Grid.WorldToCell(seeker.transform.position));
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
                capturesAfterSighting, sightingToCaptureStepSum);

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
        foreach (SeekerAgent seeker in environment.Seekers)
            if (seeker != null)
                visitedCells.Add(environment.Grid.WorldToCell(seeker.transform.position));
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
