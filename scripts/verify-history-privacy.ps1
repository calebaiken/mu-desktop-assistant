[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release', [switch]$Speech)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$fixtureName = if ($Speech) { 'private-speech' } else { 'private-history' }
$runRoot = Join-Path $repo ('artifacts/qa/' + $fixtureName + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
$profile = Join-Path $runRoot 'profile'
New-Item -ItemType Directory -Path $profile -Force | Out-Null
$executable = Join-Path $repo "src/Companion.Desktop/bin/$Configuration/net10.0-windows/Companion.Desktop.exe"
$sentinels = @('PHASE3_TEMP_PROMPT_7D2A95B4A13E4D87A41F29F5E904C6B1', 'PHASE3_TEMP_REPLY_86913C08B64F4ED696CF8AA497629C15')
if ($Speech) { $sentinels = @('PHASE4A_TEMP_SPEECH_6E31D790B7A94F54B2A937AD55C00812') }
$result = [ordered]@{ startedUtc=[DateTime]::UtcNow.ToString('o'); status='FAIL'; ready=$false; ownedProcessTerminated=$false; files=@(); failure=$null }
$result.fixture = $fixtureName
$result.binarySha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
$result.binaries = [ordered]@{}
foreach ($assembly in @('Companion.Desktop.dll', 'Companion.Application.dll', 'Companion.Infrastructure.dll', 'Companion.Core.dll')) {
    $result.binaries[$assembly] = (Get-FileHash -LiteralPath (Join-Path (Split-Path $executable) $assembly) -Algorithm SHA256).Hash
}
$result.sourceCheckpoint = (& git -C $repo rev-parse HEAD)
$started = $false
$process = New-Object Diagnostics.Process
$process.StartInfo.FileName = $executable
$process.StartInfo.Arguments = '--' + $fixtureName + '-smoke --data-dir "' + $profile + '"'
$process.StartInfo.UseShellExecute = $false
$process.StartInfo.CreateNoWindow = $true
try {
    if (-not $process.Start()) { throw 'Privacy probe did not start.' }
    $started = $true
    $deadline = [Diagnostics.Stopwatch]::StartNew()
    $marker = Join-Path $profile ($fixtureName + '-ready.json')
    while (-not (Test-Path -LiteralPath $marker)) {
        if ($process.HasExited) { throw "Privacy probe exited before readiness: $($process.ExitCode)." }
        if ($deadline.Elapsed.TotalSeconds -gt 15) { throw 'Privacy probe exceeded its declared 15-second readiness deadline.' }
        Start-Sleep -Milliseconds 100
    }
    $ready = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    if (-not $ready.ready -or -not $ready.temporary -or $ready.durableConversations -ne 1 -or $ready.processId -ne $process.Id) {
        throw 'Privacy probe did not establish the expected real saved/temporary state.'
    }
    if ($Speech -and -not $ready.speechBusy) { throw 'Temporary speech operation was not active at readiness.' }
    $result.ready = $true
    $process.Kill()
    if (-not $process.WaitForExit(5000)) { throw 'Owned privacy probe did not terminate.' }
    $result.ownedProcessTerminated = $true
    $binaryEncoding = [Text.Encoding]::GetEncoding(28591)
    $patterns = foreach ($sentinel in $sentinels) {
        foreach ($encoding in @([Text.Encoding]::UTF8, [Text.Encoding]::Unicode, [Text.Encoding]::BigEndianUnicode)) {
            $binaryEncoding.GetString($encoding.GetBytes($sentinel))
        }
    }
    $files = @(Get-ChildItem -LiteralPath $profile -Recurse -File)
    if (-not ($files.Name -contains 'conversations.db')) { throw 'A real durable SQLite decoy database was not created.' }
    foreach ($file in $files) {
        if ($file.Length -gt 67108864) { throw 'Privacy fixture exceeded its per-file bound.' }
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        $contents = $binaryEncoding.GetString($bytes)
        foreach ($pattern in $patterns) {
            if ($contents.Contains($pattern)) { throw 'Temporary sentinel was persisted in the application profile.' }
        }
        $result.files += [ordered]@{ name=$file.FullName.Substring($profile.Length + 1); bytes=$file.Length; sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
    }
    $result.status = 'PASS'
}
catch { $result.failure = $_.Exception.Message; throw }
finally {
    if ($started -and -not $process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
    $process.Dispose()
    $result.endedUtc = [DateTime]::UtcNow.ToString('o')
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runRoot 'result.json') -Encoding utf8
    Write-Output "Privacy evidence: $runRoot"
}
Write-Output "PASS abrupt process termination: $fixtureName sentinels absent from all profile files in UTF-8, UTF-16LE and UTF-16BE."
