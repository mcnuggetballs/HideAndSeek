# Training and speed benchmarks

Use Python 3.10 and ML-Agents 1.1.0 with this project's Unity ML-Agents 4.0.3. The live CPU environment reports Python 3.10.11 and PyTorch 2.1.1+cpu. For the RTX 5090, use a separate Blackwell-capable CUDA PyTorch environment, then verify an actual GPU calculation before training. An earlier `torch 2.2.2+cu121` installation could not execute on that card.

## Build and launch

In Unity, build the chosen scene:

| Scene | Unity menu | Player |
| --- | --- | --- |
| Cortex team | Tools > Police Curriculum > Build Cortex1 Training Player | `Builds/CortexPolice/CortexPolice.exe` |
| Chase | Tools > Chase > Build Training Player | `Builds/Chase/Chase.exe` |
| Ordered police | Tools > Police Curriculum > Build Training Player | `Builds/OrderedPolice/OrderedPolice.exe` |

From this directory, first inspect the command:

```powershell
python train.py --scene cortex --device cpu --run-id cortex_cpu_01 --num-envs 1 --arenas 16 --dry-run
```

Then remove `--dry-run` to train. The launcher selects `config/config-cpu.yaml` or `config/config-gpu.yaml`, requests `--no-graphics`, and sets a time scale of 20. It verifies a CUDA calculation before GPU training. It keeps the total arena count explicit: `num-envs × arenas`. For a matched comparison at the current count, try `1 × 16`, `2 × 8`, and `4 × 4`, each with a new run ID. Do not run these simultaneously on the same GPU during a benchmark.

The player receives `--training-arenas` and `--curriculum-run-id`. ML-Agents supplies a unique `--mlagents-port` per player; this separates curriculum progress files and random seeds. Progress files are under Unity's `Application.persistentDataPath/TrainingProgress`. `train.py --resume` resumes both the trainer and matching curriculum file. Keep the same run ID, base port, and worker layout when resuming. The local curricula are independent across players, so a throughput win must be checked against capture performance. Existing live `hide_seek_v1` checkpoints use the old curriculum location and should continue with their existing setup until that run is complete.

## GPU check

Run `./setup_gpu.ps1` once from this project directory to create the separate `mlagents_gpu_env`. It uses the installed Python 3.10 interpreter, PyTorch 2.7.1 with CUDA 12.8, and ML-Agents 1.1.0, then checks dependencies and executes a CUDA operation. Package installation requires internet access. Start GPU training with `./mlagents_gpu_env/Scripts/python.exe train.py --device gpu ...`.

Activate the intended Python environment and check `nvidia-smi`, then run:

```powershell
python -c "import torch; print(torch.__version__, torch.version.cuda, torch.cuda.get_device_name(0)); print((torch.ones(1024,device='cuda')*2).sum().item())"
```

The sum must be `2048.0` without an exception. Start a short new run and confirm policy updates, checkpoint save, ONNX export, and inference loading before a 20-million-step run. GPU training accelerates PyTorch updates; Unity physics remains on the CPU.

On 12 November 2026, this source copy passed a 10,000-step headless Cortex smoke run with PyTorch 2.7.1+cu128 on the RTX 5090. It saved checkpoints and exported ONNX. Two Unity workers also completed a 10,000-step run and created separate progress files for ports 5205 and 5206. With four arenas in one player, the GPU run reported 20.2 seconds to 10,000 steps and the existing CPU environment reported 20.8 seconds. The two-worker run used eight total arenas and reported 13.5 seconds. These are startup-heavy smoke measurements, not a matched 16-arena or 20-million-step speed comparison.

## Measure before tuning further

Use the same scene, behavior config, total arenas, time scale, and evaluation cases for each comparison. Record warm-run steps/second over several minutes, CPU and GPU utilization, and `results/<run-id>/run_logs/timers.json`. Measure full-capture rate and first-sighting time so a faster run does not hide worse learning. For Unity CPU work, profile a standalone development player and inspect **CPU Usage → Hierarchy**, sorting by Total Time. Capture `FixedUpdate`, physics, scripts, and GC; the overview's 16/33/66 ms guide lines do not identify a bottleneck.

The local grid observation is 4 × 21 × 21 per seeker decision. Visibility rays and coverage updates are other candidates for later profiling. Changing observation shape or decision frequency changes the training contract; benchmark such changes in a fresh run.
