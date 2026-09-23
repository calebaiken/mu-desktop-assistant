$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$app = Join-Path $repo 'src/Companion.Desktop/bin/Release/net10.0-windows/Companion.Desktop.exe'
$evidence = Join-Path $repo ('artifacts/qa/local-options-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$cases = @(
    '--local-model-smoke',
    '--local-model-dir',
    '--local-model-dir models',
    '--local-model-dir //server/share',
    '--local-model-dir \\server\share',
    '--local-model-smoke --smoke --local-model-dir C:\models',
    '--smoke --local-model-smoke --local-model-dir C:\models',
    '--local-model-dir C:\models --local-model-dir C:\models'
)
$results = @()
foreach ($arguments in $cases) {
    $process = New-Object Diagnostics.Process
    $process.StartInfo.FileName = $app
    $process.StartInfo.Arguments = $arguments
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.CreateNoWindow = $true
    try {
        if (-not $process.Start()) { throw 'Could not start option check.' }
        if (-not $process.WaitForExit(10000)) { $process.Kill(); throw 'Option check exceeded deadline.' }
        $results += [pscustomobject]@{ arguments=$arguments; exitCode=$process.ExitCode; passed=($process.ExitCode -eq 2) }
    }
    finally { $process.Dispose() }
}
[ordered]@{ binarySha256=(Get-FileHash -LiteralPath (Join-Path (Split-Path $app) 'Companion.Desktop.dll')).Hash; results=$results } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'results.json') -Encoding utf8
Write-Output ('Evidence: ' + $evidence)
if (@($results | Where-Object { -not $_.passed }).Count) { throw 'Invalid local model options were not all refused.' }
Write-Output 'PASS 8 local model command-line rejection checks.'
