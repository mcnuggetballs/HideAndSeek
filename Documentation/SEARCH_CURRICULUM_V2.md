# Search curriculum v2: train and evaluate exploration

## Why this revision exists

The `cortex_gpu_20261112_01` policy finished 20,000,088 steps on 13 November
2026. Its last 100 saved curriculum results were 48/49 occupied-area passes
and 0/51 empty-area passes. It remained at Search distance 3. The final
TensorBoard window showed roughly 99% full capture on those easy Search
spawns, about 24% reachable floor checked, and roughly 2% empty-area frontier
passes. In TestingScene, the same policy took a long time on a two-obstacle
layout and stayed in the first room on Cortex1. These observations support a
specific diagnosis: nearby capture was learned, but moving through unknown
space was not. They do not by themselves prove whether the policy, sensor, or
movement actions are the limiting factor on every map.

The old Search curriculum alternated an empty-area pass gate with nearby
2v2 capture. A capture paid +5 group reward per hider and ended the episode;
the first qualified frontier inspection paid +0.25. The policy could maximize
training reward while failing the empty-area advancement gate.

## What changed

Search distance 3 through 12 now has three ordered lessons at each distance:

| Lesson | Pass condition | Episode end | Main reward |
| --- | --- | --- | --- |
| Frontier | Either seeker travels at least one cell to a frontier and reveals at least two new cells in one decision interval | First qualifying inspection or 1,024 physics steps | +5 group reward on success |
| Team coverage (`EmptyArea` in the code/CSV) | Frontier inspection plus each seeker revealing at least four exclusive cells by step 1,024 | Both conditions met or 1,024 steps | +5 group reward on success |
| Occupied area | Capture both hidden hiders | Both captured or normal Search limit | Existing +5 group reward per capture |

The existing first-frontier +0.25 reward and personal exclusive-coverage
rewards remain. Novel-floor group reward is three times the original budget
during the first two lessons. Captures in those lessons give no capture reward
and do not end the episode, so the exploration objective gets training time.
The two hiders are still present and active; their spawn constraints keep them
out of the seekers' first few walking cells. Hider behavior, observations and
actions have not changed. Each lesson and each training map must reach the
scene's 80% threshold over the 100-result shared window before advancement.
Episodes started under an older lesson cannot advance the new one.

This is a training design to test, not a claim that frontier navigation is
solved. A first frontier inspection may reveal only a small area; the fixed
evaluation below checks whether it transfers to room-to-room search.

## Implementation check (13 November 2026)

Unity 6000.4.3f1 built the revised Cortex player with zero build errors. A
disposable GPU run, `cortex_search_v2_smoke_01`, initialized from the
20-million-step `.pt` checkpoint, saved ONNX models, and advanced through
Capture, Chase, Detect, Frontier, Team coverage, and Occupied area. It reached
Frontier at Search distance 4 by 200,158 trainer steps. This verifies the
launcher, progress format, and first complete Search lesson cycle. It is not
a fixed-case performance result.

At the end of that smoke run, distance-4 Frontier had 82/100 passes overall:
Cortex1 30/37, CortexNorthRooms 31/36, CortexWestRooms 21/27. The latter is
below the 80% per-map gate, so the curriculum correctly stayed at distance 4.
Do not treat the disposable smoke model as the finished search policy.

## Starting from the 20-million-step policy

This revision changes reward meaning and curriculum progress format (v5).
Keep `cortex_gpu_20261112_01` and its v4 progress file untouched. Train a new
run initialized from its `.pt` checkpoint. `--initialize-from` resets the step
count and starts fresh curriculum progress; `-Resume` continues a matching new
run and its progress. The ONNX file is for Unity inference, not trainer
initialization. The observation and action shapes are unchanged, so the saved
weights are compatible. Build a fresh player with **Tools > Police Curriculum
> Build Cortex1 Training Player** in Unity 6000.4.3f1, then run from this
project folder:

```powershell
.\start_gpu.ps1 -RunId cortex_search_v2_01 -InitializeFrom cortex_gpu_20261112_01
```

