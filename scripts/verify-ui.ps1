[CmdletBinding()]
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$appPath = Join-Path $workspaceRoot "src\Companion.Desktop\bin\$Configuration\net10.0-windows\Companion.Desktop.exe"
if (-not (Test-Path -LiteralPath $appPath -PathType Leaf)) { throw 'Build the desktop project before running UI process checks.' }
$runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$evidenceRoot = Join-Path $workspaceRoot "artifacts\qa\desktop-ui-$runId"
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
        if (-not $process.WaitForExit(40000)) {
            $process.Kill()
            $process.WaitForExit()
            throw "$Name exceeded its 40-second deadline."
        }
        if ($process.ExitCode -ne $ExpectedExit) {
            throw "$Name returned $($process.ExitCode); expected $ExpectedExit."
        }
        Write-Output "PASS $Name (exit $($process.ExitCode))"
    }
    finally { $process.Dispose() }
}

# --ui-smoke executes real production initialization, HWND creation, settings-window
# creation, appearance/placement assertions and window closing. It does not simulate
# user input or prove accessibility, dragging or a physical mixed-DPI monitor matrix.
$profileArgument = '--data-dir "{0}"' -f $profile
Assert-AppExit 'visible-companion-settings-close' "--ui-smoke $profileArgument" 0
Assert-AppExit 'same-profile-restart-after-close' "--ui-smoke $profileArgument" 0
$appearance = Get-Content -LiteralPath (Join-Path $profile 'appearance-smoke.json') -Raw | ConvertFrom-Json
if ('blinkChecksEnabled' -notin $appearance.PSObject.Properties.Name -or
    'skipped' -notin $appearance.PSObject.Properties.Name) {
    throw 'Appearance evidence must declare whether OS motion policy enabled blink checks and list skips.'
}
$expectedAppearanceCount = if ($appearance.blinkChecksEnabled) { 43 } else { 38 }
$blinkAssertions = @('idle-blink-closes-without-animation-clocks', 'interrupted-blink-does-not-count',
    'idle-blink-completes', 'speaking-owns-two-clocks', 'idle-releases-speaking-clocks')
