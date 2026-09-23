# Phase 2 training architecture

## Runtime ownership

- `EnvironmentManager` creates, builds, validates and removes environments.
- `EnvironmentInstance` stores the grid, runtime objects, participants, spawn poses and team memory for one environment.
- `EnvironmentEpisodeCoordinator` owns the ML-Agents group, environment steps, rewards, captures, termination and reset.
- `AdaptiveSpawnCurriculum` consumes an `EpisodeOutcome` and returns the next `EpisodeSpecification`. It never creates GameObjects or rebuilds geometry.
- `SeekerAgent` writes observations and applies actions. It does not reset the environment or decide rewards.

## Episode sequence

1. The curriculum selects distinct seeker and hider cells that are connected through the walkable grid.
2. The environment updates participant spawn poses and resets episode-scoped influence and sighting memory.
3. All seekers act as one `SimpleMultiAgentGroup`.
4. Each captured hider is deactivated and the episode continues.
5. Capturing every hider calls `EndGroupEpisode`. Reaching the environment step limit calls `GroupEpisodeInterrupted`.
6. One `EpisodeOutcome` is reported and the next episode is selected.

Manual restart interrupts and resets the group without reporting a curriculum result.

## Police observation contract

The vector observation has 40 values:

- 4: normalized self position and heading
- 20: four teammate slots containing presence, relative position and heading
- 16: four hostile slots containing sighting presence, age and last known relative position

`LocalGridSensorComponent` adds a heading-aligned 21 by 21 one-channel wall observation. The hostile memory is team-shared and episode-scoped. Teammate positions are currently assumed to be continuously shared.

## Action and reward contract

The policy has two discrete branches:

- movement: reverse, stop, walk, run
- rotation: sharp left, left, straight, right, sharp right

Rewards are deliberately small in number:

- a group reward for each hider captured
- an additional individual fraction for the capturing seeker
- a group time-pressure penalty spread across the full environment step limit

There is no hidden-distance, exploration, influence heat or maintain-sight reward.

## Curriculum

All parallel environments share one pooled curriculum because they contribute samples to one policy. The curriculum:

- starts hiders inside a configurable walking-distance band from the police
- advances the maximum distance after a pooled rolling success threshold
- weights repeatedly captured cells down using `1 / (1 + catches)`
- stores progression in `curriculum_progress_v2.json`

The scenario layout and NavMesh remain unchanged between episodes.
