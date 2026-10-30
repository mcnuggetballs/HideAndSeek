# Paired seeker Search lesson

## Starting positions

In the Cortex Police curriculum, the two seekers start **2 to 5 walking cells apart**, in an open line of sight to each other. The limit is based on the shortest walkable grid path, so being on opposite sides of a wall does not count as nearby. Capture, Chase, and Detect may place both hiders in view of either seeker. Search still starts with both hiders hidden by walls from both seekers. The hiders and seeker headings continue to vary between episodes. This setting is `Cortex Maximum Seeker Spawn Distance` in `TrainingCortexPoliceScene` (default 5).

## What counts as checked

The team's influence map marks a floor cell checked when it is reachable from the seekers, within a seeker's actual view cone and range, and its grid sight line is not blocked by a wall. A cell is checked once per episode, even if viewed again. The local grid sensor already supplies each seeker with nearby shared checked/age channels. This lesson does **not** claim that a room is semantically cleared, nor reveal a hider behind a wall.

Every four physics steps, both seekers' visible cells are evaluated against the same pre-update map. A newly checked cell is classified as exclusive to seeker 0, exclusive to seeker 1, or visible to both. This avoids giving the first seeker in an iteration arbitrary credit for overlapping views. Initial coverage at the spawn earns no reward.

## Search reward

The reward applies only during Search and only when no active hider has a usable team sighting. Capture still gives +5 group reward per hider and the existing individual catch reward. The time cost remains unchanged.

At least **4 new team cells in one decision interval** are needed for a coverage reward. When that happens:

- Group coverage reward = `1.0 × (exclusive new cells + 0.25 × shared new cells) / reachable floor cells` with the scene defaults. It can never exceed 1.0 in an episode because every cell is counted once.
- Each seeker separately earns `0.5 × its exclusive new cells / (reachable floor cells / number of seekers)`, capped at **0.5 per seeker per episode**. Cells both seekers view do not earn a personal coverage reward.

This favors two complementary view cones and gives the active explorer a little more reward than a teammate who circles over already checked floor. It does not penalize a necessary turn or force agents to keep searching after they have a target. Small glimpses and repeat visits earn no coverage reward. The policy must still learn which actions reach another unchecked area; this code does not assign rooms or navigate the seekers automatically.

## How to judge it

The baseline CSV now includes `seeker_0_exclusive_checked`, `seeker_1_exclusive_checked`, `shared_new_checked`, `rewarded_novel_cells`, and `seeker_spawn_separation_cells`. Exclusive counts begin after the initial free observation. Compare both seekers: if one has near-zero exclusive cells while the other has many, the team is still relying on one explorer. A large shared count with low exclusive counts suggests overlapping view cones. Look at full captures and first sighting time first; high coverage without captures is not success.

The reward meaning and spawn distribution changed, so start a **new training run and fresh curriculum progress**. The observation and action shapes are unchanged, which keeps existing ONNX models usable for inference, but an old model has not learned these incentives. Re-run the fixed evaluation cases with the same settings for each checkpoint. Since the starting distribution changed, earlier baseline percentages from before this change are not directly comparable.
