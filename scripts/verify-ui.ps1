[CmdletBinding()]
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$appPath = Join-Path $workspaceRoot "src\Companion.Desktop\bin\$Configuration\net10.0-windows\Companion.Desktop.exe"
if (-not (Test-Path -LiteralPath $appPath -PathType Leaf)) { throw 'Build the desktop project before running UI process checks.' }
$runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$evidenceRoot = Join-Path $workspaceRoot "artifacts\qa\phase1-ui-$runId"
$profile = Join-Path $evidenceRoot 'profile'
New-Item -ItemType Directory -Path $profile -Force | Out-Null
$script:caseCount = 0

function Assert-AppExit {
    param([string]$Name, [string]$Arguments, [int]$ExpectedExit)
    $script:caseCount++
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo.FileName = $appPath
    $process.StartInfo.Arguments = $Arguments
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.CreateNoWindow = $true
    try {
        if (-not $process.Start()) { throw "$Name did not start." }
        if (-not $process.WaitForExit(15000)) {
            $process.Kill()
            $process.WaitForExit()
            throw "$Name exceeded its 15-second deadline."
        }
        if ($process.ExitCode -ne $ExpectedExit) {
            throw "$Name returned $($process.ExitCode); expected $ExpectedExit."
        }
        Write-Output "PASS $Name (exit $($process.ExitCode))"
    }
    finally { $process.Dispose() }
}

# --ui-smoke executes real production initialization, HWND creation, settings-window
# creation and window closing. It does not simulate user input or prove accessibility.
$profileArgument = '--data-dir "{0}"' -f $profile
Assert-AppExit 'visible-companion-settings-close' "--ui-smoke $profileArgument" 0
Assert-AppExit 'same-profile-restart-after-close' "--ui-smoke $profileArgument" 0
Assert-AppExit 'headless-composed-lifecycle' "--smoke $profileArgument" 0
Assert-AppExit 'reject-forward-slash-unc' '--smoke --data-dir "//invalid-host.invalid/share"' 2
Assert-AppExit 'reject-backslash-unc' '--smoke --data-dir "\\invalid-host.invalid\share"' 2
Assert-AppExit 'reject-relative-data-path' '--smoke --data-dir "relative-profile"' 2
Assert-AppExit 'reject-conflicting-smoke-flags' "--smoke --ui-smoke $profileArgument" 2
Assert-AppExit 'reject-duplicate-data-flag' "$profileArgument $profileArgument --smoke" 2

$logPath = Join-Path $profile 'logs\application.jsonl'
$events = @(Get-Content -LiteralPath $logPath | ForEach-Object { $_ | ConvertFrom-Json })
if (@($events | Where-Object code -eq 'Started').Count -ne 3 -or
    @($events | Where-Object code -eq 'Stopping').Count -ne 3) {
    throw 'Three actual composed lifecycles must each record Started and Stopping.'
}
$allowedFields = @('schemaVersion', 'timestamp', 'area', 'code', 'operationId', 'durationMs')
foreach ($entry in $events) {
    foreach ($property in $entry.PSObject.Properties.Name) {
        if ($property -notin $allowedFields) { throw "Unexpected log payload field: $property" }
    }
}
$script:caseCount++
Write-Output 'PASS composed-lifecycle-logs-and-closed-payload-schema'
Write-Output "$script:caseCount/$script:caseCount UI process/control checks passed."
Write-Output "Evidence profile retained at $profile"
Write-Output 'Interactive name/save/restart/activation/Exit requires the documented real-window QA scenario.'
