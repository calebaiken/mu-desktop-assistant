param(
    [string]$ApplicationPath = (Join-Path $PSScriptRoot '../artifacts/Mu/Companion.Desktop.exe'),
    [string]$ShortcutDirectory = [Environment]::GetFolderPath('DesktopDirectory')
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
function Local-Path([string]$value) {
    if (-not [IO.Path]::IsPathRooted($value) -or $value.StartsWith('\\') -or $value -match '["\r\n]') { throw 'Use a local absolute path without quotes or newlines.' }
    return [IO.Path]::GetFullPath($value)
}
$executable = Local-Path ([IO.Path]::GetFullPath($ApplicationPath))
$destination = Local-Path $ShortcutDirectory
if (-not [IO.File]::Exists($executable)) { throw 'Build the current Mu candidate into artifacts/Mu first.' }
if (-not [IO.Directory]::Exists($destination)) { throw 'The shortcut destination folder does not exist.' }
$shortcutPath = Join-Path $destination 'Mu.lnk'
if (Test-Path -LiteralPath $shortcutPath) { throw "An existing shortcut was preserved: $shortcutPath. Choose another folder or remove it explicitly before creating a replacement." }
$arguments = '--ml-root "' + $repo + '"'
$modelDirectory = Join-Path $repo 'artifacts/local-ai'
if (Test-Path -LiteralPath (Join-Path $modelDirectory 'Qwen3-0.6B-Q8_0.gguf')) { $arguments += ' --local-model-dir "' + $modelDirectory + '"' }
$shell = New-Object -ComObject WScript.Shell
try {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $executable
    $shortcut.Arguments = $arguments
    $shortcut.WorkingDirectory = $repo
    $shortcut.Description = 'Mu - local desktop assistant and Machine Learning Lab'
    $shortcut.IconLocation = $executable + ',0'
    $shortcut.Save()
    $verified = $shell.CreateShortcut($shortcutPath)
    if ($verified.TargetPath -ne $executable -or $verified.Arguments -ne $arguments -or $verified.WorkingDirectory -ne $repo -or $verified.IconLocation -ne ($executable + ',0')) { throw 'The saved shortcut failed verification.' }
    Write-Output "Created and verified $shortcutPath"
} finally {
    if ($null -ne $verified) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($verified) }
    if ($null -ne $shortcut) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) }
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
}
