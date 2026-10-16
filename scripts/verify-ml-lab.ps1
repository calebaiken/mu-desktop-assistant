[CmdletBinding()]
param([switch]$SkipBuild, [string]$LocalModelDirectory, [string]$ApplicationPath)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $repo
try {
    if (-not $SkipBuild) {
        & dotnet build Companion.slnx --configuration Release --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
        if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    }
    $profile = Join-Path $repo ('artifacts/qa/ml-native-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $profile -Force | Out-Null
    $process = New-Object Diagnostics.Process
    $process.StartInfo.FileName = if ($ApplicationPath) { [IO.Path]::GetFullPath($ApplicationPath) } else { Join-Path $repo 'src/Companion.Desktop/bin/Release/net10.0-windows/Companion.Desktop.exe' }
    if (-not (Test-Path -LiteralPath $process.StartInfo.FileName -PathType Leaf)) { throw 'Build the desktop application before running ML diagnostics.' }
    $process.StartInfo.Arguments = '--ml-smoke --ml-root "' + $repo + '" --data-dir "' + $profile + '"'
    if ($LocalModelDirectory) {
        $model = [IO.Path]::GetFullPath($LocalModelDirectory)
        if ($model.Contains('"')) { throw 'Invalid model path.' }
        $process.StartInfo.Arguments += ' --local-model-dir "' + $model + '"'
    }
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.CreateNoWindow = $true
    try {
        if (-not $process.Start()) { throw 'ML native diagnostic did not start.' }
        if (-not $process.WaitForExit(240000)) { $process.Kill(); throw 'ML native diagnostic exceeded four minutes.' }
        if ($process.ExitCode -ne 0) { throw "ML native diagnostic failed ($($process.ExitCode)); inspect $profile." }
        $receipt = Join-Path $profile 'ml-lab-smoke.json'
        if (-not (Test-Path -LiteralPath $receipt)) { throw 'ML native diagnostic did not write its receipt.' }
        Copy-Item -LiteralPath $receipt -Destination (Join-Path $repo 'artifacts/qa/ml-lab-smoke.json') -Force
        Get-Content -LiteralPath $receipt
        Write-Output "ML native evidence: $profile"
    }
    finally { $process.Dispose() }
}
finally { Pop-Location }
