using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Episode-scoped hostile sightings shared by the police team.</summary>
public sealed class TeamSightingMemory
{
    public readonly struct Sighting
    {
        public Vector3 WorldPosition { get; }
        public int Step { get; }

        public Sighting(Vector3 worldPosition, int step)
        {
            WorldPosition = worldPosition;
            Step = step;
        }
    }

    private readonly Dictionary<NavMeshAgent, Sighting> sightings = new();

    public void Record(NavMeshAgent hider, Vector3 worldPosition, int step)
    {
        if (hider != null)
            sightings[hider] = new Sighting(worldPosition, step);
    }

    public bool TryGet(NavMeshAgent hider, out Sighting sighting)
    {
        if (hider != null)
            return sightings.TryGetValue(hider, out sighting);
        sighting = default;
        return false;
    }

    public void ResetEpisode() => sightings.Clear();
}
