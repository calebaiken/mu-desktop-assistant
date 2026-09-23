param([string]$ModelDirectory = (Join-Path $PSScriptRoot '../artifacts/local-ai'))
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ModelDirectory = [IO.Path]::GetFullPath($ModelDirectory)
$evidence = Join-Path $repo ('artifacts/qa/local-model-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$appDirectory = Join-Path $repo 'src/Companion.Desktop/bin/Release/net10.0-windows'
$app = Join-Path $appDirectory 'Companion.Desktop.exe'
$protocol = Join-Path $repo 'docs/qa/PHASE_3_LOCAL_MODEL_PROTOCOL.md'
$contextProtocol = Join-Path $repo 'docs/qa/PHASE_3_CONTEXT_MODEL_PROTOCOL.md'
$identities = @{}
foreach ($name in @('Companion.Desktop.exe','Companion.Desktop.dll','Companion.Application.dll','Companion.Core.dll','Companion.Infrastructure.dll')) {
    $identities[$name] = (Get-FileHash -LiteralPath (Join-Path $appDirectory $name) -Algorithm SHA256).Hash
}
$plan = [ordered]@{
    schemaVersion = 1; startedUtc = [DateTime]::UtcNow.ToString('o'); protocolSha256 = (Get-FileHash -LiteralPath $protocol -Algorithm SHA256).Hash
    contextProtocolSha256 = (Get-FileHash -LiteralPath $contextProtocol -Algorithm SHA256).Hash
    binaries = $identities; modelSha256 = (Get-FileHash -LiteralPath (Join-Path $ModelDirectory 'Qwen3-0.6B-Q8_0.gguf') -Algorithm SHA256).Hash
    maximumSeconds = 390; sourceCheckpoint = (& git -C $repo rev-parse HEAD)
    sourceState = 'Working tree increment; binary hashes above freeze the executed build.'
}
$plan | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence 'plan.json') -Encoding utf8
$process = New-Object Diagnostics.Process
$process.StartInfo.FileName = $app
$process.StartInfo.UseShellExecute = $false
$process.StartInfo.CreateNoWindow = $true
# Validated local paths cannot contain a quote; arguments are Windows quoted paths.
if ($ModelDirectory.Contains('"') -or $evidence.Contains('"')) { throw 'Invalid path.' }
$process.StartInfo.Arguments = '--local-model-smoke --local-model-dir "' + $ModelDirectory + '" --data-dir "' + $evidence + '"'
$exitCode = -1
$timedOut = $false
try {
    if (-not $process.Start()) { throw 'Could not start local model verification.' }
    if (-not $process.WaitForExit(390000)) { $timedOut = $true; $process.Kill(); $process.WaitForExit(5000) | Out-Null }
    $exitCode = $process.ExitCode
}
finally {
    [ordered]@{ exitCode=$exitCode; timedOut=$timedOut; endedUtc=[DateTime]::UtcNow.ToString('o') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'execution.json') -Encoding utf8
    $process.Dispose()
}
Write-Output ('Evidence: ' + $evidence)
if ($timedOut -or $exitCode -ne 0) { throw 'Local model verification failed; the evidence directory preserves the outcome.' }
Write-Output 'PASS local model integration; see separate content observations and qualification limitations.'
