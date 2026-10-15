param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../assets/brand'))
$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Run with powershell.exe -STA -File scripts/render-mu-icon.ps1.' }
Add-Type -AssemblyName PresentationCore, WindowsBase
$destination = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($destination) | Out-Null
# Same 140 x 110 native vector particle and detector decorations as the Muon avatar.
$halo = 'M38,79 C15,50 26,16 63,8 C102,-1 138,25 135,60 C133,76 124,89 109,96'
$svg = @"
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 160" role="img" aria-labelledby="title">
 <title id="title">Mu: a friendly muon-inspired particle mascot</title>
 <g transform="translate(10 20)">
  <path d="$halo" fill="none" stroke="#24483D" stroke-width="2" stroke-dasharray="8 8" stroke-linecap="round"/>
  <circle cx="18" cy="95" r="2" fill="#24483D"/><circle cx="29" cy="87" r="3" fill="#24483D"/><circle cx="40" cy="79" r="4" fill="#24483D"/>
  <circle cx="78" cy="55" r="31" fill="#DDEAE6" stroke="#24483D" stroke-width="3"/>
  <ellipse cx="68" cy="49" rx="3" ry="5" fill="#24483D"/>
  <ellipse cx="88" cy="49" rx="3" ry="5" fill="#24483D"/>
  <path d="M65,62 Q78,75 91,62" fill="none" stroke="#24483D" stroke-width="2.5" stroke-linecap="round"/>
 </g>
</svg>
"@
[IO.File]::WriteAllText((Join-Path $destination 'Mu.svg'), $svg, [Text.UTF8Encoding]::new($false))
$ink = [Windows.Media.BrushConverter]::new().ConvertFromString('#24483D')
$fill = [Windows.Media.BrushConverter]::new().ConvertFromString('#DDEAE6')
function Render-Mu([int]$size) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $drawing = $visual.RenderOpen()
    try {
        $drawing.PushTransform([Windows.Media.ScaleTransform]::new($size / 160.0, $size / 160.0))
        $drawing.PushTransform([Windows.Media.TranslateTransform]::new(10, 20))
        $pen = [Windows.Media.Pen]::new($ink, 3)
        $haloPen = [Windows.Media.Pen]::new($ink, 2)
        $haloPen.DashStyle = [Windows.Media.DashStyle]::new([double[]]@(4,4),0)
        $haloPen.DashCap = [Windows.Media.PenLineCap]::Round
        $drawing.DrawGeometry($null, $haloPen, [Windows.Media.Geometry]::Parse($halo))
        $drawing.DrawEllipse($ink, $null, [Windows.Point]::new(18,95), 2, 2)
        $drawing.DrawEllipse($ink, $null, [Windows.Point]::new(29,87), 3, 3)
        $drawing.DrawEllipse($ink, $null, [Windows.Point]::new(40,79), 4, 4)
        $drawing.DrawEllipse($fill, $pen, [Windows.Point]::new(78,55), 31, 31)
        $drawing.DrawEllipse($ink, $null, [Windows.Point]::new(68,49), 3, 5)
        $drawing.DrawEllipse($ink, $null, [Windows.Point]::new(88,49), 3, 5)
        $smile = [Windows.Media.Pen]::new($ink, 2.5)
        $smile.StartLineCap = $smile.EndLineCap = [Windows.Media.PenLineCap]::Round
        $drawing.DrawGeometry($null, $smile, [Windows.Media.Geometry]::Parse('M65,62 Q78,75 91,62'))
        $drawing.Pop(); $drawing.Pop()
    } finally { $drawing.Close() }
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new()
    try { $encoder.Save($stream); return ,$stream.ToArray() } finally { $stream.Dispose() }
}
[IO.File]::WriteAllBytes((Join-Path $destination 'Mu.png'), (Render-Mu 1024))
$sizes = @(16,24,32,48,64,128,256)
$frames = @($sizes | ForEach-Object { ,(Render-Mu $_) })
$iconStream = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($iconStream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    $writer.Flush()
    [IO.File]::WriteAllBytes((Join-Path $destination 'Mu.ico'), $iconStream.ToArray())
} finally { $writer.Dispose(); $iconStream.Dispose() }
Write-Output "Saved Mu.svg, Mu.png and seven-size Mu.ico in $destination"
