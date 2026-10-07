# Ordered police curriculum

Open `Assets/Scenes/TrainingOrderedPoliceScene.unity` and use a fresh ML-Agents
run ID. This is a separate 40-arena, one-seeker/one-hider lesson; it does not
modify or interrupt a run already using `TrainingScene.unity`. The same `Team
Search` behavior, observations, actions, MA-POCA configuration and reward
amounts are used in every stage. Keep one trainer connected throughout the
four stages so its policy weights continue improving.

| Stage index | Skill | Initial state | Hider movement | Mastery deadline |
| --- | --- | --- | --- | --- |
| 0 | Capture | One reachable cell away, seeker facing hider | Stationary | 60 physics steps |
| 1 | Chase → capture | Two reachable cells away, clear line of sight, seeker facing hider | Flees after first sighting | 160 steps |
| 2 | Detect → chase → capture | Two reachable cells away with clear geometry, seeker facing away | Flees after first sighting | 240 steps |
| 3 | Search → detect → chase → capture | Three to eight reachable cells away, a wall blocks the direct line | Flees after first sighting | 512 steps |

All four stages use the 512-step episode cutoff in the scene. The shorter
mastery deadlines prevent a lucky late catch from advancing an early lesson.
A result counts as success only if the hider is caught within its stage limit.
At 80% success among the latest 100 completed episodes at the current stage,
the curriculum advances once and clears that window. Outcomes from arenas
finishing an older stage do not count toward the new stage. Stage 3 remains in
place for continued training and evaluation. Manual restarts and heuristic
episodes played without a Python trainer do not count.
The progress file records stage, sequence and success window. For a new run,
`Resume Curriculum Progress` is off and a unique file is created. To resume
the matching policy at its saved lesson, point to that file and turn resume on.

Reward values never change: +5 group reward per capture, +2.5 individual
reward for the catcher, and a total -2 team time cost over a full 512-step
episode. The training scene uses the existing 52-value vector, 2×21×21 grid,
11 rays and two discrete action branches. A fresh policy is required when
starting this ordered course from scratch. The older 40-value models cannot
be reused. The initial one-seeker lesson is a prerequisite to a later
two-seeker team curriculum; this scene does not claim to train coordination.

`Assets/Scripts/Training/OrderedPoliceCurriculum.cs` owns stage advancement and
spawn constraints. `Assets/Scripts/Agents/ScriptedHiderMotion.cs` makes the
hider flee only after the team first spots it. `Assets/Map Files/OrderedPolice.txt`
contains an open area plus a wall with a route around it. The scene enables
the ordered curriculum in `SimulationController`; the original distance
curriculum remains available in the existing scenes.

In Unity use **Tools > Police Curriculum > Build Training Player** to create
`Builds/OrderedPolice/OrderedPolice.exe`, then train it with a new run ID using
`config/config.yaml` and the same ML-Agents environment as the project.
Watch `Evaluation/CurriculumStage`, `Evaluation/CaptureSuccess`, and
`Evaluation/EpisodePhysicsSteps`. Stage numbers are 0, 1, 2, 3 as above.
