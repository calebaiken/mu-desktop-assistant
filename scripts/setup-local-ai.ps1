param([string]$Destination = (Join-Path $PSScriptRoot '../artifacts/local-ai'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Destination = [IO.Path]::GetFullPath($Destination)
if ($Destination.StartsWith('\\')) { throw 'Choose a local drive directory.' }
for ($parent = [IO.DirectoryInfo]::new($Destination); $null -ne $parent; $parent = $parent.Parent) {
    if ($parent.Exists -and ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Setup paths cannot contain reparse points.' }
}
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$reserve = 1GB
function Assert-Space([long]$bytes) {
    $drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($Destination))
    if ($drive.AvailableFreeSpace - $bytes -lt $reserve) { throw 'Setup requires at least 1 GiB free after writing its files.' }
}
function Get-VerifiedFile([string]$url, [string]$path, [long]$size, [string]$sha256) {
    if (Test-Path -LiteralPath $path) {
        if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Download paths cannot be reparse points.' }
        if ((Get-Item -LiteralPath $path).Length -eq $size -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $sha256) { return }
        throw 'An existing file does not match the pinned download. No file was overwritten.'
    }
    Assert-Space ($size + 64MB)
    $partial = $path + '.partial-' + [Guid]::NewGuid().ToString('N')
    $client = [Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromMinutes(10)
    $downloadCancellation = [Threading.CancellationTokenSource]::new([TimeSpan]::FromMinutes(10))
    $response = $null
    $inputStream = $null
    $outputStream = $null
    try {
        $response = $client.GetAsync($url, [Net.Http.HttpCompletionOption]::ResponseHeadersRead, $downloadCancellation.Token).GetAwaiter().GetResult()
        $response.EnsureSuccessStatusCode() | Out-Null
        $inputStream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        $outputStream = [IO.File]::Open($partial, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        $buffer = New-Object byte[] 1048576
        $written = 0L
        $deadline = [DateTime]::UtcNow.AddMinutes(10)
        while (($count = $inputStream.ReadAsync($buffer, 0, $buffer.Length, $downloadCancellation.Token).GetAwaiter().GetResult()) -gt 0) {
            $written += $count
            if ($written -gt $size -or [DateTime]::UtcNow -gt $deadline) { throw 'Download exceeded its declared size or deadline.' }
            Assert-Space $count
            $outputStream.Write($buffer, 0, $count)
        }
        $outputStream.Flush($true)
        $outputStream.Dispose(); $outputStream = $null
        if ($written -ne $size -or (Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ne $sha256) { throw 'Download checksum or length mismatch.' }
        [IO.File]::Move($partial, $path)
    }
    finally {
        if ($outputStream) { $outputStream.Dispose() }
        if ($inputStream) { $inputStream.Dispose() }
        if ($response) { $response.Dispose() }
        $client.Dispose()
        $downloadCancellation.Dispose()
        if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial }
    }
}

# Source: stable llama.cpp v0.4.1's nightly-tag.txt => b10964.
$archivePath = Join-Path $Destination 'llama-b10964-bin-win-cpu-x64.zip'
Get-VerifiedFile 'https://github.com/ggml-org/llama.cpp/releases/download/b10964/llama-b10964-bin-win-cpu-x64.zip' $archivePath 18427629 '917f39c076402c421224824607397af20f53625a60defc20e8dd22446bf4c5d7'
$runtimePath = Join-Path $Destination 'runtime'
if (-not (Test-Path -LiteralPath $runtimePath)) {
    $zip = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $expanded = 0L
        foreach ($entry in $zip.Entries) {
            $expanded += $entry.Length
            $entryPath = [IO.Path]::GetFullPath((Join-Path $runtimePath $entry.FullName))
            if (-not $entryPath.StartsWith($runtimePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe archive path.' }
        }
        if ($expanded -gt 256MB) { throw 'Runtime archive exceeds expansion budget.' }
        Assert-Space $expanded
    }
    finally { $zip.Dispose() }
    $stagingPath = Join-Path $Destination ('runtime-staging-' + [Guid]::NewGuid().ToString('N'))
    [IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $stagingPath)
    [IO.Directory]::Move($stagingPath, $runtimePath)
}
$runtimeCatalogPath = Join-Path $PSScriptRoot '../src/Companion.Infrastructure/Conversation/local-runtime-hashes.json'
$runtimeCatalog = Get-Content -LiteralPath $runtimeCatalogPath -Raw | ConvertFrom-Json
$runtimeFiles = @(Get-ChildItem -LiteralPath $runtimePath -File | Where-Object { $_.Extension -in '.dll', '.exe' })
if ($runtimeFiles.Count -ne @($runtimeCatalog.PSObject.Properties).Count) { throw 'Runtime inventory mismatch. Existing files were preserved.' }
foreach ($runtimeFile in $runtimeFiles) {
    if (($runtimeFile.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        (Get-FileHash -LiteralPath $runtimeFile.FullName -Algorithm SHA256).Hash -ne $runtimeCatalog.($runtimeFile.Name)) {
        throw 'Runtime checksum mismatch. Existing files were preserved.'
    }
}
$modelPath = Join-Path $Destination 'Qwen3-0.6B-Q8_0.gguf'
Get-VerifiedFile 'https://huggingface.co/Qwen/Qwen3-0.6B-GGUF/resolve/23749fefcc72300e3a2ad315e1317431b06b590a/Qwen3-0.6B-Q8_0.gguf' $modelPath 639446688 '9465e63a22add5354d9bb4b99e90117043c7124007664907259bd16d043bb031'
Write-Output ('Local model downloaded and hash-verified at ' + $Destination)
