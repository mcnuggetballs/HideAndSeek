# Hide-and-Seek: ML-Agents training guide

This guide explains how to prepare Python and train the seekers in Unity, including GPU training on an NVIDIA RTX 5090. Commands below are for **Windows Command Prompt (CMD)** unless stated otherwise. Run them from the Unity project directory containing `Assets` and `config`.

## 1. What the computer and software do

| Term | Meaning | Role in this project |
| --- | --- | --- |
| CPU | The general-purpose processor | Runs Unity gameplay code, movement, physics, visibility checks, and rewards. |
| CPU cores | Processing units that can work on separate jobs | Multiple Unity processes can use different cores. One sequential job cannot automatically use every core. |
| RAM | Temporary working memory for running programs | Holds the running environments and their data. More free RAM does not automatically make calculations faster. |
| GPU | A processor designed for many similar calculations at once | Can perform neural-network calculations for training. |
| VRAM | The GPU's own working memory | Holds network data and calculations on the GPU. It is separate from RAM. |
| Python | The programming language used by the trainer | Runs the ML-Agents training program outside Unity. |
| ML-Agents | The training software connecting Unity to a learning algorithm | Collects observations, sends actions, and trains the policy. |
| PyTorch | The library that performs neural-network calculations | Can run those calculations on the CPU or GPU. |
| CUDA | NVIDIA's platform for GPU computation | Allows CUDA-enabled PyTorch to use an NVIDIA GPU. |
| Virtual environment | A separate folder containing Python packages | Keeps training dependencies separate from other projects. |

The computer inspected for this project has a Ryzen 9 9950X3D (16 cores, 32 hardware threads), approximately 64 GB RAM, and an RTX 5090 with 32 GB VRAM. Hardware threads are not the same as full physical cores.

During GPU training, the work is approximately:

```text
CPU runs Unity environments -> observations and rewards -> PyTorch trains on GPU
CPU runs Unity environments <- actions from the policy <- PyTorch
```

Using CUDA through PyTorch does not require writing CUDA code. It also does not move Unity physics or NavMesh simulation onto the GPU. Prebuilt PyTorch packages provide the required CUDA runtime libraries; a compatible NVIDIA graphics driver is still required. A separate full CUDA development toolkit is normally unnecessary for this workflow.

## 2. Choose one installation path

- **CPU setup:** preserves the older package versions from the original project instructions.
- **GPU setup:** uses a separate environment and a newer CUDA-enabled PyTorch for the RTX 5090.

Do not install both sets of PyTorch versions into the same environment. Do not change packages in an environment while it is running a training job.

### Python version

The project has been using Python **3.10.12**. Verify the version with:

```bat
python --version
```

If the Windows Python launcher already has Python 3.10 installed, use:

```bat
py -3.10 --version
```

`py -3.10` selects an installed Python 3.10 interpreter; it does not guarantee patch version 3.10.12. `python -3.10` is not a valid way to select a Python version.

For exactly Python 3.10.12, an alternative is **Anaconda Prompt** with Miniconda installed:

```bat
conda create -n hide-seek-python310 python=3.10.12
conda activate hide-seek-python310
python --version
```

You can then use that interpreter with `python -m venv` below. Python 3.10.12 was a source-only Python.org release, so do not expect a standard Windows installer on its release page.

## 3. Original CPU setup

Create the environment using an installed Python 3.10 interpreter:

```bat
py -3.10 -m venv mlagents_env
```

Alternatively, if `python --version` already shows the intended Python 3.10 version:

```bat
python -m venv mlagents_env
```

Activate and install the original dependencies:

```bat
mlagents_env\Scripts\activate
python -m pip install --upgrade pip
python -m pip install mlagents==1.1.0 torch==2.1.1
python -m pip install packaging==21.3 setuptools==65.5.0
python -m pip check
```

The prompt should include `(mlagents_env)`. Activation tells the terminal which Python environment to use. You need to activate it again when opening a new terminal, but you do not need to reinstall packages each time.

These are the legacy project package versions, not the RTX 5090 GPU installation. Avoid an unpinned `pip install mlagents` later: it makes the intended trainer version unclear.

To explicitly use CPU training, set this at the top level of the training YAML:

```yaml
torch_settings:
  device: cpu
```

## 4. GPU setup for the RTX 5090

### Why the old PyTorch version must change

The RTX 5090 uses NVIDIA's Blackwell architecture. The previously inspected `torch 2.2.2+cu121` installation could detect the GPU but failed to execute a calculation on it. PyTorch 2.1.1 from the original instructions is also too old for this GPU.

