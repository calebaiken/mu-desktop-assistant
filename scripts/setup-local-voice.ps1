param([string]$Destination = (Join-Path $PSScriptRoot '../artifacts/local-voice'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Destination = [IO.Path]::GetFullPath($Destination)
if ($Destination.StartsWith('\\')) { throw 'Choose a local drive directory.' }
function Assert-LocalPath([string]$path) {
    for ($parent = [IO.DirectoryInfo]::new($path); $null -ne $parent; $parent = $parent.Parent) {
        if ($parent.Exists -and ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Voice setup paths cannot contain reparse points.'
        }
    }
}
Assert-LocalPath $Destination
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
function Assert-Space([long]$bytes) {
    $drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($Destination))
    if ($drive.AvailableFreeSpace - $bytes -lt 1GB) { throw 'Voice setup requires at least 1 GiB free after writing its files.' }
}
function Get-VerifiedFile([string]$url, [string]$path, [long]$size, [string]$sha256) {
    Assert-LocalPath (Split-Path -Parent $path)
    if (Test-Path -LiteralPath $path) {
        $existing = Get-Item -LiteralPath $path
        if ($existing.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'A voice asset is a reparse point.' }
        if ($existing.Length -eq $size -and (Get-FileHash -LiteralPath $path).Hash -eq $sha256) { return }
        throw 'An existing voice asset differs from its pin; no file was overwritten.'
    }
    Assert-Space ($size + 16MB)
    $partial = $path + '.partial-' + [Guid]::NewGuid().ToString('N')
    $client = [Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromMinutes(10)
    $deadline = [Threading.CancellationTokenSource]::new([TimeSpan]::FromMinutes(10))
    $response = $null; $incoming = $null; $outgoing = $null
    try {
        $response = $client.GetAsync($url, [Net.Http.HttpCompletionOption]::ResponseHeadersRead, $deadline.Token).GetAwaiter().GetResult()
        $response.EnsureSuccessStatusCode() | Out-Null
        if ($response.Content.Headers.ContentLength -and $response.Content.Headers.ContentLength -ne $size) {
            throw 'The publisher returned an unexpected length.'
        }
        $incoming = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        $outgoing = [IO.File]::Open($partial, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        $buffer = New-Object byte[] 65536
        $written = 0L
        while (($count = $incoming.ReadAsync($buffer, 0, $buffer.Length, $deadline.Token).GetAwaiter().GetResult()) -gt 0) {
            $written += $count
            if ($written -gt $size) { throw 'Voice asset exceeded its pinned size.' }
            Assert-Space $count
            $outgoing.Write($buffer, 0, $count)
        }
        $outgoing.Flush($true)
        $outgoing.Dispose(); $outgoing = $null
        if ($written -ne $size -or (Get-FileHash -LiteralPath $partial).Hash -ne $sha256) {
            throw 'Voice asset checksum or length mismatch; it must not be loaded.'
        }
        [IO.File]::Move($partial, $path)
    }
    finally {
        if ($outgoing) { $outgoing.Dispose() }
        if ($incoming) { $incoming.Dispose() }
        if ($response) { $response.Dispose() }
        $client.Dispose(); $deadline.Dispose()
        if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial }
    }
}

# Stable v1.9.4 names b5130, both commit 927cfce34f31707e17f2bff35c349632fb9e2c3a.
$archive = Join-Path $Destination 'whisper-b5130-bin-x64.zip'
Get-VerifiedFile 'https://github.com/ggml-org/whisper.cpp/releases/download/b5130/whisper-bin-x64.zip' $archive 8573270 'f9ec6c52a2e949b62ab51fa21d0d497958f9e41c3010c157c4e42932d5316f3c'
$catalogPath = Join-Path $PSScriptRoot '../src/Companion.Infrastructure/VoiceInput/recognition-runtime-hashes.json'
$catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
$runtime = Join-Path $Destination 'runtime'
Assert-LocalPath $runtime
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach ($member in $catalog.PSObject.Properties) {
        if ($member.Name -notmatch '^[a-z0-9-]+\.dll$') { throw 'Invalid catalog name.' }
        $entry = $zip.GetEntry('Release/' + $member.Name)
        if ($null -eq $entry -or $entry.Length -ne $member.Value.bytes -or $entry.Length -gt 4MB) { throw 'Unexpected runtime member.' }
        $target = Join-Path $runtime $member.Name
        if (-not (Test-Path -LiteralPath $target)) {
            Assert-Space $entry.Length
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $false)
        }
        $file = Get-Item -LiteralPath $target
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $file.Length -ne $member.Value.bytes -or
            (Get-FileHash -LiteralPath $target).Hash -ne $member.Value.sha256) { throw 'Runtime asset mismatch; no existing file was overwritten.' }
    }
}
finally { $zip.Dispose() }
$actual = @(Get-ChildItem -LiteralPath $runtime -Force)
if ($actual.Count -ne @($catalog.PSObject.Properties).Count -or @($actual | Where-Object { $_.Name -notin $catalog.PSObject.Properties.Name }).Count -ne 0) {
    throw 'Unmanifested runtime entries are present; they were preserved and must not be loaded.'
}
$models = Join-Path $Destination 'models'
Assert-LocalPath $models
New-Item -ItemType Directory -Path $models -Force | Out-Null
Get-VerifiedFile 'https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-base.en.bin' (Join-Path $models 'ggml-base.en.bin') 147964211 'a03779c86df3323075f5e796cb2ce5029f00ec8869eee3fdfb897afe36c6d002'
Get-VerifiedFile 'https://huggingface.co/ggml-org/whisper-vad/resolve/9ffd54a1e1ee413ddf265af9913beaf518d1639b/ggml-silero-v6.2.0.bin' (Join-Path $models 'ggml-silero-v6.2.0.bin') 885098 '2aa269b785eeb53a82983a20501ddf7c1d9c48e33ab63a41391ac6c9f7fb6987'
Write-Output ('Pinned local speech recognition assets verified at ' + $Destination)
Write-Output 'Setup does not open a microphone, run recognition or enable recording.'
