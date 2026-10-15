[CmdletBinding()]
param([string]$Python = '', [string]$Uv = 'uv')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runtime = Join-Path $repoRoot 'artifacts/laya-runtime'
$models = Join-Path $repoRoot 'artifacts/laya-model'
$manifestPath = Join-Path $repoRoot 'workers/laya_advisor/model-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.repository -ne 'convaiinnovations/laya' -or
    $manifest.revision -ne '55cf4c4ebb4ebe31b2550e8bdf3bd21b99753851' -or
    @($manifest.files.PSObject.Properties).Count -ne 5) { throw 'The reviewed LAYA manifest is invalid.' }

function Assert-LocalPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if ($full.StartsWith('\\') -or $full.Substring(2).Contains(':')) { throw 'LAYA setup requires ordinary local paths.' }
    for ($part = $full; $part; $part = [IO.Path]::GetDirectoryName($part)) {
        if (Test-Path -LiteralPath $part) {
            if (((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'LAYA setup refuses links and junctions.'
            }
        }
    }
}
Assert-LocalPath $runtime
Assert-LocalPath $models
$drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($repoRoot))
if ($drive.AvailableFreeSpace -lt 3GB) { throw 'Optional LAYA setup needs at least 3 GiB free (one 807 MiB checkpoint plus a separate CPU runtime and reserve).' }
if (-not $Python) {
    $mlConfig = Join-Path $repoRoot 'artifacts/ml-runtime/pyvenv.cfg'
    if (Test-Path -LiteralPath $mlConfig) {
        $line = Get-Content -LiteralPath $mlConfig | Where-Object { $_.StartsWith('home = ') } | Select-Object -First 1
        if ($line) { $Python = Join-Path $line.Substring(7).Trim() 'python.exe' }
    }
    if (-not $Python) { $Python = 'python' }
}
$version = & $Python -I -c 'import sys; print(sys.version_info.major,sys.version_info.minor,sep=chr(46))'
if ($LASTEXITCODE -ne 0 -or $version -ne '3.12') { throw 'Select a local Python 3.12 interpreter using -Python.' }
if (-not (Test-Path -LiteralPath (Join-Path $runtime 'Scripts/python.exe'))) {
    & $Uv venv --python $Python $runtime
    if ($LASTEXITCODE -ne 0) { throw 'Optional LAYA environment creation failed.' }
}
$pythonExe = Join-Path $runtime 'Scripts/python.exe'
$env:UV_CACHE_DIR = Join-Path $repoRoot 'artifacts/uv-cache'
$env:UV_LINK_MODE = 'hardlink'
& $Uv pip sync --python $pythonExe --require-hashes --only-binary :all: --no-cache --index https://download.pytorch.org/whl/cpu --index-strategy unsafe-best-match (Join-Path $repoRoot 'workers/requirements-laya.lock')
if ($LASTEXITCODE -ne 0) { throw 'Optional LAYA CPU runtime installation failed. Existing chat and ML Lab are unaffected.' }
& $pythonExe -I -c 'import torch,transformers,laya; assert torch.__version__ == "2.6.0+cpu" and torch.version.cuda is None; assert transformers.__version__ == "4.51.3" and laya.__version__ == "0.3.21"; print("LAYA CPU dependencies verified")'
if ($LASTEXITCODE -ne 0) { throw 'Optional LAYA runtime verification failed.' }

Add-Type -AssemblyName System.Net.Http
$handler = [Net.Http.HttpClientHandler]::new()
$handler.UseCookies = $false
$client = [Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromMinutes(15)
try {
    foreach ($entry in $manifest.files.PSObject.Properties) {
        $name = $entry.Name
        if ($name -notin @('model.safetensors', 'rl_agent_config.json', 'encoder/config.json', 'tokenizer/tokenizer_config.json', 'tokenizer/tokenizer.json')) {
            throw 'Unexpected file in LAYA model manifest.'
        }
        $target = Join-Path $models $name
        Assert-LocalPath $target
        $expectedBytes = [long]$entry.Value.size
        $expectedHash = [string]$entry.Value.sha256
        if (Test-Path -LiteralPath $target) {
            if ((Get-Item -LiteralPath $target).Length -ne $expectedBytes -or
                (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) {
                throw "Existing LAYA asset '$name' differs from the reviewed pin. It was preserved."
            }
            Write-Output "Verified $name"
            continue
        }
        $drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($repoRoot))
        if ($drive.AvailableFreeSpace -lt $expectedBytes + 1GB) { throw 'Insufficient free space to download this asset and preserve a 1 GiB reserve.' }
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        $temporary = $target + '.download-' + [Guid]::NewGuid().ToString('N')
        $response = $null
        $inputStream = $null
        $outputStream = $null
        $deadline = [Threading.CancellationTokenSource]::new([TimeSpan]::FromMinutes(15))
        try {
            $url = 'https://huggingface.co/' + $manifest.repository + '/resolve/' + $manifest.revision + '/' + $name
            $response = $client.GetAsync($url, [Net.Http.HttpCompletionOption]::ResponseHeadersRead, $deadline.Token).GetAwaiter().GetResult()
            $response.EnsureSuccessStatusCode() | Out-Null
            if ($response.Content.Headers.ContentLength -and $response.Content.Headers.ContentLength -ne $expectedBytes) { throw 'LAYA download length differs from the pinned asset.' }
            $inputStream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
            $outputStream = [IO.FileStream]::new($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
            $buffer = [byte[]]::new(65536)
            [long]$total = 0
            while (($read = $inputStream.ReadAsync($buffer, 0, $buffer.Length, $deadline.Token).GetAwaiter().GetResult()) -gt 0) {
                $total += $read
                if ($total -gt $expectedBytes) { throw 'LAYA download exceeded its pinned byte limit.' }
                $outputStream.Write($buffer, 0, $read)
            }
            $outputStream.Flush($true)
            $outputStream.Dispose(); $outputStream = $null
            if ($total -ne $expectedBytes -or (Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) {
                throw 'LAYA download failed integrity verification.'
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
$configuration = @{ schemaVersion = 1; python = $pythonExe; worker = (Join-Path $repoRoot 'workers/laya_advisor/worker.py'); modelDir = $models; revision = $manifest.revision } | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $repoRoot 'artifacts/laya-runtime.json'), $configuration, [Text.UTF8Encoding]::new($false))
Write-Output 'LAYA optional CPU assets installed. Inference stays local and requires no API key. Run local inference QA before treating this installation as validated.'
