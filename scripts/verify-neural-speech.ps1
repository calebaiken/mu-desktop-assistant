param(
    [string]$ApplicationPath = (Join-Path $PSScriptRoot '../artifacts/Mu/Companion.Desktop.exe'),
    [switch]$Playback
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$app = [IO.Path]::GetFullPath($ApplicationPath)
if (-not (Test-Path -LiteralPath $app -PathType Leaf)) { throw 'Build the current Mu executable first.' }
$profile = Join-Path $repo ('artifacts/qa/neural-speech-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($profile) | Out-Null
$process = [Diagnostics.Process]::new()
$process.StartInfo.FileName = $app
$process.StartInfo.UseShellExecute = $false
$process.StartInfo.CreateNoWindow = $true
$process.StartInfo.Arguments = '--neural-speech-smoke --ml-root "' + $repo + '" --data-dir "' + $profile + '"'
if ($Playback) { $process.StartInfo.Arguments += ' --neural-speech-playback' }
$started = $false
try {
    $started = $process.Start()
    if (-not $started) { throw 'The neural voice diagnostic could not start.' }
    if (-not $process.WaitForExit(650000)) { $process.Kill(); $process.WaitForExit(5000) | Out-Null; throw "Neural voice aggregate deadline exceeded. Evidence: $profile" }
    if ($process.ExitCode -ne 0) { throw "Neural voice diagnostic failed (exit $($process.ExitCode)). Evidence: $profile" }
    $report = Get-Content -LiteralPath (Join-Path $profile 'neural-speech-smoke.json') -Raw | ConvertFrom-Json
    $expected = if ($Playback) { 25 } else { 21 }
    if ($report.failure -or $report.count -ne $expected -or @($report.passed | Select-Object -Unique).Count -ne $expected) { throw 'Neural voice diagnostic returned incomplete evidence.' }
    Write-Output "PASS $expected actual host/native neural speech checks. Evidence: $profile"
} finally {
    if ($started -and -not $process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
    $process.Dispose()
}
