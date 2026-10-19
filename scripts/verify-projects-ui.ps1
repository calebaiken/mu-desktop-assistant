[CmdletBinding()]
param([ValidateSet('baseline','candidate')][string]$Mode = 'candidate', [string]$SourceCommit)
$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $workspaceRoot
try {
    if ([string]::IsNullOrWhiteSpace($SourceCommit)) { $SourceCommit = (& git rev-parse HEAD).Trim() }
    $project = 'scripts/ResearchProjects.UiChecks/ResearchProjects.UiChecks.csproj'
    & dotnet restore $project --locked-mode --disable-parallel
    if ($LASTEXITCODE -ne 0) { throw 'Native project UI harness restore failed.' }
    & dotnet build $project -c Release --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Native project UI harness build failed.' }
    $output = Join-Path $workspaceRoot "artifacts/ui-v3/$Mode"
    $application = Join-Path $workspaceRoot 'scripts/ResearchProjects.UiChecks/bin/Release/net10.0-windows/ResearchProjects.UiChecks.exe'
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo.FileName = $application
    $process.StartInfo.Arguments = '"{0}" {1} {2}' -f $output, $Mode, $SourceCommit
    $process.StartInfo.UseShellExecute = $false
    try {
        if (-not $process.Start()) { throw 'Native project UI harness did not start.' }
        if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Native project UI harness exceeded 30 seconds.' }
        if ($process.ExitCode -ne 0) { throw "Native project UI harness failed: $($process.ExitCode)." }
    }
    finally { $process.Dispose() }
}
finally { Pop-Location }
