# Cortex1 combined police curriculum

Open `Assets/Scenes/TrainingCortexPoliceScene.unity` for the combined lesson. It uses the existing Cortex1 33 x 20 grid, 40 parallel arenas, two seekers and two hiders per arena. Do not select `TrainingScene` or `TrainingOrderedPoliceScene` if you intend to train this curriculum.

## Lesson order

| Stage | Initial state | Hider | Episode deadline |
| --- | --- | --- | ---: |
| Capture | Each seeker faces a hider one walkable cell away with a clear sight line. | Stationary | 512 physics steps |
| Chase | Each seeker faces a hider two walkable cells away, within the seeker's actual sight range. | Flees after its first team sighting | 768 physics steps |
| Detect | Same visible-range placement, but each seeker faces away from its nearest hider. | Flees after its first team sighting | 1024 physics steps |
| Search | Each hider is behind a wall but reachable along a walkable path. Seeker headings are random. | Flees after its first team sighting | 2048 physics steps |

Search starts at a maximum path distance of 3 cells and increases by 1 up to 12. Hiders are drawn from a band of the current maximum or one cell less. These are *walkable path* distances, not straight-line distances. Capture/Chase/Detect keep their near-range distance because an initially visible target cannot be placed arbitrarily far away with the present 10-unit sight range.

A single `CortexPoliceCurriculum` is shared by all arenas. Full capture of both hiders within the stage deadline is a pass. Partial capture is a failed attempt for advancement, although capture reward is still given. Manual restarts are excluded. After 100 completed attempts at the current stage/distance, 80 passes advance to the next lesson; the window resets. Results from arenas started under an older stage or distance are ignored for the new window. The scene defaults to fresh curriculum progress on each start, with a uniquely named progress JSON file.

The existing observation shape, action branches, reward amounts, and seeker prefab are unchanged. In particular, this curriculum does not add an explored-cell observation or coverage reward. The seeker sight test already uses its forward field of view, distance and physics occlusion. The curriculum uses grid line checks and initial headings to create visible, facing-away, or wall-hidden starting conditions. A new policy training run is recommended because the lesson and hider motion distribution are different, even though the observation dimensions are compatible with old models.

For training, build this scene with `Tools > Police Curriculum > Build Cortex1 Training Player`, then point `mlagents-learn` at `Builds/CortexPolice/CortexPolice.exe` with a **new run ID**. The training behavior name and POCA configuration remain the same as the current Team Search setup. To alter progression, select `SimulationController` in the scene and edit the `Cortex Police Curriculum` fields: window size, success threshold, final search distance, distance increment and stage deadlines. `Team Episode Rules` supplies the Search cutoff and reward amounts. Keep Capture/Chase/Detect deadlines at or below the Search cutoff.

Watch `Evaluation/CurriculumStage` (0 Capture, 1 Chase, 2 Detect, 3 Search), `Evaluation/CurriculumDistance`, `Evaluation/StageWindowSuccessRate`, `Evaluation/StageWindowCount`, `Evaluation/CaptureSuccess` and `Evaluation/CapturedFraction` in TensorBoard. The curriculum stats are pooled over arenas and report the state before the just-completed result is added, so they can lag by one result. Reward alone cannot establish mastery. A stage that stays below 80% is not advancing; inspect full capture and partial capture separately.

This scene tests searching behind walls at increasing path distances. It does **not** yet teach systematic coverage of every room. The policy has no team coverage observation or new-cell incentive, so a search plateau is possible. Validate actual in-Unity spawning, NavMesh motion and first-frame visibility before treating a long training run as evidence that the curriculum works.
