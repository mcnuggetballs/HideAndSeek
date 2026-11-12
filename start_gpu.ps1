param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9_-]+$')]
    [string]$RunId,
    [ValidateRange(1, 50)]
    [int]$NumEnvs = 1,
    [ValidateRange(1, 50)]
    [int]$Arenas = 16,
    [switch]$Resume
)

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$python = Join-Path $project 'mlagents_gpu_env/Scripts/python.exe'
$launcher = Join-Path $project 'train.py'
$logDirectory = Join-Path $project 'Logs'
if (-not (Test-Path -LiteralPath $python)) { throw 'GPU Python environment is missing. Run setup_gpu.ps1.' }
if (-not (Test-Path -LiteralPath $launcher)) { throw 'train.py is missing.' }
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$transcript = Join-Path $logDirectory "$RunId.transcript.txt"
Start-Transcript -Path $transcript -Append | Out-Null
try {
    Set-Location -LiteralPath $project
    $arguments = @($launcher, '--scene', 'cortex', '--device', 'gpu',
        '--run-id', $RunId, '--num-envs', $NumEnvs, '--arenas', $Arenas)
    if ($Resume) { $arguments += '--resume' }
    & $python @arguments
    if ($LASTEXITCODE -ne 0) { throw "Training exited with code $LASTEXITCODE." }
}
finally {
    Stop-Transcript | Out-Null
}
