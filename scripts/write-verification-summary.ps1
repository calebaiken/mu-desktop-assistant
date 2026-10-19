[CmdletBinding()]
param([string]$SummaryPath = $env:GITHUB_STEP_SUMMARY)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($SummaryPath)) { throw 'A job summary output path is required.' }
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$lines = [System.Collections.Generic.List[string]]::new()
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
if (($lines -join "`n").Length -gt 32KB) { throw 'Verification summary exceeds its 32 KiB character limit.' }
$lines | Out-File -LiteralPath $SummaryPath -Encoding utf8 -Append
if ($env:GITHUB_ACTIONS -eq 'true') {
    # A single bounded annotation is also readable through GitHub's normal checks
    # API when job summaries/log downloads are unavailable. Escape workflow-command
    # delimiters; the same content policy above applies, without extra data.
    $annotation = ($lines -join "`n").Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
    Write-Output ('::notice title=Bounded verification evidence::' + $annotation)
}
