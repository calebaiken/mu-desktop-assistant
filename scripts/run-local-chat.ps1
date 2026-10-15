param([string]$ModelDirectory = (Join-Path $PSScriptRoot '../artifacts/local-ai'))
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable = Join-Path $repo 'src/Companion.Desktop/bin/Release/net10.0-windows/Companion.Desktop.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build the project with scripts/verify.ps1 first.' }
if (-not (Test-Path -LiteralPath (Join-Path $ModelDirectory 'Qwen3-0.6B-Q8_0.gguf'))) { throw 'Run scripts/setup-local-ai.ps1 first.' }
& $executable --local-model-dir ([IO.Path]::GetFullPath($ModelDirectory)) --ml-root $repo