if ($appearance.count -ne $expectedAppearanceCount -or
    @($appearance.passed).Count -ne $expectedAppearanceCount -or
    @($appearance.passed | Select-Object -Unique).Count -ne $expectedAppearanceCount) {
    throw "Expected $expectedAppearanceCount distinct real-window appearance, state, placement and animation assertions."
}
foreach ($assertion in $blinkAssertions) {
    if (($assertion -in $appearance.passed) -ne [bool]$appearance.blinkChecksEnabled) {
        throw "Conditional animation assertion disagrees with the recorded motion policy: $assertion"
    }
}
if ('hidden-releases-animation-resources' -notin $appearance.passed -or
    ($appearance.blinkChecksEnabled -and @($appearance.skipped).Count -ne 0) -or
    (-not $appearance.blinkChecksEnabled -and @($appearance.skipped).Count -ne 1)) {
    throw 'Appearance evidence has incomplete animation resource coverage or inconsistent skip metadata.'
}
$script:caseCount++
Write-Output "PASS $expectedAppearanceCount real-window appearance-state-placement-animation assertions"
foreach ($limit in $appearance.skipped) { Write-Output "LIMIT: $limit" }
$integration = Get-Content -LiteralPath (Join-Path $profile 'desktop-integration-smoke.json') -Raw | ConvertFrom-Json
foreach ($field in @('registeredShortcut', 'alternateShortcut', 'skippedChecks', 'skippedCount', 'shortcutAttempts')) {
    if ($field -notin $integration.PSObject.Properties.Name) { throw "Missing recovery evidence: $field" }
}
$expectedIntegrationCount = if ($null -eq $integration.registeredShortcut) { 15 } elseif ($null -eq $integration.alternateShortcut) { 23 } else { 24 }
$expectedSkippedCount = if ($null -eq $integration.registeredShortcut) { 11 } elseif ($null -eq $integration.alternateShortcut) { 1 } else { 0 }
if ($integration.failure -or @($integration.adapterErrors).Count -gt 0 -or
    $integration.count -ne $expectedIntegrationCount -or
    @($integration.passed | Select-Object -Unique).Count -ne $expectedIntegrationCount -or
    $integration.skippedCount -ne $expectedSkippedCount -or
    @($integration.skippedChecks | Select-Object -Unique).Count -ne $expectedSkippedCount) {
    throw 'Desktop integration evidence is incomplete or reports an adapter failure.'
}
foreach ($assertion in @('tray-visibility-alone-is-not-confirmed-recovery',
    'tray-only-click-through-refused', 'tray-only-hide-refused',
    'tray-only-click-through-refusal-preserves-native-style-and-visibility',
    'tray-only-hide-refusal-preserves-native-style-and-visibility')) {
    if ($assertion -notin $integration.passed) { throw "Missing required recovery guard: $assertion" }
}
$script:caseCount++
Write-Output "PASS $($integration.count) desktop recovery/tray/shortcut assertions"
foreach ($limit in $integration.skipped) { Write-Output "LIMIT: $limit" }
$stages = @(Get-Content -LiteralPath (Join-Path $profile 'ui-stages.txt'))
$compactAvatar = Get-Content -LiteralPath (Join-Path $profile 'compact-avatar-smoke.json') -Raw | ConvertFrom-Json
if ($compactAvatar.failure -or $compactAvatar.count -ne 51 -or
    @($compactAvatar.passed | Select-Object -Unique).Count -ne 51 -or
    @($stages | Where-Object { $_ -like 'compact-avatar-complete:*' }).Count -ne 2) {
    throw 'Both native runs must complete all 51 compact avatar layout and disclosure-policy assertions.'
}
$script:caseCount++
Write-Output 'PASS 51 compact avatar layout/disclosure-policy assertions'
if (@($stages | Where-Object { $_ -like 'settings-recovery-complete:*' }).Count -ne 2) {
    throw 'Both visible runs must recover hidden and minimized settings.'
}
$script:caseCount++
Write-Output 'PASS hidden-and-minimized-settings-recovery'
$settingsLifecycle = Get-Content -LiteralPath (Join-Path $profile 'settings-lifecycle-smoke.json') -Raw | ConvertFrom-Json
if ($settingsLifecycle.failure -or $settingsLifecycle.count -ne 29 -or
    @($settingsLifecycle.passed | Select-Object -Unique).Count -ne 29 -or
    @($stages | Where-Object { $_ -like 'settings-lifecycle-complete:*' }).Count -ne 2) {
    throw 'Both native runs must complete all 29 save/shutdown lifecycle assertions.'
}
$script:caseCount++
Write-Output 'PASS 29 settings-save-shutdown lifecycle assertions'
$placementInitialization = Get-Content -LiteralPath (Join-Path $profile 'placement-initialization-smoke.json') -Raw | ConvertFrom-Json
if ($placementInitialization.failure -or $placementInitialization.count -ne 49 -or
    @($placementInitialization.passed | Select-Object -Unique).Count -ne 49 -or
    @($stages | Where-Object { $_ -like 'placement-initialization-complete:*' }).Count -ne 2) {
    throw 'Both native runs must complete all 49 placement initialization assertions.'
}
$script:caseCount++
Write-Output 'PASS 49 placement initialization assertions'
$chatLifecycle = Get-Content -LiteralPath (Join-Path $profile 'chat-lifecycle-smoke.json') -Raw | ConvertFrom-Json
if ($chatLifecycle.failure -or $chatLifecycle.count -ne 54 -or
    @($chatLifecycle.passed | Select-Object -Unique).Count -ne 54 -or
    @($stages | Where-Object { $_ -like 'chat-lifecycle-complete:*' }).Count -ne 2) {
    throw 'Both native runs must complete all 54 chat lifecycle, history and layout assertions.'
}
$script:caseCount++
Write-Output 'PASS 54 local chat lifecycle/history/layout assertions'
Assert-AppExit 'headless-composed-lifecycle' "--smoke $profileArgument" 0
Assert-AppExit 'reject-forward-slash-unc' '--smoke --data-dir "//invalid-host.invalid/share"' 2
Assert-AppExit 'reject-backslash-unc' '--smoke --data-dir "\\invalid-host.invalid\share"' 2
Assert-AppExit 'reject-relative-data-path' '--smoke --data-dir "relative-profile"' 2
Assert-AppExit 'reject-conflicting-smoke-flags' "--smoke --ui-smoke $profileArgument" 2
Assert-AppExit 'reject-duplicate-data-flag' "$profileArgument $profileArgument --smoke" 2
Assert-AppExit 'reject-observer-without-performance-mode' "--smoke --performance-observer process $profileArgument" 2
Assert-AppExit 'reject-unknown-observer' "--performance-smoke --performance-observer unknown $profileArgument" 2
Assert-AppExit 'reject-missing-observer-value' '--performance-smoke --performance-observer' 2
Assert-AppExit 'reject-duplicate-observer' "--performance-smoke --performance-observer threads --performance-observer process $profileArgument" 2

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