Use the same 1 worker × 16 arenas as the source run for the first comparison.
The Cortex training scene references `SeekerFrontierTraining.prefab`; its
Behavior Type must be **Default** when building the player. An assigned ONNX
model may remain on that prefab because the connected trainer supplies actions.
After a graceful stop, continue this *new* run with:

```powershell
.\start_gpu.ps1 -RunId cortex_search_v2_01 -Resume
```

If working in a separate Git worktree, the ignored `results` directory is not
copied automatically. Train in the original project after bringing these
source changes there, or place the source run's `Team Search/checkpoint.pt`
under the corresponding `results/cortex_gpu_20261112_01/Team Search/` path
in the worktree before initializing.

## Evaluation checkpoints and decisions

1. Before the run, save the old 20-million-step ONNX model and record 100
   episodes each on Open pursuit, Obstacle search, Familiar rooms (Cortex1),
   and New rooms (HeldoutOffsetRooms) using the fixed inference baseline in
   `POLICE_EVALUATION_AND_MAP_POOL.md`. Use the same model, map, seed, episode
   count, and arena count for later comparisons. The fixed Search baseline
   continues to use the full occupied-area 2v2 capture objective.
2. During training, watch `Evaluation/CurriculumStage` (3 means Search),
   `Evaluation/CurriculumDistance`, and `Evaluation/CurriculumSearchLesson`
   (3 Frontier, 1 Team coverage, 2 Occupied area). The lesson numbers follow
   the `SearchLessonKind` enum. `StageWindowSuccessRate` must reach 0.8 on all
   three maps. `Baseline/Search/FrontierLessonPass`,
   `Baseline/Search/EmptyAreaFrontierPass`, and
   `Baseline/Search/BothSeekersCoveredNewFloor` explain which exploration
   objective succeeds. Aggregate `Evaluation/CaptureSuccess` is not the
   exploration pass rate.
3. After each Search lesson advances, export that checkpoint and run the same
   four fixed cases. Compare full capture first, then first-sighting time,
   checked-floor fraction, each seeker's exclusive cells, frontiers inspected,
   blocked-forward decisions, and turn-in-place steps. Watch several episodes
   visually on the two-obstacle layout and Cortex1. Success means actually
   leaving the starting room and inspecting another area, not just earning a
   high reward on the easy training spawn.
4. Stop and diagnose if Frontier pass remains near zero after a meaningful
   sample, or if fixed-case search fails to improve despite lesson advancement.
   Increasing `max_steps` alone is not evidence of progress.

## How to decide the next design change

| Evaluation pattern | Next investigation |
| --- | --- |
| Little new floor checked, few frontiers inspected | Check that a scripted/manual seeker can satisfy the frontier condition on every training map. Inspect local-grid unknown/known channels and whether the agent has enough history to follow the boundary of known space. |
| New floor is checked, but frontier passes stay low | Inspect the one-cell travel, two-new-cell same-interval, frontier proximity, and target-known clauses separately. The pass rule may reject useful search. |
| Frontier pass rises, but only one seeker contributes | Review shared coverage observations and consider an explicit teammate role or target, or a separate coordination lesson. Keep the one-seeker frontier skill established first. |
| Both seekers explore, but get stuck at doors/walls | Inspect blocked-forward and turn-in-place counts alongside video. Revisit turn/move action resolution or collision handling only if these show a control problem. |
| Training passes, but two-obstacle or held-out rooms still fail | Increase layout diversity and distance gradually; check whether the observation window or recurrent memory loses useful map information. Do not infer general search from training-map capture. |
| Sighting is high but capture remains low | Focus on chase and capture behavior rather than the search curriculum. |

The current four-channel 21 × 21 local grid marks known walls, outside map,
checked floor, and age. It does not provide an explicit frontier target. A
frontier can be inferred from the boundary of known floor and unknown space.
Change observations or actions only after the fixed cases and episode traces
identify what information or control is missing. Such a change requires a new
policy architecture and a fresh run; it cannot initialize directly from this
checkpoint if tensor shapes change.
