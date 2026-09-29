# Training file map and next experiments

Reviewed against `C:/Users/Duality1/Desktop/Eshlyne/HideAndSeek` on 2026-09-29. The changes made for this request concern testing-map import only. Observation, action, reward, curriculum and training configuration changes below are recommendations, not applied changes.

## How the parts connect

```text
TrainingScene + SimulationController + ScenarioSystem
  -> EnvironmentSpawner / EnvironmentManager
  -> WorldBuilder creates seekers and hiders after the NavMesh is built
  -> EnvironmentInstance records agents, spawn poses and shared sightings
  -> EnvironmentEpisodeCoordinator registers the seeker group and advances episodes

SeekerAgent + LocalGridSensor + ray sensor produce observations
  -> Python MA-POCA trainer configured by config/config.yaml
  -> movement and turning action indices
  -> SeekerAgent executes movement and reports captures
  -> coordinator assigns rewards and ends/interrupts the group episode
  -> AdaptiveSpawnCurriculum selects the next spawn positions
```

The policy is the learned rule for selecting actions. MA-POCA is the training algorithm that updates it. Unity C# defines the world, observations, action meanings and extrinsic rewards. YAML controls the learning algorithm and network. These are separate responsibilities.

## Observations: files and current behavior

| File/component | Responsibility | When it needs changing |
| --- | --- | --- |
| `Assets/Scripts/Agents/SeekerAgent.cs` | `CollectObservations()`, `UpdateTeamSightings()`, `CanSee()`, and `WriteTeammate()` | Target information, visibility, normalization, relative coordinates, observation order/count. |
| `Assets/Scripts/Training/TeamSightingMemory.cs` | Shares timestamped target positions within one environment | Sighting expiration, removing captured targets, memory semantics. |
| `Assets/Scripts/Sensing/LocalGridSensor.cs` | Produces a heading-aligned wall image directly from `ScenarioGrid` | Spatial sampling, represented cell types, sensor image content. |
| `Assets/Scripts/Sensing/LocalGridSensorComponent.cs` | Configures the grid sensor radius and supersampling, receives its environment's grid | Sensor dimensions or sensor creation. |
| `Assets/Prefabs/Seeker/SeekerPrefab.prefab` | Serialized Behavior Parameters, child ray sensor, grid component, perception settings | Observation count, stacking, child sensors, sensor settings. Change through Unity Inspector. |
| `Assets/Scripts/ScenarioGrid.cs` | World/cell conversion and map dimensions | Coordinate-system changes. |
| `Assets/Scripts/EnvironmentManager.cs` | Initializes every seeker and configures its sensor after building agents | Sensor/environment setup. |
| `Assets/Scripts/EnvironmentInstance.cs` | Per-arena team lists and resets | Target lifecycle, shared memory reset, spawn state. |

The vector observation contains exactly 40 values:

- 4 for seeker position relative to grid center and its forward direction.
- Up to 4 teammate slots × 5 values = 20: presence, relative X/Z, relative heading X/Z.
- Up to 4 hider slots × 4 values = 16: sighting present, normalized sighting age, relative X/Z.

Unused slots are padded with zeros. The target position is the last shared sighting, not necessarily its current position. The sighting flag does not explicitly distinguish a target personally visible now from one remembered or seen by a teammate.

The grid sensor uses radius 5 and supersampling 2, giving `(2 × 5 × 2 + 1) = 21` samples per side. It represents walls and boundaries; it does not represent hiders or exploration history. With 5-unit cells, neighboring samples are 2.5 world units apart. Do not assume the influence-map visualization is itself part of the policy observations.

The separate child ray sensor supplies 11 casts × 4 values = 44 values: two detectable tags plus hit information per cast. Its configured length is 45 and sphere-cast radius is 3.15, while the custom sighting check is range 10 and a total 90-degree cone. Actual ray geometry must be checked in world space because the sensor code applies transform scale. The ray mask excludes layer 2 but includes many other colliders, so inspect what each cast actually hits, not only whether it is drawn.

