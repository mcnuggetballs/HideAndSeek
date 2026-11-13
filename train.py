"""Launch reproducible headless ML-Agents runs from the Unity project root."""

import argparse
import re
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parent
BUILDS = {
    "cortex": ROOT / "Builds/CortexPolice/CortexPolice.exe",
    "chase": ROOT / "Builds/Chase/Chase.exe",
    "ordered": ROOT / "Builds/OrderedPolice/OrderedPolice.exe",
}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scene", choices=BUILDS, default="cortex")
    parser.add_argument("--device", choices=("cpu", "gpu"), default="cpu")
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--num-envs", type=int, default=1,
                        help="Number of separate Unity player processes")
    parser.add_argument("--arenas", type=int, default=16,
                        help="Training arenas in each player (1 to 50)")
    parser.add_argument("--time-scale", type=float, default=20.0)
    parser.add_argument("--base-port", type=int, default=5005)
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--initialize-from",
                        help="Previous run ID whose .pt checkpoint initializes a fresh run")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    if not re.fullmatch(r"[A-Za-z0-9_-]+", args.run_id):
        parser.error("--run-id must use letters, digits, - or _")
    if args.initialize_from and not re.fullmatch(r"[A-Za-z0-9_-]+", args.initialize_from):
        parser.error("--initialize-from must be a run ID")
    if args.initialize_from and args.resume:
        parser.error("--initialize-from and --resume are mutually exclusive")
    if args.initialize_from == args.run_id:
        parser.error("Use a new --run-id when initializing from an existing run")
    if args.num_envs < 1 or not 1 <= args.arenas <= 50:
        parser.error("--num-envs must be positive and --arenas must be 1 to 50")
    if args.time_scale <= 0 or args.base_port < 1024 or args.base_port + args.num_envs > 65535:
        parser.error("Invalid time scale or base port")

    build = BUILDS[args.scene]
    config = ROOT / "config" / f"config-{args.device}.yaml"
    if not args.dry_run and not build.is_file():
        parser.error(f"Build missing: {build}. Build the scene in Unity first.")

    if args.device == "gpu" and not args.dry_run:
        import torch
        if not torch.cuda.is_available():
            parser.error("CUDA is unavailable in this Python environment")
        result = (torch.ones(1024, device="cuda") * 2).sum().item()
        if result != 2048:
            parser.error("CUDA calculation failed")
        print(f"CUDA verified: torch {torch.__version__}, {torch.cuda.get_device_name(0)}", flush=True)

    command = [sys.executable, "-m", "mlagents.trainers.learn", str(config),
               f"--env={build}", f"--run-id={args.run_id}",
               f"--num-envs={args.num_envs}", f"--base-port={args.base_port}",
               f"--time-scale={args.time_scale}", "--no-graphics"]
    if args.resume:
        command.append("--resume")
    if args.initialize_from:
        command.extend(("--initialize-from", args.initialize_from))
    command.extend(("--env-args", "--training-arenas", str(args.arenas),
                    "--curriculum-run-id", args.run_id))
    if args.resume:
        command.append("--curriculum-resume")
    print("Total arenas:", args.num_envs * args.arenas, flush=True)
    print("Command:", subprocess.list2cmdline(command), flush=True)
    if args.dry_run:
        return 0
    return subprocess.call(command, cwd=ROOT)


if __name__ == "__main__":
    raise SystemExit(main())
