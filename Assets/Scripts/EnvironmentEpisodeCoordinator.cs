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

    private SimpleMultiAgentGroup group;
    private EpisodeRules rules = new();
    private ITrainingCurriculum curriculum;
    private bool isActive;
    private bool transitionInProgress;
    private int step;
    private float groupReward;

    public int EpisodeNumber { get; private set; }
    public int CurrentStep => step;
    public event Action<EpisodeOutcome> EpisodeFinished;

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

        float timeReward = -rules.totalTimePenalty / rules.maxEnvironmentSteps;
        AddGroupReward(timeReward);

        if (step >= rules.maxEnvironmentSteps)
            FinishEpisode(interrupted: true, reportToCurriculum: true);
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

            // Close the trajectory before any participant is repositioned.
            if (interrupted) group.GroupEpisodeInterrupted();
            else group.EndGroupEpisode();

            EpisodeFinished?.Invoke(outcome);
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
        environment.TeamSightings.ResetEpisode();
        environment.InfluenceMap.ResetEpisode();
        environment.ResetParticipantsToSpawn();
        environment.AssignTargets();
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