The inspected ONNX expects a 21×21 wall input, 44 ray values, 40 vector values, discrete action masks and 128 recurrent-memory values.

Priority observation recommendations:

1. Recalculate the visibility-ray direction from the actual eye origin to the target point. Currently its origin is offset after computing direction from a different point.
2. Exclude captured/inactive targets from observations and remove their sightings. Currently a captured hider's position can remain observable until memory expires.
3. Explicitly inspect sighting freshness, ray hits and observation values in the stationary-target test.
4. Reconcile the ray and custom-sighting definitions of visibility. They can intentionally differ, but their meanings should be clear.
5. Consider expressing target distance at a useful local scale, with a bearing/distance representation and current-visibility flag. Current relative coordinates are scaled by both `InverseTransformPoint` and map dimensions; world-distance interpretation requires checking the agent transform scale. Changing map dimensions in testing also changes normalization.

Changing observation count, order, sensor names, tag order or dimensions changes the model's input contract. Update the matching prefab configuration and retrain; a shape-compatible model can still be semantically incompatible if observation order/meaning changes. Changing grid dimensions must also remain compatible with the YAML visual encoder: `simple` needs at least 20×20 input in this toolkit.

## Actions and movement: files and current behavior

| File/component | Responsibility |
| --- | --- |
| `Assets/Scripts/Agents/SeekerAgent.cs` | Speed/turn buckets, `OnActionReceived()`, movement execution, heuristic and capture-distance check. |
| `SeekerPrefab.prefab` — Behavior Parameters | Two discrete action branches: `[4, 5]`. Must match code. |
| `SeekerPrefab.prefab` — Decision Requester | New decision every 4 physics steps, repeating actions between decisions. |
| `SeekerPrefab.prefab` — SeekerAgent and NavMeshAgent | Actual serialized speed, turn rate, catch radius and navigation settings. |
| `ProjectSettings/TimeManager.asset` | Physics timestep of 0.02 seconds. |
| `Assets/Scripts/RuntimeNavMeshBuilder.cs` | Builds navigation geometry. |
| `Assets/Scripts/WorldBuilder.cs` | Spawns agents and obstacles on the grid. |
| `Assets/Scripts/EnvironmentInstance.cs` | Resets agents, updates active target lists and deactivates captured hiders. |

Movement indices mean `[-0.5, 0, 0.5, 1] × moveSpeed`; turning indices mean `[-1, -0.5, 0, 0.5, 1] × rotateSpeed`.

The current prefab uses move speed 5 and turn rate 200 degrees/second. One maximum turn held for four 0.02-second ticks rotates approximately 16 degrees. Commands move along the seeker's forward direction through `NavMeshAgent.Move()`; this is not an automatic `SetDestination()` chase controller. The NavMeshAgent component's nominal speed is not the movement magnitude used by this code. The policy must learn steering and movement.

Capture is reported after actions when distance between the agents' `nextPosition` values is within `max(catchDistance, combined NavMeshAgent radii)`. Current catchDistance is 1.5. Capture is not a reward for merely seeing the target or choosing forward.

Before changing the action space, inspect the action indices and actual displacement when deterministic inference gets stuck. A stop command and a blocked forward command have different causes. Consider slower/gentler turning or continuous control only as separately measured experiments. Changing discrete branch sizes or switching to continuous actions requires matching prefab settings and retraining.

## MA-POCA and the learned model

| File/component | Responsibility |
| --- | --- |
| `config/config.yaml` | Selects `poca`, network architecture, optimization, reward discounting and training budgets. |
| `Assets/Scripts/EnvironmentEpisodeCoordinator.cs` | Creates a `SimpleMultiAgentGroup` per arena, registers seekers, supplies team rewards and group episode boundaries. |
| `Assets/Scripts/EnvironmentManager.cs` | Configures and activates each coordinator. |
| `Assets/Scripts/EnvironmentInstance.cs` | Provides the environment's agents and episode state. |
| `SeekerPrefab.prefab` — Behavior Parameters | Behavior name `Team Search`, action/observation contract, ONNX selection and inference mode. |
| Python `mlagents/trainers/poca/trainer.py`, `optimizer_torch.py` and `torch_entities/networks.py` | Installed implementations of trajectory processing, cooperative critic/baseline, actor network and optimization. Normally do not edit these to tune a project. |

