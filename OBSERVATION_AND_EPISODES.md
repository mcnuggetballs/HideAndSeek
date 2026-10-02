# Chase observation and episode contract

This contract applies to the seeker prefabs and `Team Search` behavior after the October 2026 sensor change. The previous ONNX model accepted a different observation shape and must not be used with these prefabs. Train a new model from a new run ID and a fresh curriculum file, then attach that model for inference.

## What a seeker receives

The vector has **52 floats**, always in this order:

| Index | Meaning |
| --- | --- |
| 0–1 | Seeker X/Z relative to its arena grid center, in arena coordinates and divided by grid half width/height. |
| 2–3 | Seeker forward X/Z in arena coordinates. |
| 4–23 | Four teammate slots, five values each: present, relative X/Z in seeker coordinates, and relative heading X/Z. Unused slots are zero. |
| 24–51 | Four hider slots, seven values each: active, known position, personally visible now, teammate visible now, normalized sighting age, last known relative X/Z. Missing or caught hiders have seven zeros. |

For an active hider with no valid sighting, `active=1`, `known=0`, `age=1`, and position is zero. A current sighting has age zero. A remembered sighting has positive age, divided by `hostileMemorySteps` (512 in the prefab), and remains valid through that limit. Position is the **last observed world position**, transformed into the seeker's current heading frame, not a prediction or privileged live position. `personally visible now` is set only by this seeker's current line of sight. `teammate visible now` is set when the shared memory was refreshed this physics step and this seeker does not see the hider itself. The arena controller runs its perception pass before ML-Agents requests the step's decisions, so these flags describe the latest physics-step sweep. The teammate and target slots keep arena list order across an episode. Sightings clear on capture and restart.

The local grid is a **2 × 21 × 21** image at the prefab's radius 5 and supersampling 2. Channel 0 marks `X` wall cells. Channel 1 marks outside the arena. Both are zero for walkable cells, including spawn markers. The center pixel samples the seeker's position. Higher row index is farther forward; higher column index is farther right. Sampling uses the seeker's horizontal forward/right vectors, a 2.5 world unit pixel spacing for the current 5 unit cells, and the `ScenarioGrid` owned by that seeker's arena. The window turns with the seeker. It encodes map geometry, not hiders or their last sightings.

The child ray sensor supplies **11 rays** across ±45 degrees, 5 world units long. It reports obstacle/hider tags, hit or miss, and hit fraction (44 values total). Rays report nearby physical contacts and can see dynamic hiders. The seeker's separate line of sight can reach 10 units and is the source of the visibility flags and shared sightings. Thus rays, grid, and sighting vector have distinct jobs and ranges.

## Episode outcome

For chase and later team/search lessons, success means **every hider was caught before the deadline**. Each capture gives the team a reward and the catcher an individual reward, so partial progress still has a learning signal. A deadline with some hiders remaining is a completed failure, with captured fraction logged for diagnosis and full/partial captures available to the curriculum. A manual restart interrupts the trajectory and is excluded from success, step, and captured fraction statistics and from curriculum advancement. The time penalty accrues per physics step through the deadline.

The quick chase scene starts with one seeker and one hider on an open map, adjacent spawn cells, and the seeker facing the hider. Its episode deadline is 150 physics steps. It advances from the first lesson only after a 100 episode window reaches 95% full capture within that limit. Later lessons introduce random headings and greater distance. The separate team/search training scene retains its 2048 step cutoff.

## Capture mechanics and measured results

The original 1.5 world unit catch distance was smaller than the separation the scaled character agents could reliably reach. A sighting-guided scripted seeker ran toward a stationary hider but caught only **4/100** in the first lesson. The seeker and hider roots both have scale 2, so the capture comparison now uses each agent radius multiplied by its horizontal world scale, plus 0.1 unit tolerance. The same scripted seeker then caught **100/100**, with median and 90th percentile capture times both 30 physics steps. It steers from the shared sighting memory and does not read the hider's live position when that position is unknown.

A fresh MA-POCA model was trained for 1,000,000 steps with the 52-value vector and 2-channel grid. The training executable was built before the catch-distance correction; the final model was evaluated against the corrected rule. A 100-start, one-arena test of the fixed first lesson with spawn seed 12345 gave **100/100 full captures**, mean 33.8 steps, median 34 steps, and 90th percentile 38 steps. A second 100-start test with seed 67890 also gave **100/100** after the perception ordering fix, mean 34.0 steps, median 33 steps, and 90th percentile 37.1 steps. The 200k and 500k checkpoints had each caught **0/100** under the old capture rule, despite moving and seeing the target, which is why reward trends were not accepted as proof of reliable chase.

The evaluated model is `Assets/AI Models/ChaseLesson.onnx`. `Assets/Prefabs/Seeker/ChaseLessonSeeker.prefab` loads it in inference mode. Open `Assets/Scenes/TrainingChaseInferenceScene.unity` for a one-arena first-lesson demonstration with fresh spawn progress. The ordinary `TrainingChaseScene.unity` keeps 40 parallel arenas and the training seeker prefab. General `SeekerPrefab.prefab` remains unassigned because this chase model has only been evaluated on the first lesson; team/search behavior needs its own training and evaluation.

To reproduce CPU training, use **Tools > Chase > Build Training Player** in Unity, then run the following from the project root with the Python environment containing ML-Agents 1.1.0:

```powershell
python config/run_chase_cpu.py config/config-chase-cpu.yaml --env Builds/Chase/Chase.exe --run-id chase_fresh_run --no-graphics --time-scale 20
```

Use a new run ID. The local PyTorch 2.2.2 CUDA build could see the RTX 5090 but could not run its kernels; `run_chase_cpu.py` hides CUDA before ML-Agents imports PyTorch. `torch_settings: device: cpu` alone was insufficient with this installed combination. The original model and older training checkpoints use incompatible observations and must not be resumed into this run.

Unity edit mode checks sampled production-size wall and boundary values, a 90 degree seeker turn, and two independent arena grids. The vector observation and curriculum checks passed. A headless chase smoke test built all 40 seekers and advanced physics steps. The final first-lesson result exceeds its 95% full-capture gate. Random heading, greater distance, obstacles, multiple hiders, and teammates remain separate evaluation gates before using this model for those scenarios.
