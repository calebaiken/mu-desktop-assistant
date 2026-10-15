[CmdletBinding()]
param([string]$Python = '', [string]$Uv = 'uv')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runtime = Join-Path $repoRoot 'artifacts/neural-speech-runtime'
$models = Join-Path $repoRoot 'artifacts/neural-speech-model'
$manifest = Get-Content -LiteralPath (Join-Path $repoRoot 'workers/neural_speech/model-manifest.json') -Raw | ConvertFrom-Json
$reviewedAssets = @{
    'kokoro-v1.0.onnx' = @{ size = 325505369L; sha256 = 'beb0d1848dee9a49da392cc3df26958d46cfa35d321edf434f52949153f0df3a' }
    'voices-v1.0.bin' = @{ size = 28214398L; sha256 = 'bca610b8308e8d99f32e6fe4197e7ec01679264efed0cac9140fe9c29f1fbf7d' }
}
if ($manifest.schemaVersion -ne 1 -or $manifest.repository -ne 'thewh1teagle/kokoro-onnx' -or
    $manifest.revision -ne 'model-files-v1.1' -or @($manifest.files.PSObject.Properties).Count -ne 2) {
    throw 'The reviewed neural voice manifest is invalid.'
}
foreach ($asset in $manifest.files.PSObject.Properties) {
    if (-not $reviewedAssets.ContainsKey($asset.Name) -or $asset.Value.size -ne $reviewedAssets[$asset.Name].size -or
        $asset.Value.sha256 -ne $reviewedAssets[$asset.Name].sha256) { throw 'Neural voice manifest integrity pins differ from the reviewed assets.' }
}

function Assert-LocalPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if ($full.StartsWith('\\') -or $full.Substring(2).Contains(':')) { throw 'Neural voice setup requires ordinary local paths.' }
    for ($part = $full; $part; $part = [IO.Path]::GetDirectoryName($part)) {
        if (Test-Path -LiteralPath $part) {
            if (((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Neural voice setup refuses links and junctions.'
            }
        }
    }
}
Assert-LocalPath $runtime
Assert-LocalPath $models
$drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($repoRoot))
if ($drive.AvailableFreeSpace -lt 2GB) { throw 'Optional neural voice setup needs at least 2 GiB free, including its separate CPU runtime and a reserve.' }
if (-not $Python) {
    $config = Join-Path $repoRoot 'artifacts/ml-runtime/pyvenv.cfg'
    if (Test-Path -LiteralPath $config) {
        $line = Get-Content -LiteralPath $config | Where-Object { $_.StartsWith('home = ') } | Select-Object -First 1
        if ($line) { $Python = Join-Path $line.Substring(7).Trim() 'python.exe' }
    }
    if (-not $Python) { $Python = 'python' }
}
$version = & $Python -I -c 'import sys; print(sys.version_info.major,sys.version_info.minor,sep=chr(46))'
if ($LASTEXITCODE -ne 0 -or $version -ne '3.12') { throw 'Select a local Python 3.12 interpreter using -Python.' }
if (-not (Test-Path -LiteralPath (Join-Path $runtime 'Scripts/python.exe'))) {
    & $Uv venv --python $Python $runtime
    if ($LASTEXITCODE -ne 0) { throw 'Optional neural voice environment creation failed.' }
}
$pythonExe = Join-Path $runtime 'Scripts/python.exe'
$previousCache = $env:UV_CACHE_DIR
$previousLink = $env:UV_LINK_MODE
try {
    $env:UV_CACHE_DIR = Join-Path $repoRoot 'artifacts/uv-cache'
    $env:UV_LINK_MODE = 'hardlink'
    & $Uv pip sync --python $pythonExe --require-hashes --only-binary :all: --no-cache (Join-Path $repoRoot 'workers/requirements-neural-speech.lock')
    if ($LASTEXITCODE -ne 0) { throw 'Optional neural voice CPU runtime installation failed. Existing chat and Windows speech remain available.' }
}
finally { $env:UV_CACHE_DIR = $previousCache; $env:UV_LINK_MODE = $previousLink }
$verifyCode = @'
import importlib.metadata as m
expected = {"kokoro-onnx":"0.6.1", "onnxruntime":"1.23.2", "numpy":"2.3.3", "phonemizer":"3.4.0", "espeakng-loader":"0.2.4"}
assert all(m.version(k) == v for k, v in expected.items())
print("Neural voice CPU dependencies verified")
'@
$verifyCode | & $pythonExe -I -
if ($LASTEXITCODE -ne 0) { throw 'Optional neural voice dependency verification failed.' }

