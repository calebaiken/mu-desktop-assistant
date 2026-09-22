[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [ValidateSet('threads', 'process')][string]$Observer = 'threads'
)
$ErrorActionPreference = 'Stop'
$Observer = $Observer.ToLowerInvariant()
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$appPath = Join-Path $workspaceRoot "src\Companion.Desktop\bin\$Configuration\net10.0-windows\Companion.Desktop.exe"
if (-not (Test-Path -LiteralPath $appPath -PathType Leaf)) { throw 'Build the selected desktop configuration before measuring.' }
$runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$profile = Join-Path $workspaceRoot "artifacts\qa\performance-$runId"
New-Item -ItemType Directory -Path $profile -Force | Out-Null

$reference = [ordered]@{
    configuration = $Configuration
    capturedAt = [DateTimeOffset]::UtcNow.ToString('O')
    logicalProcessorCount = [Environment]::ProcessorCount
    computerNameRecorded = $false
}
try {
    $reference.processorModel = @(Get-CimInstance Win32_Processor -OperationTimeoutSec 5 | ForEach-Object { $_.Name.Trim() })
    $reference.physicalMemoryBytes = (Get-CimInstance Win32_ComputerSystem -OperationTimeoutSec 5).TotalPhysicalMemory
}
catch { $reference.hardwareMetadataErrorType = $_.Exception.GetType().FullName }
$reference | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $profile 'reference-hardware.json') -Encoding UTF8

# Identify the exact selected build before launch, even if source changes during a run.
# Sampler/diagnostic methods are recorded by that binary in performance-smoke.json;
# do not infer a compiled method from the current source tree.
$binaryDirectory = Split-Path -Parent $appPath
$binaryHashes = @('Companion.Desktop.exe', 'Companion.Desktop.dll', 'Companion.Application.dll',
    'Companion.Core.dll', 'Companion.Infrastructure.dll') | ForEach-Object {
    $binaryPath = Join-Path $binaryDirectory $_
    $binaryHash = Get-FileHash -LiteralPath $binaryPath -Algorithm SHA256
    [pscustomobject]@{ Algorithm = $binaryHash.Algorithm; Hash = $binaryHash.Hash; File = $_ }
}
$binaryHashes | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $profile 'binary-hashes.json') -Encoding UTF8

$process = New-Object System.Diagnostics.Process
$process.StartInfo.FileName = $appPath
$process.StartInfo.Arguments = '--performance-smoke --performance-observer ' + $Observer + ' --data-dir "' + $profile + '"'
$process.StartInfo.UseShellExecute = $false
$process.StartInfo.CreateNoWindow = $true
Write-Output 'Measuring isolated companion: 10-second warmup and three 60-second process-counter intervals.'
Write-Output "Evidence profile: $profile"
Write-Output "Observer mode: $Observer"
try {
    if (-not $process.Start()) { throw 'Performance harness could not start.' }
    $elapsed = [System.Diagnostics.Stopwatch]::StartNew()
    $nextProgressSeconds = 30
    while (-not $process.WaitForExit(1000)) {
        if ($elapsed.Elapsed.TotalSeconds -ge 240) {
            $process.Kill()
            if (-not $process.WaitForExit(5000)) { throw 'Timed-out measurement process could not be stopped promptly.' }
            throw 'Performance harness exceeded its four-minute deadline.'
        }
        if ($elapsed.Elapsed.TotalSeconds -ge $nextProgressSeconds) {
            Write-Output "Measurement remains active ($([int]$elapsed.Elapsed.TotalSeconds) seconds elapsed)."
            $nextProgressSeconds += 30
        }
    }
    if ($process.ExitCode -ne 0) { throw "Performance harness failed with exit $($process.ExitCode)." }
}
finally { $process.Dispose() }

