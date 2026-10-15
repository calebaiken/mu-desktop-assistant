param([string]$ApplicationPath = (Join-Path $PSScriptRoot '../artifacts/Mu/Companion.Desktop.exe'))
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$app = [IO.Path]::GetFullPath($ApplicationPath)
if (-not (Test-Path -LiteralPath $app -PathType Leaf)) { throw 'Build the current Mu desktop candidate first.' }
$profile = Join-Path $repo ('artifacts/qa/laya-runtime-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($profile) | Out-Null
$process = [Diagnostics.Process]::new()
$process.StartInfo.FileName = $app
$process.StartInfo.Arguments = '--laya-smoke --ml-root "' + $repo + '" --data-dir "' + $profile + '"'
$process.StartInfo.UseShellExecute = $false
$process.StartInfo.CreateNoWindow = $true
try {
    if (-not $process.Start()) { throw 'The LAYA diagnostic could not start.' }
    # Two individually bounded two-minute requests and native startup/cleanup.
    if (-not $process.WaitForExit(270000)) {
        $process.Kill(); $process.WaitForExit()
        throw "LAYA diagnostics exceeded the aggregate deadline. Evidence: $profile"
    }
    if ($process.ExitCode -ne 0) { throw "LAYA diagnostics failed (exit $($process.ExitCode)). Evidence: $profile" }
    $report = Get-Content -LiteralPath (Join-Path $profile 'laya-runtime-smoke.json') -Raw | ConvertFrom-Json
    if ($report.failure -or $report.count -ne 16 -or @($report.passed | Select-Object -Unique).Count -ne 16 -or @($report.observations).Count -ne 2) { throw 'LAYA diagnostics returned incomplete evidence.' }
    Write-Output "PASS 16 actual host/native LAYA checks. Evidence: $profile"
} finally { $process.Dispose() }
