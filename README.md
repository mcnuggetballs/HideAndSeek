# Hide and Seek

This is the Unity project to open in Unity 6000.4.3f1. The current training scene is `Assets/Scenes/TrainingCortexPoliceScene.unity`. It starts 16 arenas by default, each with two seekers and two hiders, and uses the Cortex1, CortexNorthRooms, and CortexWestRooms training maps. It teaches Capture → Chase → Detect → Search. Search now teaches a frontier inspection, team coverage, and hidden-hider capture in order at each distance. See [Documentation/SEARCH_CURRICULUM_V2.md](Documentation/SEARCH_CURRICULUM_V2.md) for the motivation, warm-start command, and evaluation procedure.

## Where to start

1. Read [TRAINING.md](TRAINING.md) for the standalone, headless CPU/GPU launch commands and benchmark procedure.
2. Read [Documentation/POLICE_EVALUATION_AND_MAP_POOL.md](Documentation/POLICE_EVALUATION_AND_MAP_POOL.md) and [Documentation/PAIRED_SEARCH_LESSON.md](Documentation/PAIRED_SEARCH_LESSON.md) for the current lesson and fixed evaluation cases.
3. Build the player in Unity with **Tools > Police Curriculum > Build Cortex1 Training Player**. Start a new GPU run with `./start_gpu.ps1 -RunId <new_id>` in PowerShell. This uses the local CUDA environment, headless player, and 16 arenas. See `TRAINING.md` for CPU and multi-worker options.

The `cortex_gpu_20261112_01` run completed 20,000,088 steps on 13 November 2026. It reached Search distance 3 but failed every empty-area result in its final 100-episode curriculum window. Its checkpoint remains under `results/cortex_gpu_20261112_01`. Keep that run as the comparison baseline. The revised Search curriculum needs a fresh run ID initialized from that checkpoint; do not resume its v4 progress with this source.

## Code map

| Path | Role |
| --- | --- |
| `Assets/Scripts/Agents/SeekerAgent.cs` | Agent observations, actions, visibility, captures |
| `Assets/Scripts/Sensing/LocalGridSensor.cs` | Four-channel 21 × 21 wall and coverage observation |
| `Assets/Scripts/Training` | Curricula, episode rules, metrics |
| `Assets/Scripts/Environment*` | Arena creation, spawning, episodes |
| `Assets/Scenes` | Training and testing scenes |
| `config` | Trainer YAML and launcher settings |
| `Assets/Editor` | Build commands and validation probes |

Changes to observation shape/order, actions, or reward meaning require a new policy and fixed-case evaluation. Keep the `Assets` `.meta` files with their assets so Unity references remain valid. Generated `Library`, `Temp`, `Builds`, `results`, logs, and Python environments are local artifacts rather than source.
