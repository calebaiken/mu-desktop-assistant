[CmdletBinding()]
param([string]$SummaryPath = $env:GITHUB_STEP_SUMMARY)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($SummaryPath)) { throw 'A job summary output path is required.' }
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$lines = [System.Collections.Generic.List[string]]::new()
$shortNotices = [System.Collections.Generic.List[string]]::new()
$lines.Add('## Verification evidence')
$lines.Add('Reported observations only. Required verification steps determine success; missing evidence is not a pass.')
$reportPath = Join-Path $workspaceRoot 'artifacts/test-results/Release/tests.trx'
if ((Test-Path -LiteralPath $reportPath -PathType Leaf) -and (Get-Item -LiteralPath $reportPath).Length -le 8MB) {
    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.MaxCharactersInDocument = 8MB
    $reader = $null
    try {
        $reader = [System.Xml.XmlReader]::Create($reportPath, $settings)
        $report = [System.Xml.XmlDocument]::new()
        $report.XmlResolver = $null
        $report.Load($reader)
        $counter = $report.SelectSingleNode('//*[local-name()="Counters"]')
        if ($null -eq $counter) { throw 'Missing counters.' }
        $counts = @('total', 'executed', 'passed', 'failed') | ForEach-Object {
            $value = $counter.GetAttribute($_)
            if ($value -notmatch '^[0-9]{1,7}$') { throw 'Invalid counters.' }
            "${_}: $value"
        }
        $lines.Add('TRX ' + ($counts -join '; '))
        $methods = @{}
        foreach ($test in $report.SelectNodes('//*[local-name()="UnitTest"]')) {
            $method = $test.SelectSingleNode('*[local-name()="TestMethod"]')
            if ($null -eq $method) { continue }
            $name = $method.GetAttribute('className') + '.' + $method.GetAttribute('name')
            # Never publish DataRow values, test output, errors or stack traces.
            if ($name.Length -le 240 -and $name -match '^[A-Za-z0-9_.+]+$') { $methods[$test.GetAttribute('id')] = $name }
        }
        foreach ($outcome in @('Failed', 'NotExecuted')) {
            $results = @($report.SelectNodes('//*[local-name()="UnitTestResult"]') | Where-Object { $_.GetAttribute('outcome') -eq $outcome })
            $lines.Add("$outcome results: $($results.Count)")
            $names = @($results | ForEach-Object {
                $name = $methods[$_.GetAttribute('testId')]
                if ($null -ne $name) { $name } else { '[method unavailable]' }
            } | Sort-Object -Unique)
            foreach ($name in ($names | Select-Object -First 50)) { $lines.Add('- ' + $name) }
            if ($names.Count -gt 50) { $lines.Add('[method list truncated at 50]') }
        }
        foreach ($native in @(
            'Companion.Tests.IndependentResearchProjectTests.RealWindowsOwnedWorkerRoutesRequestsOnlyToSelectedProjectAndReloadsItsOwnMarker',
            'Companion.Tests.TextInspectionTests.WindowsRealPipelineRequiresConsentLocksIdentityAndRedactsPersistentAudit',
            'Companion.Tests.TextInspectionTests.WindowsOversizedFileIsRefusedBeforeConsentAndLeaseIsReleased',
            'Companion.Tests.TextInspectionTests.WindowsLeafAndAncestorLinksAreRefusedWithoutReadingTarget'
        )) {
            $results = @($report.SelectNodes('//*[local-name()="UnitTestResult"]') | Where-Object { $methods[$_.GetAttribute('testId')] -eq $native })
            if ($results.Count -eq 0) { $lines.Add('Native case missing: ' + $native); continue }
            $outcomes = @($results | ForEach-Object {
                $value = $_.GetAttribute('outcome')
                if ($value -notmatch '^[A-Za-z]{1,24}$') { throw 'Invalid native outcome.' }
                $value
            } | Sort-Object -Unique)
            $lines.Add('Native case: ' + $native + ' => ' + ($outcomes -join ', '))
        }
    }
    catch { $lines.Add('TRX details unavailable or invalid; no passing result inferred.') }
    finally { if ($null -ne $reader) { $reader.Dispose() } }
}
else { $lines.Add('TRX missing or exceeds 8 MiB; no test result inferred.') }

