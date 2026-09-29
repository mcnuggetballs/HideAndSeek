# Training fixes and quick chase check

These changes correct concrete perception and curriculum problems. They do not establish that the resulting policy has learned to chase; that requires a fresh training run and evaluation.

## What changed

- **SeekerAgent.cs:** line of sight now aims from the eye to the target collider center, ignores the seeker's own colliders, and respects intervening walls. Relative target/teammate positions use world distances rotated into the seeker's frame, so the prefab's scale of 2 no longer halves those distances. Captured/inactive targets have empty observation slots. Selected-agent gizmos show the custom FOV and sight lines. Inspector diagnostics show movement/turn choices, displacement, and visible target count.
- **TeamSightingMemory.cs / EnvironmentInstance.cs:** capture removes the target's shared sighting. Initial-lesson facing is optional and applied to saved spawn rotations.
- **AdaptiveSpawnCurriculum.cs:** late outcomes from earlier lessons cannot advance the current lesson. Spawn selection retries within the allowed distance band rather than silently falling back to unlimited distances. Impossible placements report an error. Advancement can optionally require captures within a time limit.
- **SimulationController.cs:** new policies start with fresh curriculum progress by default. Each fresh session writes a unique progress file; the Console prints its path. To resume, enable **Resume Curriculum Progress**, set **Curriculum Progress File** to the previous session's actual filename, and resume the matching Python run. Python `--resume` alone does not restore Unity's curriculum.
- **EnvironmentEpisodeCoordinator.cs:** records capture success, episode physics steps, captured fraction, and curriculum distance for training summaries.
- **SeekerPrefab.prefab / TrainingSeekerPrefab.prefab:** ray fan is 90 degrees total, range approximately 10 world units, sphere radius approximately 0.2 world units, and height 0.5 world units after prefab scaling. Training uses a separate prefab without an assigned model.

The vector size remains 40 and action branches remain `[4, 5]`. However, observation meanings and ray geometry changed: **train a fresh policy; do not resume the old 20-million-step policy for this comparison.** Existing ONNX files are retained.

Capture rewards, time penalties, discrete movement/turn controls, and MA-POCA remain unchanged. The first experiment isolates perception and lesson progression before introducing reward shaping or changing the algorithm.

## Start a shorter diagnostic run

1. Open `Assets/Scenes/TrainingChaseScene.unity` in Unity. This has 40 arenas, one seeker and one stationary hider per arena, and a small open map.
2. Activate your existing ML-Agents Python environment in the project directory.
3. Run:

   ```cmd
   python -m mlagents.trainers.learn config/config-quick.yaml --run-id=chase_fix_01
   ```

4. Press Play in Unity after the trainer says it is listening.
5. The first lesson starts the hider one grid step away (5 world units) and points the seeker at it. Advancement requires 95 successful captures out of the most recent 100 episodes, each within 150 physics steps (3 simulated seconds at fixed timestep 0.02). Later lessons increase distance and use random headings.
6. The quick YAML caps training at **1,000,000 agent steps**, saves every **100,000**, summarizes every **10,000**, and reduces `time_horizon` to 128 for shorter rollouts. Other network/PPO settings are unchanged. Steps aggregate across agents; they are not steps per arena.
7. Evaluate checkpoints at 100k, 300k, 500k, and 1m using a single seeker and stationary hider on the same open map. Assign the new model in testing; use the imported `QuickChase.txt` map. Test repeated starts, including different headings and distances. Record capture success and time, rather than watching one episode.

If straightforward visible-target capture remains poor at 300k–500k and the metrics are flat, stop and inspect visible-target count, action choices, displacement, and spawn difficulty. Do not immediately run another 20 million steps. Expand to multiple agents and clutter after the simple task succeeds consistently. GPU acceleration can follow once this baseline works; it changes throughput, not the learning objective.

## Testing and map import

Testing's **Load** button opens a `.txt` scenario picker. Valid files return to editing; press Play to rebuild. Cancellation or malformed files preserve the current map. F7 remains the saved-map shortcut.

Select a runtime seeker to inspect its cyan custom FOV boundary lines and green/red target sight lines in Gizmos. The custom sight check and ML-Agents ray sensor are separate; absence of ray drawings alone is not proof that observations are missing. The grid sensor continues to observe local walls.

## Validation

Unity compiles the changed scripts and imports the new assets in an isolated copy of the project. Synchronous checks cover direct/off-axis/blocked/self-collider visibility, observation dimensions and scale, inactive targets and memory removal, fresh/resumed curriculum, stale lesson outcomes, speed-gated advancement, ray configuration, and the existing scenario-import behavior. A live training run and learned-policy performance are not validated by these checks.