MA-POCA uses an actor to select each agent's actions and cooperative value/baseline estimates to assign credit during training. It uses PPO-style clipped policy optimization; `poca` does not mean PPO-related hyperparameters are irrelevant. The trained actor is exported to ONNX for Unity inference; the group critic is used during training.

Hiders currently have an empty `HiderAgent.cs` and are not registered as a learning group. This is seeker cooperation against stationary targets, not trained adversarial self-play.

For training, ensure Behavior Type is `Default` so seekers use the connected Python trainer. The current shared prefab is set to `Inference Only` for testing. Switch it back before training or use distinct training/testing prefab configurations. A static saved prefab setting does not establish what setting was used during an earlier run.

Deterministic Inference is an inference action-selection option. It does not configure Python exploration. Compare both modes in evaluation and record which was used.

MA-POCA is appropriate for the intended cooperative setup. The one-seeker baseline does not require changing algorithms. If doing a separate PPO comparison, account for group-versus-individual reward semantics; changing only `trainer_type` is not a controlled equivalence.

## Rewards, penalties and episode endings

| File | Responsibility | Change here for |
| --- | --- | --- |
| `Assets/Scripts/Training/EpisodeRules.cs` | Default reward and episode-limit fields, validation | Adding new adjustable reward settings. |
| `Assets/Scripts/EnvironmentEpisodeCoordinator.cs` | `Step()`, `ReportCapture()`, `FinishEpisode()` | Actual time penalties, group/individual rewards, termination and future shaping. |
| `Assets/Scenes/TrainingScene.unity` — SimulationController | Serialized rule values actually used in training | Adjust existing reward/timeout values through Inspector. |
| `Assets/Prefabs/Scene/TestingScenePrefab.prefab` and TestingScene overrides | Testing controller settings | Consistent episode/capture evaluation settings. |
| `Assets/Scripts/Agents/SeekerAgent.cs` | Detects capture and reports it | Capture mechanics, not the main reward amounts. |
| `Assets/Scripts/Training/EpisodeOutcome.cs` | Carries success, interruption, capture counts, steps, reward and capture cells | Adding evaluation measurements. |

Current training rules: group capture reward +5, individual capture reward +2.5, full-timeout group cost -2, timeout 2048 physics steps. There are no separate penalties for turning, collisions, reversing or oscillation, and no approach/progress bonus.

The coordinator's 2048-step limit is physics ticks, approximately 40.96 simulated seconds. DecisionPeriod 4 means approximately 512 decisions per full episode. YAML `max_steps` and `time_horizon` are trainer experience units, not this physics timeout.

Capture already favors earlier success through the time cost and discounted returns. The deficiency is not a complete absence of a speed incentive. It is weak guidance for learning a useful approach in a complex task, plus observation concerns that need validation.

Recommended reward experiment: retain capture as the objective; on a simple visible stationary-target lesson, compare existing rewards against modest potential-based progress shaping. Avoid reward for selecting forward or facing the target every tick. Account for discounting, terminal/reset transitions, target changes and visibility changes. Rewarding only decreases while ignoring increases lets oscillation accumulate reward. With obstacles, use meaningful reachable progress rather than straight-line distance into a wall. Do not add a blanket rotation penalty first.

Decide explicitly which quantities are cooperative team rewards and which are personal. `AddGroupReward()` is not equivalent to awarding the same amount through every agent's `AddReward()`.

## Curriculum: separate from YAML

