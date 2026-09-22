using System;
using UnityEngine;

// Owns one environment's round lifecycle. Agents report events; this class resets the team once.
public sealed class EnvironmentEpisodeCoordinator
{
    private readonly EnvironmentInstance environment;
    private bool isActive;
    private bool transitionInProgress;

    public int EpisodeNumber { get; private set; }

    public EnvironmentEpisodeCoordinator(EnvironmentInstance environment)
    {
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    internal void Activate()
    {
        isActive = true;
        ResetRuntimeState();
    }

    internal void Deactivate()
    {
        isActive = false;
        transitionInProgress = false;
    }

    // Called when ML-Agents starts one agent automatically, including a MaxStep timeout.
    public void OnAgentEpisodeBegin(SeekerAgent agent)
    {
        if (!isActive || transitionInProgress || agent == null)
            return;
        if (!Contains(agent))
            throw new InvalidOperationException($"{agent.name} is not registered with {environment.Root.name}.");

        transitionInProgress = true;
        try
        {
            ResetRuntimeState();
            foreach (SeekerAgent teammate in environment.Seekers)
            {
                if (teammate != null && teammate != agent)
                    teammate.EndEpisode();
            }
            EpisodeNumber++;
        }
        finally
        {
            transitionInProgress = false;
        }
    }

    // Current rule: one capture completes the shared environment episode.
    public void ReportCapture(SeekerAgent capturingAgent)
    {
        if (!isActive || transitionInProgress || capturingAgent == null)
            return;
        if (!Contains(capturingAgent))
            throw new InvalidOperationException($"{capturingAgent.name} is not registered with {environment.Root.name}.");

        EndSharedEpisode();
    }

    public void RestartEpisode()
    {
        if (!isActive || transitionInProgress)
            return;
        EndSharedEpisode();
    }

    private void EndSharedEpisode()
    {
        transitionInProgress = true;
        try
        {
            ResetRuntimeState();
            foreach (SeekerAgent seeker in environment.Seekers)
                if (seeker != null) seeker.EndEpisode();
            EpisodeNumber++;
        }
        finally
        {
            transitionInProgress = false;
        }
    }

    private void ResetRuntimeState()
    {
        environment.ResetParticipantsToSpawn();
        environment.InfluenceMap.ResetEpisode();
        environment.AssignTargets();
    }

    private bool Contains(SeekerAgent agent)
    {
        foreach (SeekerAgent seeker in environment.Seekers)
            if (seeker == agent) return true;
        return false;
    }
}