$smokeRoot = Join-Path $workspaceRoot 'artifacts/smoke'
$found = $false
if (Test-Path -LiteralPath $smokeRoot -PathType Container) {
    foreach ($directory in (Get-ChildItem -LiteralPath $smokeRoot -Directory | Select-Object -First 20)) {
        foreach ($receiptName in @('research-project-smoke.json', 'chat-project-handoff-smoke.json')) {
        $receipt = Join-Path $directory.FullName $receiptName
        if (-not (Test-Path -LiteralPath $receipt -PathType Leaf)) { continue }
        $found = $true
        try {
            if ((Get-Item -LiteralPath $receipt).Length -gt 8KB) { throw 'Oversized smoke receipt.' }
            $smoke = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
            if ($smoke.completed -isnot [bool]) { throw 'Invalid smoke completion.' }
            $label = if ($receiptName -eq 'chat-project-handoff-smoke.json') { 'Chat handoff' } else { 'Research' }
            $lines.Add($label + ' native control smoke completed: ' + $smoke.completed)
            if ($receiptName -eq 'chat-project-handoff-smoke.json' -and $null -ne $smoke.elapsedMilliseconds) {
                if ($smoke.elapsedMilliseconds -isnot [long] -and $smoke.elapsedMilliseconds -isnot [int]) { throw 'Invalid smoke duration.' }
                if ($smoke.elapsedMilliseconds -lt 0 -or $smoke.elapsedMilliseconds -gt 60000) { throw 'Invalid smoke duration.' }
                $lines.Add('Handoff smoke elapsed milliseconds: ' + $smoke.elapsedMilliseconds)
            }
            foreach ($field in @('failedCheck', 'failureType')) {
                $value = $smoke.$field
                if ($null -ne $value) {
                    if ($value -isnot [string] -or $value.Length -gt 120 -or $value -notmatch '^[A-Za-z0-9 .-]+$') { throw 'Invalid fixed smoke label.' }
                    $lines.Add("${field}: $value")
                }
            }
            $passed = @($smoke.passed)
            if ($passed.Count -gt 32) { throw 'Invalid checkpoint count.' }
            foreach ($checkpoint in $passed) {
                if ($checkpoint -isnot [string] -or $checkpoint.Length -gt 120 -or $checkpoint -notmatch '^[A-Za-z0-9 .-]+$') { throw 'Invalid fixed checkpoint.' }
                $lines.Add('- checkpoint: ' + $checkpoint)
            }
        }
        catch { $lines.Add('Native smoke receipt invalid; no passing result inferred.') }
        }
    }
}
if (-not $found) { $lines.Add('Native research smoke receipt missing; no native result inferred.') }
foreach ($captureMode in @('baseline', 'candidate')) {
    $captureRoot = Join-Path $workspaceRoot "artifacts/ui-v3/$captureMode"
    $capturePath = Join-Path $captureRoot 'captures.json'
    if (Test-Path -LiteralPath $capturePath -PathType Leaf) {
        try {
            if ((Get-Item -LiteralPath $capturePath).Length -gt 16KB) { throw 'Oversized capture manifest.' }
            $capture = Get-Content -LiteralPath $capturePath -Raw | ConvertFrom-Json
            if ($capture.source -notmatch '^[a-f0-9]{40}$' -or $capture.mode -ne $captureMode -or @($capture.images).Count -gt 16) { throw 'Invalid capture manifest.' }
            $lines.Add("Native client captures generated: $captureMode; source: $($capture.source); images: $(@($capture.images).Count). Image inspection is separate.")
            foreach ($picture in $capture.images) {
                if ($picture.name -notmatch '^(baseline|candidate)-[0-9]{3,4}x[0-9]{3,4}-(selected(-(archive|review))?|empty|error|high-contrast)\.png$') { throw 'Invalid fixed capture name.' }
                foreach ($field in @('windowWidthDip','windowHeightDip','PixelWidth','PixelHeight','DpiScaleX','DpiScaleY')) {
                    $number = $picture.$field
                    if ($number -isnot [double] -and $number -isnot [long] -and $number -isnot [int]) { throw 'Invalid capture dimensions.' }
                    if ($number -le 0 -or $number -gt 8192) { throw 'Invalid capture dimensions.' }
                }
                $lines.Add("- $($picture.name): window $($picture.windowWidthDip)x$($picture.windowHeightDip) DIP; client $($picture.PixelWidth)x$($picture.PixelHeight) pixels; scale $($picture.DpiScaleX)x$($picture.DpiScaleY)")
            }
        }
        catch { $lines.Add('Native capture manifest unavailable or invalid; no visual result inferred.') }
    }
    $checksPath = Join-Path $captureRoot 'independent-native-checks.json'
    if (Test-Path -LiteralPath $checksPath -PathType Leaf) {
        try {
            if ((Get-Item -LiteralPath $checksPath).Length -gt 32KB) { throw 'Oversized native UI receipt.' }
            $checks = Get-Content -LiteralPath $checksPath -Raw | ConvertFrom-Json
            if ($checks.completed -isnot [bool] -or @($checks.passed).Count -gt 200) { throw 'Invalid native UI result.' }
            $lines.Add('Independent native UI completed: ' + $checks.completed + '; passed checkpoints: ' + @($checks.passed).Count)
            foreach ($field in @('stage','failedCheck','failureType')) {
                $value = $checks.$field
                if ($null -ne $value) {
                    if ($value -isnot [string] -or $value.Length -gt 120 -or $value -notmatch '^[A-Za-z0-9 .-]+$') { throw 'Invalid fixed UI checkpoint.' }
                    $lines.Add("UI ${field}: $value")
                }
            }
            $shortNotices.Add("Independent native UI completed: $($checks.completed); passed: $(@($checks.passed).Count); stage: $($checks.stage); failed: $($checks.failedCheck); type: $($checks.failureType)")
        }
        catch { $lines.Add('Independent native UI receipt invalid; no passing result inferred.') }
    }
    $contrastPath = Join-Path $captureRoot 'high-contrast-observation.json'
    if (Test-Path -LiteralPath $contrastPath -PathType Leaf) {
        try {
            if ((Get-Item -LiteralPath $contrastPath).Length -gt 2KB) { throw 'Oversized contrast receipt.' }
            $contrast = Get-Content -LiteralPath $contrastPath -Raw | ConvertFrom-Json
            if ($contrast.nativeEnabled -isnot [bool] -or $contrast.wpfEnabled -isnot [bool]) { throw 'Invalid contrast receipt.' }
            $lines.Add('OS contrast capture: native enabled ' + $contrast.nativeEnabled + '; WPF enabled ' + $contrast.wpfEnabled + '. Physical display and screen reader remain unverified.')
            $shortNotices.Add('OS contrast capture: native enabled ' + $contrast.nativeEnabled + '; WPF enabled ' + $contrast.wpfEnabled)
        }
        catch { $lines.Add('OS contrast observation invalid; no contrast result inferred.') }
    }
    $failurePath = Join-Path $captureRoot 'failure.json'
    if (Test-Path -LiteralPath $failurePath -PathType Leaf) {
        try {
            if ((Get-Item -LiteralPath $failurePath).Length -gt 1KB) { throw 'Oversized harness failure.' }
            $failure = Get-Content -LiteralPath $failurePath -Raw | ConvertFrom-Json
            if ($failure.stage -notin @('capture','independent checks') -or $failure.type -notmatch '^[A-Za-z0-9]{1,80}$') { throw 'Invalid harness failure.' }
            $shortNotices.Add('Native harness failed at ' + $failure.stage + '; type: ' + $failure.type)
        }
        catch { $lines.Add('Harness failure detail unavailable; no passing result inferred.') }
    }
}
$performanceRoot = Join-Path $workspaceRoot 'artifacts/qa'
if (Test-Path -LiteralPath $performanceRoot -PathType Container) {
    foreach ($performance in (Get-ChildItem -LiteralPath $performanceRoot -Directory -Filter 'performance-*' | Select-Object -First 3)) {
        $budgetPath = Join-Path $performance.FullName 'budget-evaluation.json'
        if (-not (Test-Path -LiteralPath $budgetPath -PathType Leaf)) { $lines.Add('Resource evaluation missing; no budget pass inferred.'); continue }
        try {
            if ((Get-Item -LiteralPath $budgetPath).Length -gt 16KB) { throw 'Oversized budget receipt.' }
            $budget = Get-Content -LiteralPath $budgetPath -Raw | ConvertFrom-Json
            if ($budget.allIntervalsWithinBudget -isnot [bool] -or @($budget.samples).Count -ne 3 -or $budget.cpuLimitExclusiveOneCorePercent -ne 1 -or $budget.privateBytesLimitExclusive -ne 150000000 -or $budget.overheadSubtracted -ne $false) { throw 'Invalid budget receipt.' }
            $lines.Add('Companion resource intervals within unchanged budgets: ' + $budget.allIntervalsWithinBudget)
            $shortNotices.Add('Companion resource intervals within unchanged budgets: ' + $budget.allIntervalsWithinBudget)
            foreach ($sample in $budget.samples) {
                if ($sample.mode -notin @('visible-idle', 'visible-reduced-motion', 'hidden') -or $sample.oneCorePercent -lt 0 -or $sample.oneCorePercent -gt 10000 -or $sample.peakPrivateBytes -lt 0 -or $sample.peakPrivateBytes -gt 1TB) { throw 'Invalid resource sample.' }
                $lines.Add('- resource mode ' + $sample.mode + '; one-core CPU percent: ' + $sample.oneCorePercent + '; peak private bytes: ' + $sample.peakPrivateBytes)
                $shortNotices.Add('Resource mode ' + $sample.mode + '; one-core CPU percent: ' + $sample.oneCorePercent + '; peak private bytes: ' + $sample.peakPrivateBytes)
            }
        }
        catch { $lines.Add('Resource receipt invalid; no budget result inferred.') }
    }
}
if (($lines -join "`n").Length -gt 32KB) { throw 'Verification summary exceeds its 32 KiB character limit.' }
$lines | Out-File -LiteralPath $SummaryPath -Encoding utf8 -Append
if ($env:GITHUB_ACTIONS -eq 'true') {
    foreach ($notice in $shortNotices) {
        if ($notice.Length -gt 1024) { throw 'Compact verification notice exceeds bound.' }
        Write-Output ('::notice title=Native UI and resource result::' + $notice.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A'))
    }
    # A single bounded annotation is also readable through GitHub's normal checks
    # API when job summaries/log downloads are unavailable. Escape workflow-command
    # delimiters; the same content policy above applies, without extra data.
    $annotation = ($lines -join "`n").Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
    Write-Output ('::notice title=Bounded verification evidence::' + $annotation)
}
