[CmdletBinding()]
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$checkerPath = Join-Path $workspaceRoot "tests\Companion.ArchitectureChecks\bin\$Configuration\net10.0\Companion.ArchitectureChecks.exe"
$appPath = Join-Path $workspaceRoot "src\Companion.Desktop\bin\$Configuration\net10.0-windows\Companion.Desktop.exe"
foreach ($executable in @($checkerPath, $appPath)) {
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "Build prerequisite missing: $executable. Run scripts/verify.ps1 first."
    }
}

# All mutations are confined to a new fixture. Retain evidence; never modify source.
$runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$evidenceRoot = Join-Path $workspaceRoot "artifacts\qa\negative-$runId"
$fixtureRoot = Join-Path $evidenceRoot 'fixture'
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$fixtureFiles = @(
    'Companion.slnx', 'Directory.Build.props', 'global.json', 'NuGet.Config', '.gitignore',
    'README.md', 'ARCHITECTURE.md', 'SECURITY.md', 'TESTING.md', 'ROADMAP.md',
    'CHANGELOG.md', 'DEVELOPMENT_STATUS.md', 'docs/CONTRACTS.md', 'docs/REQUIREMENTS.md',
    'docs/QA_PROCESS.md', 'src/Companion.Desktop/app.manifest',
    'tests/Companion.ArchitectureChecks/Companion.ArchitectureChecks.csproj',
    'tests/Companion.Tests/Companion.Tests.csproj'
)
foreach ($layer in @('Core', 'Application', 'Infrastructure', 'Desktop')) {
    $fixtureFiles += "src/Companion.$layer/Companion.$layer.csproj"
}
foreach ($relativePath in $fixtureFiles) {
    $destination = Join-Path $fixtureRoot $relativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $workspaceRoot $relativePath) -Destination $destination
}

$script:caseCount = 0
function Assert-ProcessResult {
    param([string]$Name, [string]$Executable, [string]$Arguments, [int]$ExpectedExit, [string]$ExpectedText = '')
    $script:caseCount++
    $stdoutPath = Join-Path $evidenceRoot "$Name.stdout.txt"
    $stderrPath = Join-Path $evidenceRoot "$Name.stderr.txt"
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $process.StartInfo.FileName = $Executable
    $process.StartInfo.Arguments = $Arguments
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.CreateNoWindow = $true
    $process.StartInfo.RedirectStandardOutput = $true
    $process.StartInfo.RedirectStandardError = $true
    try {
        if (-not $process.Start()) { throw "$Name could not start." }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(15000)) {
            $process.Kill()
            $process.WaitForExit()
            throw "$Name exceeded 15-second deadline."
        }
        $actualExit = $process.ExitCode
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        Set-Content -LiteralPath $stdoutPath -Value $stdout -Encoding UTF8
        Set-Content -LiteralPath $stderrPath -Value $stderr -Encoding UTF8
        $captured = $stdout + $stderr
        if ($actualExit -ne $ExpectedExit) { throw "$Name returned $actualExit, expected $ExpectedExit. $captured" }
        if ($ExpectedText -and -not $captured.Contains($ExpectedText)) {
            throw "$Name did not report expected diagnostic '$ExpectedText'. $captured"
        }
        Write-Output "PASS $Name (exit $actualExit)"
    }
    finally { $process.Dispose() }
}

$fixtureArgument = '"{0}"' -f $fixtureRoot
Assert-ProcessResult 'valid-fixture' $checkerPath $fixtureArgument 0 '11/11 architecture checks passed.'
Assert-ProcessResult 'checker-missing-argument' $checkerPath '' 2 'Usage:'
Assert-ProcessResult 'checker-invalid-root' $checkerPath ('"{0}"' -f (Join-Path $fixtureRoot 'absent')) 2 'Usage:'

$coreProject = Join-Path $fixtureRoot 'src/Companion.Core/Companion.Core.csproj'
$originalCore = Get-Content -LiteralPath $coreProject -Raw
try {
    $forbiddenProject = '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../Companion.Desktop/Companion.Desktop.csproj" /></ItemGroup></Project>'
    Set-Content -LiteralPath $coreProject -Value $forbiddenProject -Encoding UTF8
    Assert-ProcessResult 'forbidden-reference' $checkerPath $fixtureArgument 1 'FAIL Core project dependency boundary'
}
finally { Set-Content -LiteralPath $coreProject -Value $originalCore -Encoding UTF8 }

$readme = Join-Path $fixtureRoot 'README.md'
# A zero-length document exercises the same required-document assertion without deletion.
Set-Content -LiteralPath $readme -Value '' -Encoding UTF8
Assert-ProcessResult 'empty-required-document' $checkerPath $fixtureArgument 1 'FAIL required planning documents'
Copy-Item -LiteralPath (Join-Path $workspaceRoot 'README.md') -Destination $readme -Force

try {
    Set-Content -LiteralPath $coreProject -Value '<Project><broken>' -Encoding UTF8
    Assert-ProcessResult 'malformed-project-xml' $checkerPath $fixtureArgument 1 'FAIL Core project dependency boundary'
}
finally { Set-Content -LiteralPath $coreProject -Value $originalCore -Encoding UTF8 }

Assert-ProcessResult 'app-invalid-flag' $appPath '--invalid' 2
Assert-ProcessResult 'app-extra-flag' $appPath '--smoke extra' 2
Assert-ProcessResult 'restored-fixture' $checkerPath $fixtureArgument 0 '11/11 architecture checks passed.'
Write-Output "$script:caseCount/$script:caseCount independent negative/control checks passed."
Write-Output "Evidence retained at $evidenceRoot"
