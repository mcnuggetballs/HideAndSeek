# Police evaluation and map pool

## What changed

`TrainingCortexPoliceScene` still runs the Capture → Chase → Detect → Search curriculum with two seekers and two hiders in each arena. Its fixed Cortex1 map now shares the training batch with `CortexNorthRooms` and `CortexWestRooms`. The maps have the same 33 × 20 cells and 5 world units per cell, so the policy's observation and action sizes stay unchanged. Each arena keeps one layout for its lifetime; the arena's spawn positions and headings change on each episode. This avoids rebuilding the NavMesh every episode.

The curriculum is still shared by all arenas. Advancement requires the most recent 100 completed episodes to reach the configured 80% full-capture threshold **and** each training map in that window to reach 80%. With the default 100-episode window, each map must contribute at least 20 episodes. Manual restarts and episodes from an older stage do not count. Map-specific spawn-pair caches prevent coordinates from one layout being used on another. Existing pre-map-pool progress files are reset if selected for resume; use a fresh run ID to compare this curriculum fairly with older runs.

## Reusable fixed-stage evaluation cases

In `TrainingCortexPoliceScene`, assign the exported model to the seeker prefab's Behavior Parameters and set Behavior Type to **Inference Only**. Set `Run Cortex Baseline` on, `Baseline Completed Episodes` to 100, `Baseline Model Label` to the checkpoint name, and choose one `Baseline Case` preset below. The preset sets map, stage, and distance together. Press Play and wait until the dashboard pauses the simulation. The CSV path is printed in the Console and shown on the dashboard. Change the `Baseline Case` and repeat with the *same* model. Select `Custom` if you want to set Baseline Map Id, Stage, and Search Distance manually. The baseline holds stage, distance, map, hider movement rule, and random seed fixed; it samples valid spawn pairs rather than using one memorized coordinate pair. The 100 rows are completed arena episodes pooled across the parallel arenas, not 100 episodes per arena.

| Case | Baseline Map Id | Baseline Stage | Search Distance | What it diagnoses |
| --- | --- | --- | --- | --- |
| Open pursuit | `EvalOpen` | Chase | irrelevant | Pursuit and capture after immediate sighting, without an interior wall. |
| Obstacle search | `EvalObstacles` | Search | 10 | Searching around simple occluding walls. |
| Familiar rooms | `Cortex1` | Search | 10 | Search on the original training layout. |
| New rooms | `HeldoutOffsetRooms` | Search | 10 | Transfer to a layout excluded from training. |

`CortexNorthRooms` and `CortexWestRooms` can also be selected for fixed-stage evaluations. Capture and Detect can be evaluated on any map in the table; `EvalOpen` cannot evaluate Search because there are no wall-hidden spawn pairs. Keep stage, distance, model, episode count, and arena count the same when comparing checkpoints. A quick 20-episode smoke check is useful for setup, but use 100 or more completed episodes for a stable comparison.

The CSV includes map ID, full capture, first sighting, sighting-to-capture time, checked floor, and per-seeker travel, visited cells, turn-in-place steps, blocked-forward decisions, and captures. `blocked_forward` means the last action commanded full forward motion but the prior decision interval had <0.05 world units of displacement. It suggests a stall; it does not prove the obstacle caused it. `turn_in_place` counts physics ticks with >0.5° rotation and <0.025 world units of movement. These are **diagnostics only** and do not enter observations or rewards. `checked_cells` is team-visible reachable floor, and is not a room-cleared verdict.

Compare the four cases using **full capture rate first**, then first-sighting rate/time, sighting-to-capture time, and the per-seeker movement diagnostics. A strong familiar-room result and weak new-room result supports map memorization. High sighting with low capture points toward pursuit or movement. Low checked floor and high turn-in-place counts suggest search behavior deserves inspection. Do not interpret mean reward as capture percentage.

## Training setup

1. Turn `Run Cortex Baseline` off and leave `Use Cortex Map Pool` on.
2. Set the seeker Behavior Type to **Default** and start the Python trainer with a new run ID, for example `python -m mlagents.trainers.learn config/config.yaml --run-id=hide_seek_maps_v1` from the project root, then press Play in Unity. A connected trainer pauses with an error if the seeker is not on Default.
3. In the dashboard, read the shared stage, total 100-episode window, and per-map wins/counts. In TensorBoard, `Evaluation/Maps/<map>/<stage>/FullCapture` shows each map separately.
4. Export a checkpoint, switch to inference, and run all four evaluation cases on the same model. Compare against an older checkpoint using identical settings.

New maps go into `Assets/Resources/PoliceMaps/Training` or `Evaluation` as 33 × 20 text files using `X` for a wall, spaces for floor, and exactly two `S` and two `H` spawn markers. Training maps are discovered automatically at startup; evaluation maps are selected by their filename without `.txt`. Keep every floor region reachable and verify Capture/Chase/Detect/Search spawn pairs before a long run. A training map added to the pool needs at least one arena and enough completed episodes in the 100-result window for the per-map gate.


## Paired Search lesson

See [PAIRED_SEARCH_LESSON.md](PAIRED_SEARCH_LESSON.md) for the new nearby seeker spawns, complementary coverage reward, and CSV contribution columns.