The setup below pins **PyTorch 2.7.1 with CUDA 12.8** as a compatibility candidate. PyTorch 2.7 introduced Blackwell support. This is deliberately not an instruction to install an arbitrary newest PyTorch version with the older ML-Agents 1.1.0 trainer.

**Validation status:** the incompatible older installation was tested. The new combination below has not yet been installed and validated end to end for this project. Complete the short training, checkpoint, and export checks before a long run.

### Create a separate GPU environment

Use either of these creation commands, depending on how you selected Python above:

```bat
py -3.10 -m venv mlagents_gpu_env
```

Or, when `python` is already the intended Python 3.10 interpreter:

```bat
python -m venv mlagents_gpu_env
```

Then activate and install:

```bat
mlagents_gpu_env\Scripts\activate
python -m pip install --upgrade pip
python -m pip install setuptools==65.5.0
python -m pip install torch==2.7.1 --index-url https://download.pytorch.org/whl/cu128
python -m pip install mlagents==1.1.0
python -m pip check
```

`cu128` identifies the CUDA 12.8 package source. The CUDA build is what enables this PyTorch installation to use the GPU. `torchvision` and `torchaudio` are not required by these project instructions. The legacy `packaging==21.3` pin is not carried into this new environment unless an actual dependency requires it.

### Verify GPU execution

Check that Windows can see the NVIDIA GPU and driver:

```bat
nvidia-smi
```

Then run an actual calculation:

```bat
python -c "import torch; print('PyTorch:',torch.__version__); print('CUDA build:',torch.version.cuda); print('CUDA available:',torch.cuda.is_available()); print('GPU:',torch.cuda.get_device_name(0)); x=torch.ones(1024,device='cuda'); print('GPU calculation:',(x+x).sum().item())"
```

Expected results include:

- PyTorch version `2.7.1+cu128`.
- CUDA build `12.8`.
- CUDA available `True`.
- GPU name `NVIDIA GeForce RTX 5090`.
- GPU calculation `2048.0`, with no error.

`cuda.is_available()` alone is not enough: the calculation confirms that GPU code can actually execute. The CUDA version shown by `nvidia-smi` describes driver capability and can differ from PyTorch's CUDA build.

### Create a GPU training configuration

Copy the existing configuration in CMD:

```bat
copy config\config.yaml config\config-gpu.yaml
```

Open `config/config-gpu.yaml` and add or update this top-level section:

```yaml
torch_settings:
  device: cuda:0
```

`cuda:0` means the first NVIDIA GPU. This section belongs outside `behaviors`, at the same indentation level. Do not add a duplicate `torch_settings` section if one exists. Keep the existing behavior, observation, reward, and network settings for the first comparison.

This setting controls the Python trainer. Unity's Behavior Parameters **Inference Device** setting is for running an exported model inside Unity; it does not select the Python training device.

## 5. Start training in the Unity Editor

Open the training scene in Unity. In CMD, activate the chosen environment and start the trainer.

CPU:

```bat
mlagents_env\Scripts\activate
python -m mlagents.trainers.learn config/config.yaml --run-id=hide_seek_v1
```

GPU:

```bat
mlagents_gpu_env\Scripts\activate
python -m mlagents.trainers.learn config/config-gpu.yaml --run-id=hide_seek_gpu_test_01
```

When the terminal says to start training by pressing Play, press **Play in Unity**. Use a new run ID for a new experiment. Results are stored under `results/<run-id>` relative to the terminal's directory.

For the GPU compatibility test, wait until the trainer has actually updated the policy. Then press **Ctrl+C once** in the trainer terminal and let it save. Confirm that a checkpoint and an ONNX model were produced without errors. Test loading the exported model in Unity before relying on a long training run.

To resume the GPU test from its saved training checkpoint:

```bat
python -m mlagents.trainers.learn config/config-gpu.yaml --run-id=hide_seek_gpu_test_01 --resume
```

An ONNX file is for inference. Resuming training uses the trainer's saved checkpoint files, so keep the complete results folder. Try a separate new GPU run before migrating an older run across PyTorch versions.

## 6. Observe training and compare speed

In a second activated terminal, open TensorBoard:

```bat
tensorboard --logdir results
```

Open the local address printed by TensorBoard. Examine reward trends and any capture-success statistics the project records.

In another terminal, monitor the GPU:

```bat
nvidia-smi -l 2
```

This refreshes every two seconds. Ctrl+C stops the monitor without stopping training in another terminal.

Compare CPU and GPU runs with the same arena count and learning settings. Ignore startup time and measure several minutes:

```text
Training steps per second = increase in reported training steps / elapsed seconds
```

