using System;
using UnityEngine;

/// <summary>
/// Immutable data describing where the already-built participants should start an episode.
/// Geometry remains owned by ScenarioGrid and EnvironmentManager.
/// </summary>
public sealed class EpisodeSpecification
{
    public int Sequence { get; }
    public int Difficulty { get; }
    public Vector2Int[] SeekerSpawnCells { get; }
    public Vector2Int[] HiderSpawnCells { get; }

    public EpisodeSpecification(int sequence, int difficulty,
        Vector2Int[] seekerSpawnCells, Vector2Int[] hiderSpawnCells)
    {
        Sequence = sequence;
        Difficulty = difficulty;
        SeekerSpawnCells = seekerSpawnCells ?? throw new ArgumentNullException(nameof(seekerSpawnCells));
        HiderSpawnCells = hiderSpawnCells ?? throw new ArgumentNullException(nameof(hiderSpawnCells));
    }
}
