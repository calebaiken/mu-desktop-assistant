[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedProtocolSha256,
    [switch]$FinalBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$protocolName = if ($FinalBuild) { 'PHASE_2_FINAL_BUILD_PROTOCOL.md' } else { 'PHASE_2_OBSERVER_PROTOCOL.md' }
$protocolPath = Join-Path $workspaceRoot ('docs\qa\' + $protocolName)
$measurementScript = Join-Path $PSScriptRoot 'measure-desktop.ps1'
$qaDirectory = Join-Path $workspaceRoot 'artifacts\qa'
$binaryDirectory = Join-Path $workspaceRoot "src\Companion.Desktop\bin\$Configuration\net10.0-windows"
$binaryNames = @('Companion.Desktop.exe', 'Companion.Desktop.dll', 'Companion.Application.dll',
    'Companion.Core.dll', 'Companion.Infrastructure.dll')
$expectedBudgetFailure = 'Resource budget failed: every interval must remain strictly below 1% of one logical processor and 150,000,000 private bytes. Raw measurements are retained; no overhead was subtracted.'
$experiment = if ($FinalBuild) { 'final-build' } else { 'observer' }
$comparisonDirectory = Join-Path $qaDirectory ($experiment + '-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $comparisonDirectory | Out-Null
$comparisonPath = Join-Path $comparisonDirectory 'comparison.json'

# Declare the complete sequence before any measurement. A is descriptive; both B
# replicates must pass. Valid failures are retained and never replaced by another run.
$plan = if ($FinalBuild) { @(
    [pscustomobject]@{ index = 1; condition = 'B'; observer = 'process' },
    [pscustomobject]@{ index = 2; condition = 'B'; observer = 'process' }
) } else { @(
    [pscustomobject]@{ index = 1; condition = 'A'; observer = 'threads' },
    [pscustomobject]@{ index = 2; condition = 'B'; observer = 'process' },
    [pscustomobject]@{ index = 3; condition = 'B'; observer = 'process' },
    [pscustomobject]@{ index = 4; condition = 'A'; observer = 'threads' }
) }
$comparison = [ordered]@{
    schemaVersion = 1
    experiment = $experiment
    startedAt = [DateTimeOffset]::UtcNow.ToString('O')
    completedAt = $null
    configuration = $Configuration
    protocolPath = $protocolPath
    expectedProtocolSha256 = $ExpectedProtocolSha256.ToUpperInvariant()
    protocolSha256 = $null
    measurementScriptSha256 = $null
    binaryHashes = @()
    plan = $plan
    acceptance = "All $($plan.Count) declared runs must be valid and completed; both process-only B runs must pass every fixed CPU/private-byte interval. Any A budget failures remain valid observations."
    cpuLimitExclusiveOneCorePercent = 1.0
    privateBytesLimitExclusive = 150000000
    overheadSubtracted = $false
    status = 'planned'
    allDeclaredRunsValid = $false
    bothProcessRunsWithinBudget = $null
    runs = @()
    failure = $null
}

function Save-Comparison {
    $json = $comparison | ConvertTo-Json -Depth 32
    [System.IO.File]::WriteAllText($comparisonPath, $json, [System.Text.UTF8Encoding]::new($false))
}

function Get-BuildHashes {
    foreach ($name in $binaryNames) {
        $hash = Get-FileHash -LiteralPath (Join-Path $binaryDirectory $name) -Algorithm SHA256
        [pscustomobject]@{ Algorithm = $hash.Algorithm; Hash = $hash.Hash; File = $name }
    }
}

function Assert-BuildHashes {
    param([object[]]$Candidate, [string]$Context)
    if ($Candidate.Count -ne $binaryNames.Count) { throw "$Context does not contain exactly five binary hashes." }
    foreach ($expected in $comparison.binaryHashes) {
        $matching = @($Candidate | Where-Object { $_.File -ceq $expected.File })
        if ($matching.Count -ne 1 -or $matching[0].Algorithm -cne 'SHA256' -or
            $matching[0].Hash -cne $expected.Hash) {
            throw "$Context differs from the frozen build for $($expected.File)."
        }
    }
}

function Assert-FrozenInputs {
    $protocolHash = (Get-FileHash -LiteralPath $protocolPath -Algorithm SHA256).Hash
    if ($protocolHash -cne $comparison.expectedProtocolSha256) {
        throw 'The observer protocol differs from its explicitly supplied frozen SHA256.'
    }
    $scriptHash = (Get-FileHash -LiteralPath $measurementScript -Algorithm SHA256).Hash
    if ($scriptHash -cne $comparison.measurementScriptSha256) {
        throw 'The measurement supervisor changed during the declared comparison.'
    }
    Assert-BuildHashes -Candidate @(Get-BuildHashes) -Context 'Current on-disk build'
}

function Get-PerformanceProfiles {
    @(Get-ChildItem -LiteralPath $qaDirectory -Directory -Filter 'performance-*' | ForEach-Object { $_.FullName })
}

function Assert-BudgetArtifact {
    param($Budget, $Report, [string]$Observer)
    if ($Budget.schemaVersion -ne 1 -or $Budget.cpuLimitExclusiveOneCorePercent -ne 1.0 -or
        $Budget.privateBytesLimitExclusive -ne 150000000 -or $Budget.overheadSubtracted -isnot [bool] -or
        $Budget.overheadSubtracted -or $Budget.allIntervalsWithinBudget -isnot [bool] -or
        $Budget.observerMode -cne $Observer -or $Report.observerMode -cne $Observer -or
        @($Budget.samples).Count -ne 3 -or @($Report.samples).Count -ne 3) {
        throw 'Budget artifact does not match the frozen resource policy or observer.'
    }
    $modes = @('visible-idle', 'visible-reduced-motion', 'hidden')
    $allPassed = $true
    for ($sampleIndex = 0; $sampleIndex -lt 3; $sampleIndex++) {
        $budgetSample = $Budget.samples[$sampleIndex]
        $reportSample = $Report.samples[$sampleIndex]
        if ($budgetSample.mode -cne $modes[$sampleIndex] -or $reportSample.mode -cne $modes[$sampleIndex] -or
            $budgetSample.cpuBudgetPass -isnot [bool] -or $budgetSample.privateBytesBudgetPass -isnot [bool] -or
            $budgetSample.oneCorePercent -ne $reportSample.oneCorePercent -or
            $budgetSample.peakPrivateBytes -ne $reportSample.privateBytesPeak -or
            $budgetSample.cpuBudgetPass -ne ([double]$reportSample.oneCorePercent -lt 1.0) -or
            $budgetSample.privateBytesBudgetPass -ne ([long]$reportSample.privateBytesPeak -lt 150000000)) {
            throw 'Budget artifact does not correspond to the complete performance report.'
        }
        if (-not $budgetSample.cpuBudgetPass -or -not $budgetSample.privateBytesBudgetPass) { $allPassed = $false }
    }
    if ($Budget.allIntervalsWithinBudget -ne $allPassed) { throw 'Budget aggregate disagrees with its interval results.' }
}

Save-Comparison
Write-Output "Declared $experiment sequence ($($plan.observer -join ', ')): $comparisonPath"
$activeRun = $null
try {
    $comparison.protocolSha256 = (Get-FileHash -LiteralPath $protocolPath -Algorithm SHA256).Hash
    if ($comparison.protocolSha256 -cne $comparison.expectedProtocolSha256) {
        throw 'The observer protocol differs from its explicitly supplied frozen SHA256.'
    }
    $comparison.measurementScriptSha256 = (Get-FileHash -LiteralPath $measurementScript -Algorithm SHA256).Hash
    $comparison.binaryHashes = @(Get-BuildHashes)
    $comparison.status = 'running'
    Save-Comparison

    foreach ($step in $plan) {
        $activeRun = [ordered]@{
            index = $step.index
            condition = $step.condition
            observer = $step.observer
            startedAt = [DateTimeOffset]::UtcNow.ToString('O')
            completedAt = $null
            status = 'running'
            newProfiles = @()
            removedProfiles = @()
            profile = $null
            outputLog = Join-Path $comparisonDirectory ("run-$($step.index)-$($step.observer).log")
            protocolSha256 = $null
            binaryHashes = @()
            reportPath = $null
            budgetPath = $null
            report = $null
            budget = $null
            childError = $null
            failure = $null
        }
        $comparison.runs += $activeRun
        Save-Comparison
        Assert-FrozenInputs
        $beforeProfiles = @(Get-PerformanceProfiles)
        $childFailure = $null
        Write-Output "Starting run $($step.index)/$($plan.Count): condition $($step.condition), observer $($step.observer)."
        try {
            # Call in a child script scope. Do not reinterpret LASTEXITCODE: the
            # existing supervisor signals its guarded budget result by exception.
            & $measurementScript -Configuration $Configuration -Observer $step.observer *>&1 |
                Tee-Object -FilePath $activeRun.outputLog
        }
        catch {
            $childFailure = $_
            $activeRun.childError = [ordered]@{
                type = $_.Exception.GetType().FullName
                message = $_.Exception.Message
                fullyQualifiedErrorId = $_.FullyQualifiedErrorId
            }
        }

        $afterProfiles = @(Get-PerformanceProfiles)
        $activeRun.newProfiles = @($afterProfiles | Where-Object { $_ -notin $beforeProfiles })
        $activeRun.removedProfiles = @($beforeProfiles | Where-Object { $_ -notin $afterProfiles })
        if ($activeRun.newProfiles.Count -ne 1 -or $activeRun.removedProfiles.Count -ne 0) {
            throw 'Expected exactly one fresh performance profile and no removed profiles; the sequence is incomplete.'
        }
        $activeRun.profile = $activeRun.newProfiles[0]
        $activeRun.reportPath = Join-Path $activeRun.profile 'performance-smoke.json'
        $activeRun.budgetPath = Join-Path $activeRun.profile 'budget-evaluation.json'
        $activeRun.binaryHashes = @(Get-Content -LiteralPath (Join-Path $activeRun.profile 'binary-hashes.json') -Raw | ConvertFrom-Json)
        Assert-BuildHashes -Candidate $activeRun.binaryHashes -Context "Run $($step.index) recorded build"
        Assert-FrozenInputs
        $activeRun.protocolSha256 = (Get-FileHash -LiteralPath $protocolPath -Algorithm SHA256).Hash
        $activeRun.report = Get-Content -LiteralPath $activeRun.reportPath -Raw | ConvertFrom-Json
        # This file is emitted only after the child supervisor's scenario,
        # contamination, observer and numerical guards have all completed.
        $activeRun.budget = Get-Content -LiteralPath $activeRun.budgetPath -Raw | ConvertFrom-Json
        Assert-BudgetArtifact -Budget $activeRun.budget -Report $activeRun.report -Observer $step.observer
        $budgetPassed = $activeRun.budget.allIntervalsWithinBudget
        if ($null -ne $childFailure) {
            if ($childFailure.Exception.Message -cne $expectedBudgetFailure -or $budgetPassed) {
                throw "Run $($step.index) had a technical failure, not the expected guarded resource-budget failure."
            }
        }
        elseif (-not $budgetPassed) {
            throw "Run $($step.index) recorded a failed budget without the expected supervisor failure."
        }
        $activeRun.status = if ($budgetPassed) { 'valid-budget-pass' } else { 'valid-budget-fail' }
        $activeRun.completedAt = [DateTimeOffset]::UtcNow.ToString('O')
        Save-Comparison
        Write-Output "Retained run $($step.index): $($activeRun.status); profile $($activeRun.profile)"
        $activeRun = $null
    }

    Assert-FrozenInputs
    $comparison.allDeclaredRunsValid = $comparison.runs.Count -eq $plan.Count
    $processRuns = @($comparison.runs | Where-Object { $_.observer -ceq 'process' })
    $comparison.bothProcessRunsWithinBudget = $processRuns.Count -eq 2 -and
        @($processRuns | Where-Object { -not $_.budget.allIntervalsWithinBudget }).Count -eq 0
    $comparison.status = if ($comparison.bothProcessRunsWithinBudget) { 'complete-process-replicates-pass' } else { 'complete-process-replicates-fail' }
    $comparison.completedAt = [DateTimeOffset]::UtcNow.ToString('O')
    Save-Comparison
}
catch {
    $failure = [ordered]@{ type = $_.Exception.GetType().FullName; message = $_.Exception.Message }
    if ($null -ne $activeRun) {
        $activeRun.status = 'technical-failure'
        $activeRun.failure = $failure
        $activeRun.completedAt = [DateTimeOffset]::UtcNow.ToString('O')
    }
    $comparison.status = 'incomplete-technical-failure'
    $comparison.failure = $failure
    $comparison.completedAt = [DateTimeOffset]::UtcNow.ToString('O')
    Save-Comparison
    Write-Output "INCOMPLETE: $($failure.message)"
    Write-Output "All available evidence retained: $comparisonPath"
    exit 1
}

$comparison.runs | ForEach-Object {
    [pscustomobject]@{
        Run = $_.index
        Condition = $_.condition
        Observer = $_.observer
        Status = $_.status
        Profile = $_.profile
    }
} | Format-Table -AutoSize
Write-Output "Comparison result: $($comparison.status)"
Write-Output "Complete evidence: $comparisonPath"
Write-Output 'This comparison neither subtracts observer cost nor establishes statistical significance, product optimization or Phase 2 approval.'
if (-not $comparison.bothProcessRunsWithinBudget) { exit 1 }
exit 0