$reportPath = Join-Path $profile 'performance-smoke.json'
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
# This script always creates an empty profile, and the harness never saves preferences.
# A settings file therefore proves another interaction changed the measured scenario.
if (Test-Path -LiteralPath (Join-Path $profile 'settings.json')) {
    throw 'Measurement contaminated by a settings save during the run. Preserve this profile, but do not use it as a controlled idle baseline.'
}
if ($report.schemaVersion -ne 1 -or $report.logicalProcessorCount -lt 1 -or @($report.samples).Count -ne 3) {
    throw 'Invalid performance report structure.'
}
if ($report.observerMode -cne $Observer -or $report.threadSnapshotCaptureAttempts -ne $(if ($Observer -eq 'threads') { 6 } else { 0 })) {
    throw 'Recorded observer mode or diagnostic capture count does not match this invocation.'
}
$expectedModes = @('visible-idle', 'visible-reduced-motion', 'hidden')
for ($index = 0; $index -lt 3; $index++) {
    $sample = $report.samples[$index]
    foreach ($field in @('threadSnapshotCaptureAttempts', 'threadsAtStart', 'threadsAtEnd')) {
        if ($field -notin $sample.PSObject.Properties.Name) { throw "Missing observer evidence: $field" }
    }
    if ($Observer -eq 'threads') {
        if ($sample.threadSnapshotCaptureAttempts -ne 2 -or $null -eq $sample.threadsAtStart -or $null -eq $sample.threadsAtEnd -or
            @($sample.threadsAtStart).Count -eq 0 -or @($sample.threadsAtEnd).Count -eq 0) {
            throw 'Thread diagnostic interval lacks its two nonempty snapshots.'
        }
    }
    elseif ($sample.threadSnapshotCaptureAttempts -ne 0 -or $null -ne $sample.threadsAtStart -or $null -ne $sample.threadsAtEnd) {
        throw 'Process-only interval unexpectedly captured thread diagnostics.'
    }
    if ($sample.mode -ne $expectedModes[$index] -or $sample.elapsedSeconds -lt 60 -or $sample.cpuSeconds -lt 0) {
        throw 'Incomplete or invalid measurement interval.'
    }
    # Recompute exported percentages independently in the supervising process.
    $expectedOneCore = 100.0 * [double]$sample.cpuSeconds / [double]$sample.elapsedSeconds
    $expectedMachine = $expectedOneCore / [int]$report.logicalProcessorCount
    if ([Math]::Abs($expectedOneCore - [double]$sample.oneCorePercent) -gt 0.000001 -or
        [Math]::Abs($expectedMachine - [double]$sample.machinePercent) -gt 0.000001) {
        throw 'Exported CPU percentage does not match the recorded counters.'
    }
    if ($sample.privateBytesPeak -lt $sample.privateBytesStart -or $sample.privateBytesPeak -lt $sample.privateBytesEnd) {
        throw 'Private-byte peak does not cover measured endpoints.'
    }
    if ($sample.mode -ne 'visible-idle' -and $sample.animationObservedEnabled) {
        throw 'Reduced-motion or hidden interval observed animation and is not a valid controlled scenario.'
    }
    if ($sample.mode -eq 'visible-idle' -and $report.windowsAnimationsEnabled -and -not $report.highContrast -and
        $sample.animationObservedDisabled) {
        throw 'Visible-idle animation unexpectedly stopped during the controlled interval.'
    }
    foreach ($field in @('completedIdleBlinks', 'idleTimerObservedEnabled', 'ownedAnimationClockPeak')) {
        if ($field -notin $sample.PSObject.Properties.Name) { throw "Missing animation resource evidence: $field" }
    }
    if ($sample.completedIdleBlinks -lt 0 -or $sample.ownedAnimationClockPeak -ne 0) {
        throw 'Idle measurement reported invalid blink counts or unexpected owned animation clocks.'
    }
    $blinkExpected = $sample.mode -eq 'visible-idle' -and $report.windowsAnimationsEnabled -and -not $report.highContrast
    if ($blinkExpected -and ($sample.completedIdleBlinks -le 0 -or -not $sample.idleTimerObservedEnabled)) {
        throw 'Visible-idle measurement did not complete a blink with its idle timer enabled.'
    }
    if (-not $blinkExpected -and ($sample.completedIdleBlinks -ne 0 -or $sample.idleTimerObservedEnabled)) {
        throw 'Motion-disabled interval completed a blink or observed an enabled idle timer.'
    }
    $expectedVisible = $sample.mode -ne 'hidden'
    if ($sample.visibleAtStart -ne $expectedVisible -or $sample.visibleAtEnd -ne $expectedVisible -or
        $sample.windowStateAtStart -ne 'Normal' -or $sample.windowStateAtEnd -ne 'Normal') {
        throw 'Measured window visibility/state did not match the requested scenario.'
    }
}
$budgetSamples = @($report.samples | ForEach-Object {
    [pscustomobject]@{
        mode = $_.mode
        elapsedSeconds = $_.elapsedSeconds
        oneCorePercent = $_.oneCorePercent
        machinePercent = $_.machinePercent
        peakPrivateBytes = $_.privateBytesPeak
        cpuBudgetPass = ([double]$_.oneCorePercent -lt 1.0)
        privateBytesBudgetPass = ([long]$_.privateBytesPeak -lt 150000000)
        animationObservedEnabled = $_.animationObservedEnabled
        animationObservedDisabled = $_.animationObservedDisabled
        completedIdleBlinks = $_.completedIdleBlinks
        idleTimerObservedEnabled = $_.idleTimerObservedEnabled
        ownedAnimationClockPeak = $_.ownedAnimationClockPeak
    }
})
$budgetPassed = @($budgetSamples | Where-Object { -not $_.cpuBudgetPass -or -not $_.privateBytesBudgetPass }).Count -eq 0
[ordered]@{
    schemaVersion = 1
    cpuLimitExclusiveOneCorePercent = 1.0
    privateBytesLimitExclusive = 150000000
    overheadSubtracted = $false
    observerMode = $report.observerMode
    allIntervalsWithinBudget = $budgetPassed
    samples = $budgetSamples
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $profile 'budget-evaluation.json') -Encoding UTF8
$budgetSamples | Select-Object mode, completedIdleBlinks, oneCorePercent, peakPrivateBytes, cpuBudgetPass, privateBytesBudgetPass |
    Format-Table -AutoSize
if (-not $report.samples[0].animationObservedEnabled) {
    Write-Output 'LIMIT: visible-idle interval did not observe active animation; it does not establish animated-idle resource use.'
}
Write-Output 'Measurement and independent CPU arithmetic checks passed. This is one run, not a percentile benchmark or general performance certification.'
Write-Output "Report: $reportPath"
if (-not $budgetPassed) {
    throw 'Resource budget failed: every interval must remain strictly below 1% of one logical processor and 150,000,000 private bytes. Raw measurements are retained; no overhead was subtracted.'
}
Write-Output 'PASS all measured intervals are within the declared CPU and private-byte budgets.'
