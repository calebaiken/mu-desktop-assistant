[CmdletBinding()]
param([string]$Python = 'python', [string]$Uv = 'uv')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runtime = Join-Path $repo 'artifacts/ml-runtime'
$env:UV_CACHE_DIR = Join-Path $repo 'artifacts/uv-cache'
$env:UV_LINK_MODE = 'copy'
$env:PYTHONNOUSERSITE = '1'
$drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($repo))
if ($drive.AvailableFreeSpace -lt 2GB) { throw 'At least 2 GiB free space is required for the optional ML environment.' }
$version = & $Python -c 'import sys; print(sys.version_info.major,sys.version_info.minor,sep=chr(46))'
if ($LASTEXITCODE -ne 0 -or $version -notin @('3.12', '3.13')) { throw 'Choose a local Python 3.12 or 3.13 interpreter with -Python.' }
if (-not (Test-Path -LiteralPath (Join-Path $runtime 'Scripts/python.exe'))) {
    & $Uv venv --python $Python $runtime
    if ($LASTEXITCODE -ne 0) { throw 'ML environment creation failed.' }
}
$pythonExe = Join-Path $runtime 'Scripts/python.exe'
& $Uv pip sync --python $pythonExe --require-hashes (Join-Path $repo 'workers/requirements-ml.lock')
if ($LASTEXITCODE -ne 0) { throw 'ML dependency installation failed. Existing Mu chat remains available.' }
& $pythonExe -I -c 'import numpy,pandas,sklearn,scipy,matplotlib,h5py,skops,psutil; print(sklearn.__version__)'
if ($LASTEXITCODE -ne 0) { throw 'ML dependency check failed.' }
$runtimeConfig = @{ schemaVersion = 1; python = $pythonExe; worker = (Join-Path $repo 'workers/ml_lab/worker.py') } | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $repo 'artifacts/ml-runtime.json'), $runtimeConfig, [Text.UTF8Encoding]::new($false))
Write-Output 'Launch Mu using scripts/run-local-chat.ps1. Machine Learning Lab is in the More menu and Chat.'
