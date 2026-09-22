[CmdletBinding()]
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $workspaceRoot
try {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_CLI_HOME = Join-Path $workspaceRoot '.dotnet-home'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
    & dotnet restore Companion.slnx --locked-mode --disable-parallel --disable-build-servers -m:1 -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & dotnet build Companion.slnx --configuration $Configuration --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & dotnet run --project tests/Companion.ArchitectureChecks --configuration $Configuration --no-build -- $workspaceRoot
    if ($LASTEXITCODE -ne 0) { throw 'Architecture checks failed.' }
    $resultsDirectory = Join-Path $workspaceRoot "artifacts\test-results\$Configuration"
    & dotnet test tests/Companion.Tests/Companion.Tests.csproj --configuration $Configuration --no-build --no-restore --logger 'trx;LogFileName=tests.trx' --results-directory $resultsDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Unit/integration tests failed.' }
    [xml]$testReport = Get-Content -LiteralPath (Join-Path $resultsDirectory 'tests.trx') -Raw
    if ([int]$testReport.TestRun.ResultSummary.Counters.executed -lt 1) { throw 'No unit/integration tests executed.' }
    $appPath = Join-Path $workspaceRoot "src\Companion.Desktop\bin\$Configuration\net10.0-windows\Companion.Desktop.exe"
    $appProcess = New-Object System.Diagnostics.Process
    $appProcess.StartInfo.FileName = $appPath
    $smokeData = Join-Path $workspaceRoot ('artifacts\smoke\' + [Guid]::NewGuid().ToString('N'))
    $appProcess.StartInfo.Arguments = '--smoke --data-dir "' + $smokeData + '"'
    $appProcess.StartInfo.UseShellExecute = $false
    $appProcess.StartInfo.CreateNoWindow = $true
    try {
        if (-not $appProcess.Start()) { throw 'WPF smoke process could not start.' }
        if (-not $appProcess.WaitForExit(15000)) {
            $appProcess.Kill()
            throw 'WPF smoke check timed out after 15 seconds.'
        }
        if ($appProcess.ExitCode -ne 0) { throw "WPF smoke check failed: $($appProcess.ExitCode)." }
    }
    finally { $appProcess.Dispose() }
    Write-Output 'PASS composed WPF settings/logging/instance/XAML/startup/shutdown smoke (no visible window).'
    Write-Output 'Verification succeeded. This does not validate future application features.'
}
finally {
    Pop-Location
}