Add-Type -AssemblyName System.Net.Http
$handler = [Net.Http.HttpClientHandler]::new()
$handler.UseCookies = $false
$client = [Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromMinutes(15)
try {
    foreach ($entry in $manifest.files.PSObject.Properties) {
        $name = $entry.Name
        if ($name -notin @('kokoro-v1.0.onnx', 'voices-v1.0.bin')) { throw 'Unexpected neural voice asset.' }
        $target = Join-Path $models $name
        Assert-LocalPath $target
        $expectedBytes = [long]$entry.Value.size
        $expectedHash = [string]$entry.Value.sha256
        if ($expectedBytes -le 0 -or $expectedBytes -gt 350MB -or $expectedHash -notmatch '^[a-f0-9]{64}$') { throw 'Invalid neural voice asset integrity metadata.' }
        if (Test-Path -LiteralPath $target) {
            if ((Get-Item -LiteralPath $target).Length -ne $expectedBytes -or
                (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) {
                throw "Existing neural voice asset '$name' differs from the reviewed pin. It was preserved."
            }
            Write-Output "Verified $name"
            continue
        }
        $drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($repoRoot))
        if ($drive.AvailableFreeSpace -lt $expectedBytes + 1GB) { throw 'Insufficient free space for this asset plus a 1 GiB reserve.' }
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        $temporary = $target + '.download-' + [Guid]::NewGuid().ToString('N')
        $response = $null
        $inputStream = $null
        $outputStream = $null
        $deadline = [Threading.CancellationTokenSource]::new([TimeSpan]::FromMinutes(15))
        try {
            $url = 'https://github.com/' + $manifest.repository + '/releases/download/' + $manifest.revision + '/' + $name
            $response = $client.GetAsync($url, [Net.Http.HttpCompletionOption]::ResponseHeadersRead, $deadline.Token).GetAwaiter().GetResult()
            $response.EnsureSuccessStatusCode() | Out-Null
            if ($response.Content.Headers.ContentLength -and $response.Content.Headers.ContentLength -ne $expectedBytes) { throw 'Neural voice download differs from its pinned length.' }
            $inputStream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
            $outputStream = [IO.FileStream]::new($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
            $buffer = [byte[]]::new(65536)
            [long]$total = 0
            while (($read = $inputStream.ReadAsync($buffer, 0, $buffer.Length, $deadline.Token).GetAwaiter().GetResult()) -gt 0) {
                $total += $read
                if ($total -gt $expectedBytes) { throw 'Neural voice download exceeded its pinned byte limit.' }
                $outputStream.Write($buffer, 0, $read)
            }
            $outputStream.Flush($true)
            $outputStream.Dispose(); $outputStream = $null
            if ($total -ne $expectedBytes -or (Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) {
                throw 'Neural voice download failed integrity verification.'
            }
            [IO.File]::Move($temporary, $target)
            Write-Output "Installed verified $name ($expectedBytes bytes)"
        }
        finally {
            if ($outputStream) { $outputStream.Dispose() }
            if ($inputStream) { $inputStream.Dispose() }
            if ($response) { $response.Dispose() }
            $deadline.Dispose()
            if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
        }
    }
}
finally { $client.Dispose(); $handler.Dispose() }
Write-Output 'Optional Kokoro neural voices installed. Speech stays local with no API key. Qualify real synthesis before claiming voice playback works.'