| File/component | Responsibility |
| --- | --- |
| `Assets/Scripts/Training/AdaptiveSpawnCurriculum.cs` | Distance-band sampling, success window, capture-cell weighting, advancement, JSON load/save. |
| `Assets/Scripts/Training/ITrainingCurriculum.cs` | Episode-generation contract. |
| `Assets/Scripts/Training/EpisodeSpecification.cs` | Spawn cells, sequence and declared difficulty. |
| `Assets/Scripts/Training/EpisodeOutcome.cs` | Results supplied to the curriculum. |
| `Assets/Scripts/SimulationController.cs` and TrainingScene Inspector | Enables the curriculum and supplies distance/window/threshold/save-file settings. |
| `Assets/Scripts/EnvironmentEpisodeCoordinator.cs` | Records outcomes and asks for the next episode. |
| `Assets/Scripts/EnvironmentInstance.cs` | Applies spawn cells and random headings. |
| Project-root `curriculum_progress_v2.json` | Persisted stage and sampling history; independent of Python run ID. |
| `Assets/Scripts/ScenarioSystem.cs` and source map | Map obstacles and team counts. |

Current starting distance is 2–4 cells from the nearest seeker, a 20-episode pooled window, 80% success threshold, +1 cell advancement and no rollback. Already-running episodes can belong to earlier stages. The shortage fallback permits the full reachable band, which can silently make an intended easy lesson harder. Successful capture cells are downweighted, changing the sample distribution.

For quick chase experiments, use a fresh curriculum file per experiment and one seeker/one hider on an open map. Verify target visibility and initial heading. Assess results from the current stage, with success and capture time, then add search/obstacles/teams gradually. Keep some easy cases. This requires curriculum changes; shortening YAML alone does not create the simple lesson.

## YAML review

| Setting | Current | Interpretation and recommendation |
| --- | --- | --- |
| `trainer_type` | `poca` | Keep for the cooperative project while diagnosing observations/rewards. |
| `batch_size` / `buffer_size` | 1024 / 10240 | Reasonable 1:10 ratio; no evidence they caused the failure. Keep for initial controlled comparisons. |
| `learning_rate` | 0.0003, linear | Conventional starting rate; reaches zero at max_steps. Do not assume simply resuming a completed run preserves useful learning settings. |
| `beta` | 0.01, linear | Entropy/exploration strength, toward the high end of common settings. Later compare 0.003 after the baseline works; do not treat reducing randomness as a sensing fix. |
| `epsilon` | 0.2, linear | Policy-update clipping threshold; keep initially. |
| `lambd` / `num_epoch` | 0.95 / 3 | Advantage estimation and passes per update; keep initially. |
| `normalize` | true | Running vector normalization. Separate from manual scaling in C#. Compare only after verifying input meanings. |
| `hidden_units` / `num_layers` | 256 / 2 | Actor/network capacity. No demonstrated need for a bigger network. |
| `vis_encode_type` | simple | CNN for the wall-grid input; 21×21 fits its minimum dimensions. |
| memory size / sequence length | 128 / 64 | Recurrent state and training sequence length. At DecisionPeriod 4, 64 decisions span about 5.12 simulated seconds. |
| `gamma` | 0.995 | Discounts future rewards at trainer transition scale. Rough characteristic horizon 200 decisions ≈16 simulated seconds here; not an episode-duration setting. |
| `extrinsic.strength` | 1 | Scales Unity rewards. Actual capture/penalty logic remains in C#. |
| `time_horizon` | 2048 | Per-agent trajectory collection limit. Exceeds roughly 512 decisions per full current episode, so episodes often end first. For the short diagnostic experiment, try 128 as an explicitly recorded change to deliver trajectories sooner. |
| `max_steps` | 20000000 | Shared behavior experience budget across arenas/agents; not proof of task mastery. |
| summary / checkpoint interval | 50000 / 500000 | Too coarse for a very short run. Use 10000 / 100000 for the diagnostic run. |
| keep checkpoints | 5 | Use 10 if you want every 100k checkpoint of a 1m run available. |

Neither Unity's arena count nor the custom curriculum is defined in the current YAML. The 40 arenas are configured in EnvironmentSpawner/TrainingScene. CUDA selection is a separate top-level `torch_settings` section.

