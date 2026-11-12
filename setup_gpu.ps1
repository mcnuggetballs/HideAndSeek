param(
    [string]$Python310 = (Join-Path $env:LOCALAPPDATA 'Programs/Python/Python310/python.exe')
)

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$venv = Join-Path $project 'mlagents_gpu_env'
if (-not (Test-Path -LiteralPath $Python310)) {
    throw "Python 3.10 not found at $Python310. Pass -Python310 with its full path."
}
& $Python310 -c 'import sys; assert sys.version_info[:2] == (3, 10), sys.version'
if ($LASTEXITCODE -ne 0) { throw 'Python 3.10 is required.' }
& $Python310 -m venv $venv
if ($LASTEXITCODE -ne 0) { throw 'Could not create GPU environment.' }
$python = Join-Path $venv 'Scripts/python.exe'
& $python -m pip install --upgrade pip
if ($LASTEXITCODE -ne 0) { throw 'pip upgrade failed.' }
& $python -m pip install torch==2.7.1 --index-url https://download.pytorch.org/whl/cu128
if ($LASTEXITCODE -ne 0) { throw 'CUDA PyTorch installation failed.' }
& $python -m pip install mlagents==1.1.0
if ($LASTEXITCODE -ne 0) { throw 'ML-Agents installation failed.' }
& $python -m pip check
if ($LASTEXITCODE -ne 0) { throw 'Dependency check failed.' }
& $python -c "import torch; print(torch.__version__, torch.version.cuda, torch.cuda.get_device_name(0)); assert (torch.ones(1024,device='cuda')*2).sum().item() == 2048"
if ($LASTEXITCODE -ne 0) { throw 'GPU calculation failed.' }
Write-Output "GPU environment ready: $python"
