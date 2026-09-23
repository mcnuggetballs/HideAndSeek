# RedVsBlue teammate research baseline

This project preserves the supplied RedVsBlue implementation as a research baseline before adapting its learning mechanisms. The preserved archive is `Research/TeammateBaseline/RedVsBlue_Phase1_Source.zip`; its SHA-256 inventory is stored beside it.

## Provenance

- Supplied project: `RedVsBlue_20260904_final/RedVsBlue`
- Teammate associated with the research: Loh Shao Cong (also named as author in `AgentController.cs`)
- Unity: 6000.3.16f1
- Unity ML-Agents package: 4.0.3
- Behavior: `Team Search`
- Trainer: MA-POCA with recurrent memory
- Source state: captured from the supplied working tree, including the untracked `Assets/Scripts/New` and `Assets/Prefabs/New` implementation

The archive is evidence and reference material. It is not compiled into HideAndSeek.

## Preserved contributions

- Environment-level group episode lifecycle using `SimpleMultiAgentGroup`
- Successful termination versus timeout interruption
- Central reward ownership
- Adaptive distance curriculum with pooled success results
- Catch-weighted hostile spawning
- Heading-aligned local wall sensor
- Stable teammate and hostile-sighting observations
- Episode construction and result data contracts

## Integration decision

HideAndSeek retains its own `EnvironmentManager`, `EnvironmentInstance`, `EnvironmentSpawner`, `ScenarioGrid`, `WorldBuilder`, and runtime navigation pipeline. The duplicate RedVsBlue managers and grid builder are not imported.

The following ideas are adapted with attribution:

- Group lifecycle and environment-level timeout into `EnvironmentEpisodeCoordinator`
- Episode specifications and outcomes into `Assets/Scripts/Training`
- Adaptive spawn curriculum into `AdaptiveSpawnCurriculum`
- Local wall sensing into `Assets/Scripts/Sensing`
- Team and hostile-sighting observations into `SeekerAgent` and `TeamSightingMemory`

## Reproduction limits

The supplied project contains no `.onnx`, `.pt`, or training-results directory. Phase 1 can preserve and compile the supplied source, scene, prefabs, package manifest and trainer configuration, but it cannot reproduce a reported trained policy without the missing checkpoint or a new training run.

## Validation performed

- Clean import and compilation succeeded in Unity 6000.3.16f1 with ML-Agents 4.0.3.
- The supplied `TrainingScene` was run for 150 headless editor frames from an untouched temporary copy.
- The scene initialized, but headless inference repeatedly reached `AgentController.GetKeyboardInputs` with `Keyboard.current == null`. This is specific to running without an input device and without an attached trainer; the training path disables keyboard input when the ML-Agents communicator is connected.
- No teammate source was changed to hide or repair this baseline observation.