## How long to train before testing

Use a **1,000,000-step experiment budget**, exporting and evaluating at **100k, 250k, 500k and 1m**. The 250k evaluation can use a manual stop/export or the nearest available checkpoint. The aim is diagnosis; no step threshold guarantees learning.

- 100k: check that actions, sightings, capture events and rewards behave as intended. A weak policy this early is not proof of failure.
- 250k–500k: look for an improving capture rate and shorter capture times on the simple lesson.
- 1m: if the simple lesson remains unreliable with no improving trend, investigate rather than extending to 20m.
- Extend promising runs to 2–3m, then increase task difficulty. Use another seed to check that an improvement is repeatable.

For this separate diagnostic YAML, change only:

```yaml
# Inside behaviors -> Team Search, keeping the other behavior settings:
max_steps: 1000000
time_horizon: 128
summary_freq: 10000
checkpoint_interval: 100000
keep_checkpoints: 10
```

The reduction in time_horizon is an experiment, not an established cure. Keep the original YAML as the reference. Ensure the simple lesson actually exists before interpreting its learning curve. A completed linear-schedule 1m run has decayed its learning rate to zero; extending it needs explicit schedule planning. Start fresh when action/observation architecture changes, and do not reuse the old stalled policy as the only baseline.

Based on the previous run's approximately 5.5-hour logged interval for 20m steps, 100k/500k/1m are roughly 1.7/8.3/16.5 minutes at similar throughput. Startup, hardware load, episode length and the new lesson can change those times.

Use about 100 fixed evaluation episodes with varied headings/distances, separately from training. Track success, simulated seconds from first sighting to capture, path distance, and movement commands versus actual displacement when stuck. Evaluate both inference modes separately. A practical simple-lesson advancement goal is at least 95% success with capture times near a feasible movement baseline; this is a proposed acceptance criterion, not a toolkit requirement.

## Testing-map import implemented

The existing testing **Load** button now selects a scenario `.txt` file in the Unity Editor or Windows standalone player. The source file is not copied or changed.

Flow: `UIManager` -> `GameEvents.LoadScenarioRequested` -> `SimulationController.ImportScenario()` -> `ScenarioFilePicker` -> `ScenarioStorage.LoadFile()` -> `ScenarioTextFormat.Parse()` -> `EnvironmentManager.ReplaceLayout()` -> editor/camera refresh. Press Play to build the imported geometry, NavMesh and agents.

Files changed:

- `Assets/Scripts/SimulationController.cs`: picker entry point, validated file import, event wiring.
- `Assets/Scripts/ScenarioStorage.cs`: shared file-path loader.
- New `Assets/Scripts/ScenarioFilePicker.cs`: Editor picker and Windows player native dialog.

F6 still saves the named painted scenario; F7 still reloads it. Canceling selection leaves the world unchanged. Malformed or missing files are rejected before replacing the current scenario, with an error in the Console. Imported dimensions are taken from the file; cell size and world origin come from the testing environment. A valid file returns to editing even if the simulation was running. Play rebuilds and resumes it.

The map format uses `X` for walls, `S` for seekers, `H` for hiders and spaces for empty cells. Every row must have the same width, including trailing spaces. Example:

```text
XXXXX
XS HX
X   X
XXXXX
```

The policy supports at most five seekers and four hiders. Both teams must be present before Play; an imported map can still be edited before that validation occurs. This imports layout data, not a Unity `.unity` scene or a trained model.

## References

- [Unity training configuration](https://unity-technologies.github.io/ml-agents/Training-Configuration-File/)
- [Unity MA-POCA overview](https://unity-technologies.github.io/ml-agents/ML-Agents-Overview/)
- [Potential-based shaping research](https://people.eecs.berkeley.edu/~pabbeel/cs287-fa09/readings/NgHaradaRussell-shaping-ICML1999.pdf)

Runtime validation findings and any remaining UI/platform test limits are reported separately with the implementation.
