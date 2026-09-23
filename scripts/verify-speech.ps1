[CmdletBinding()]
param([switch]$Playback)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$runRoot = Join-Path $repo ('artifacts/qa/speech-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
$profile = Join-Path $runRoot 'profile'
New-Item -ItemType Directory -Path $profile -Force | Out-Null
$appDirectory = Join-Path $repo 'src/Companion.Desktop/bin/Release/net10.0-windows'
$executable = Join-Path $appDirectory 'Companion.Desktop.exe'
$binaries = [ordered]@{}
foreach ($name in @('Companion.Desktop.exe','Companion.Desktop.dll','Companion.Application.dll','Companion.Infrastructure.dll','Companion.Core.dll','System.Speech.dll')) {
    $binaries[$name] = (Get-FileHash -LiteralPath (Join-Path $appDirectory $name) -Algorithm SHA256).Hash
}
[ordered]@{ startedUtc=[DateTime]::UtcNow.ToString('o'); playback=[bool]$Playback; binaries=$binaries;
    sourceCheckpoint=(& git -C $repo rev-parse HEAD); protocolSha256=(Get-FileHash -LiteralPath (Join-Path $repo 'docs/qa/PHASE_4A_SPEECH_REVIEW.md')).Hash;
    maximumSeconds=120; sourceState='Working tree speech increment; exact executed binary hashes retained.' } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot 'plan.json') -Encoding utf8
$process = New-Object Diagnostics.Process
$process.StartInfo.FileName = $executable
$process.StartInfo.UseShellExecute = $false
$process.StartInfo.CreateNoWindow = $true
$process.StartInfo.Arguments = '--speech-smoke --data-dir "' + $profile + '"'
if ($Playback) { $process.StartInfo.Arguments += ' --speech-playback' }
$started = $false
$exitCode = -1
$timedOut = $false
try {
    $started = $process.Start()
    if (-not $started) { throw 'Speech diagnostic did not start.' }
    if (-not $process.WaitForExit(120000)) { $timedOut=$true; $process.Kill(); $process.WaitForExit(5000) | Out-Null }
    $exitCode = $process.ExitCode
}
finally {
    if ($started -and -not $process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
    [ordered]@{ exitCode=$exitCode; timedOut=$timedOut; endedUtc=[DateTime]::UtcNow.ToString('o') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runRoot 'execution.json') -Encoding utf8
    $process.Dispose()
}
Write-Output "Speech evidence: $runRoot"
if ($timedOut -or $exitCode -ne 0) { throw 'Speech diagnostic failed; the original outcome is retained.' }
$report = Get-Content -LiteralPath (Join-Path $profile 'speech-output-smoke.json') -Raw | ConvertFrom-Json
if ($report.status -ne 'PASS' -or @($report.failures).Count -ne 0) { throw 'Speech result reports a failure.' }
Write-Output 'PASS native speech engine diagnostic. Physical hearing and device qualification are separate.'
