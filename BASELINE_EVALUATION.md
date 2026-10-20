# Cortex police baseline evaluation

Use `Assets/Scenes/TrainingCortexPoliceScene.unity` to measure one exported seeker
model against each lesson. This instrumentation does not change policy observations,
actions, rewards, obstacle sensing, or the ordinary curriculum's advancement rule.

## Run four fixed-stage baselines

1. Select the scene's `SimulationController`. Keep **Use Cortex Police Curriculum**
   enabled and enable **Run Cortex Baseline**.
2. Assign the exported ONNX model to the seeker prefab's Behavior Parameters and
   set Behavior Type to **Inference Only**. The scene pauses and logs an error if
   the model or inference setting is missing.
3. Enter a model/checkpoint name in **Baseline Model Label**. Leave **Baseline
   Completed Episodes** at 100 for the initial comparison. This is 100 pooled
   completed arena episodes across all 40 environments, not 100 per environment.
4. Run once each with **Baseline Stage** set to Capture, Chase, Detect, and Search.
   Start Search at distance 3; repeat at larger distances to test generalization.
   The fixed-stage curriculum does not advance or save curriculum progress.
5. Each run pauses automatically after the requested number of completed episodes.
   The Console prints the full CSV path under `Application.persistentDataPath` /
   `TrainingBaselines`. Copy or rename each CSV before comparing models if needed.

Manual restarts are interruptions and are omitted from the baseline count and CSV.
The same arena map and curriculum spawn generator are used in every run. The random
seed and model label should stay the same when comparing checkpoints. The runs are
not exact paired trials because some Unity runtime randomness and 40 asynchronous
arenas remain; compare rates over many episodes.

## Read the measurements

- **Full capture** means every hider was caught within that lesson's deadline.
- **Captured fraction** distinguishes one capture from a full team success.
- **First sighting step** measures how long the team took to see any hider according
  to the current `CanSee` check. A blank means no hider was seen.
- **Sighting-to-capture steps** averages, over captured hiders that were seen,
  the time from each hider's first team sighting to capture. A blank means no
  sighted hider was captured.
- **Visible physics steps** counts steps when any hider was currently visible.
- **Reacquisitions** counts a hider becoming visible again after at least one
  fully unseen physics step.
- **Unique visited cells** counts cells occupied by either seeker. This is not
  visual coverage or cleared rooms.

The on-screen dashboard shows the current stage, distance, recent full capture
rate, sighting rate, and timing. In ordinary Cortex training it also shows the
shared curriculum's advancement window and target; it displays only outcomes
from the current stage and distance. TensorBoard receives phase-specific metrics
under `Baseline/Capture`, `Baseline/Chase`, `Baseline/Detect`, and `Baseline/Search`.

These measurements describe the current perception behavior. In particular, the
known trigger-obstacle disagreement in `CanSee` remains. A sighting recorded
through an obstacle should be treated as a diagnostic clue, not proof of correct
line of sight. Fix that separately and run a new baseline with a freshly trained
model before making performance claims about the changed observation meaning.
