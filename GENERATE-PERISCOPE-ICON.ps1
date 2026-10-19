# Creates a compact native Windows ICO (16, 32, 48 and 256 px).
# Radar/periscope design, generated locally without external art or fonts.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = $PSScriptRoot
$target = Join-Path $root 'CODEX-PERISCOPE.ico'
$images = New-Object 'System.Collections.Generic.List[byte[]]'
$sizes = @(16, 32, 48, 256)
foreach ($size in $sizes) {
    $bmp = [System.Drawing.Bitmap]::new([int]$size, [int]$size)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $gfx.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $gfx.Clear([System.Drawing.Color]::Transparent)
        $navy = [System.Drawing.ColorTranslator]::FromHtml('#111926')
        $teal = [System.Drawing.ColorTranslator]::FromHtml('#29D8C3')
        $muted = [System.Drawing.ColorTranslator]::FromHtml('#438C99')
        $rect = [System.Drawing.RectangleF]::new([float]($size * 0.09), [float]($size * 0.09), [float]($size * 0.82), [float]($size * 0.82))
        $bg = [System.Drawing.SolidBrush]::new($navy)
        $pen = [System.Drawing.Pen]::new($muted, [single][Math]::Max(1.0, $size * 0.042))
        $active = [System.Drawing.Pen]::new($teal, [single][Math]::Max(1.5, $size * 0.067))
        try {
            $gfx.FillEllipse($bg, $rect)
            $gfx.DrawEllipse($pen, $rect)
            $inner = [System.Drawing.RectangleF]::new([float]($size * 0.24), [float]($size * 0.24), [float]($size * 0.52), [float]($size * 0.52))
            if ($size -ge 32) { $gfx.DrawEllipse($pen, $inner) }
            $gfx.DrawArc($active, $rect, 220, 112)
            $cx = $size / 2.0; $cy = $size / 2.0
            $gfx.DrawLine($active, $cx, $cy, $size * 0.79, $size * 0.24)
            $blip = [System.Drawing.SolidBrush]::new($teal)
            try { $gfx.FillEllipse($blip, ($size * 0.69), ($size * 0.21), ($size * 0.13), ($size * 0.13)) }
            finally { $blip.Dispose() }
        }
        finally { $bg.Dispose(); $pen.Dispose(); $active.Dispose() }
        $ms = [System.IO.MemoryStream]::new()
        try { $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $images.Add($ms.ToArray()) }
        finally { $ms.Dispose() }
    } finally { $gfx.Dispose(); $bmp.Dispose() }
}
$stream = [System.IO.File]::Create($target)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    for ($i=0; $i -lt $sizes.Count; $i++) {
        $size = $sizes[$i]
        $writer.Write([byte]($size % 256)); $writer.Write([byte]($size % 256))
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($blob in $images) { $writer.Write($blob) }
} finally { $writer.Dispose() }
# Validate before allowing the main compiler to embed it.
$icon = [System.Drawing.Icon]::new($target)
try {
    if ($icon.Width -lt 16) { throw 'Generated icon is invalid.' }
} finally { $icon.Dispose() }
Write-Host "Codex Periscope icon generated: $target" -ForegroundColor Green