More steps per second means faster experience processing. Also evaluate capture success: faster processing does not guarantee better learning. Low GPU utilization can mean the GPU is waiting for Unity, or that the network workload is small. Filling all VRAM or reaching 100% GPU usage is not the goal.

## 7. Further performance improvements

After the GPU setup works, change one thing at a time.

### Standalone build without rendering

Build a Windows executable containing the training scene. Replace the example path with the actual executable location:

```bat
python -m mlagents.trainers.learn config/config-gpu.yaml --env="Builds/Training/HideAndSeek.exe" --run-id=hide_seek_gpu_build_01 --num-envs=1 --no-graphics
```

This avoids Editor overhead and disables graphics rendering. The custom local-grid sensor inspected in this project reads grid data, not rendered camera images. Reconsider `--no-graphics` if camera-image observations are added later.

### Multiple Unity processes

Multiple arenas inside one executable and multiple executables are different:

```text
Total arenas = arenas in each executable × --num-envs
```

Four executables containing ten arenas each provide forty arenas in total. Separate Unity processes can make better use of CPU cores. Start by comparing 1 × 40, 2 × 20, and 4 × 10 before increasing the total arena count.

**Project prerequisite:** the current curriculum saves to a shared `curriculum_progress_v2.json` beside the project/build. Before launching multiple copies of the same build, change persistence to use worker-specific files and distinct random seeds, or implement a centrally coordinated curriculum. Otherwise workers can overwrite each other's progress. Separate files also mean each process has its own curriculum, which should be accounted for when comparing experiments.

After addressing that prerequisite, a four-process command would use `--num-envs=4`. Rebuild with the desired per-process arena count first; the flag does not divide the count configured in the build.

### Overlap simulation and updates

As a separate experiment, add this field inside the existing behavior:

```yaml
behaviors:
  Team Search:
    # Keep the other existing behavior settings here.
    threaded: true
```

This allows environment stepping while the trainer updates the model. It does not automatically multithread Unity C# code. Compare throughput and learning quality before keeping it. Do not change batch size, buffer size, arena count, and threading all at once: it becomes difficult to identify what helped.

## 8. Troubleshooting

| Problem | What to check |
| --- | --- |
| `py` is not recognized | Use the intended Python 3.10 interpreter through `python`, for example after activating the Conda environment above. |
| The prompt shows the wrong environment | Activate the intended environment and use `python -c "import sys; print(sys.executable)"` to confirm the interpreter path. |
| `No module named mlagents` | Install the pinned ML-Agents package in the activated environment. |
| CUDA is unavailable | Check the activated environment, PyTorch CUDA build, and NVIDIA driver. |
| `no kernel image is available` or an `sm_120` compatibility warning | The installed PyTorch build does not support the RTX 5090. Check that the GPU environment uses the specified CUDA-enabled version. |
| `pip check` reports incompatible dependencies | Resolve the reported package conflict before training. Do not assume installation success means all dependencies are compatible. |
| Resume reports a `weights_only` or unsupported-global error | PyTorch 2.6+ changed checkpoint-loading defaults. The older trainer may need a targeted compatibility fix for its own trusted checkpoint. Keep the original checkpoint and diagnose the actual error; do not disable checkpoint protections globally. |
| Training works but ONNX export fails | Save the error and check trainer/exporter compatibility. Passing the GPU calculation test does not validate ONNX export. |
| GPU training is not faster | Unity simulation, communication, or small GPU batches may be the bottleneck. Compare steps per second and profile before adding more arenas. |

## 9. Architecture notes

Game manager responsibilities from the original project notes:

- Start and stop game sessions.
- Select the influence-map configuration.

## 10. References

- [PyTorch 2.7: Blackwell and CUDA 12.8 support](https://pytorch.org/blog/pytorch-2-7/)
- [Official PyTorch version-specific installation commands](https://pytorch.org/get-started/previous-versions/)
- [ML-Agents training configuration](https://unity-technologies.github.io/ml-agents/Training-Configuration-File/)
- [ML-Agents training and concurrent Unity instances](https://unity-technologies.github.io/ml-agents/Training-ML-Agents/)
- [Unity ML-Agents Agent API — original 1.0 documentation link](https://docs.unity3d.com/Packages/com.unity.ml-agents@1.0/api/Unity.MLAgents.Agent.html)
- [Unity background — ML-Agents 4.0 documentation](https://docs.unity3d.com/Packages/com.unity.ml-agents@4.0/manual/Background-Unity.html)

The last two links target different Unity package versions. Python package and Unity package version numbers are separate; consult documentation matching the Unity package installed in the project when checking APIs.
